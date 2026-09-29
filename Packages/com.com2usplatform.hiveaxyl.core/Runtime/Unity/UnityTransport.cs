// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_2022_3_OR_NEWER

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// Unity implementation of <see cref="ITransport"/> that wraps <see cref="UnityWebRequest"/>.
    /// <para>
    /// <see cref="UnityWebRequest.SendWebRequest"/> must be called on the main thread.
    /// This class uses <see cref="IDispatcher"/> to marshal the call when necessary.
    /// </para>
    /// </summary>
    public sealed class UnityTransport : ITransport
    {
        private readonly IDispatcher m_dispatcher;
        private readonly ILogger? m_logger;

        /// <summary>
        /// Creates a new <see cref="UnityTransport"/>.
        /// </summary>
        /// <param name="dispatcher">Main-thread dispatcher for UnityWebRequest calls.</param>
        /// <param name="logger">Optional logger for diagnostic messages.</param>
        /// <exception cref="ArgumentNullException">Thrown when dispatcher is null.</exception>
        public UnityTransport(IDispatcher dispatcher, ILogger? logger = null)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            m_dispatcher = dispatcher;
            m_logger = logger;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Marshals the request to the main thread via <see cref="IDispatcher"/> because
        /// <see cref="UnityWebRequest.SendWebRequest"/> requires it. Cancellation triggers
        /// <see cref="UnityWebRequest.Abort"/>; the cancelled state is detected by checking
        /// <see cref="CancellationToken.IsCancellationRequested"/> before inspecting
        /// <c>webRequest.result</c> to avoid confusing an aborted request with a network error.
        /// </remarks>
        public async Task<Result<HttpResponse>> SendAsync(
            HttpRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var tcs = new TaskCompletionSource<Result<HttpResponse>>();

            // Marshal to main thread — UnityWebRequest.SendWebRequest() requires it.
            m_dispatcher.Post(() => SendOnMainThread(request, ct, tcs));

#if UNITY_WEBGL
            // Resume on the caller's context: WebGL never services the ThreadPool a
            // ConfigureAwait(false) continuation would be queued to.
            return await tcs.Task;
#else
            return await tcs.Task.ConfigureAwait(false);
#endif
        }

        private async void SendOnMainThread(
            HttpRequest request,
            CancellationToken ct,
            TaskCompletionSource<Result<HttpResponse>> tcs)
        {
            try
            {
                // Early exit for already-cancelled tokens — avoids creating a
                // UnityWebRequest only to Abort() it immediately via ct.Register.
                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetResult(Result<HttpResponse>.Fail(
                        new HiveError(HiveErrorCode.Cancelled, CancellationMessage.k_Request)));
                    return;
                }

                using var webRequest = CreateUnityWebRequest(request);
                using var ctReg = ct.Register(() => webRequest.Abort());

                var operation = webRequest.SendWebRequest();
                await WaitForOperation(operation);

                // Check cancellation FIRST — Abort() puts request in NetworkError state
                // with error string "Request aborted". Checking CT directly is reliable.
                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetResult(Result<HttpResponse>.Fail(
                        new HiveError(HiveErrorCode.Cancelled, CancellationMessage.k_Request)));
                    return;
                }

                switch (webRequest.result)
                {
                    case UnityWebRequest.Result.Success:
                    case UnityWebRequest.Result.ProtocolError:
                        // HTTP-level response received (including 4xx/5xx) — Result.Ok.
                        // Transport carries bytes; status code interpretation is the caller's job.
                        var headers = ExtractHeaders(webRequest);
                        var body = webRequest.downloadHandler?.data ?? Array.Empty<byte>();
                        var response = new HttpResponse(
                            (int)webRequest.responseCode,
                            headers,
                            body,
                            request);
                        tcs.TrySetResult(Result<HttpResponse>.Ok(response));
                        return;

                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                        var networkError = ClassifyNetworkError(webRequest);
                        m_logger?.Log(LogLevel.Error, nameof(UnityTransport),
                            $"Transport error: {networkError.Message}");
                        tcs.TrySetResult(Result<HttpResponse>.Fail(networkError));
                        return;

                    default:
                        tcs.TrySetResult(Result<HttpResponse>.Fail(
                            new HiveError(HiveErrorCode.Internal,
                                $"Unexpected request state: {webRequest.result}")));
                        return;
                }
            }
            catch (Exception ex)
            {
                m_logger?.Log(LogLevel.Error, nameof(UnityTransport),
                    $"Unexpected error: {ex.Message}");
                tcs.TrySetResult(Result<HttpResponse>.Fail(
                    new HiveError(HiveErrorCode.Internal, "Internal transport error.")));
            }
        }

        /// <summary>
        /// Translates Unity-specific failure signals into platform-agnostic booleans
        /// and delegates classification to <see cref="NetworkErrorClassifier"/>.
        /// <para>
        /// Timeout is not structurally distinguishable on Unity
        /// (both report <see cref="UnityWebRequest.Result.ConnectionError"/>),
        /// so it is conservatively classified as <see cref="NetworkExternalCode.k_ConnectionError"/>.
        /// </para>
        /// </summary>
        private static HiveError ClassifyNetworkError(UnityWebRequest webRequest)
        {
            bool isConnectionError = webRequest.result == UnityWebRequest.Result.ConnectionError;
            bool isReachable = Application.internetReachability != NetworkReachability.NotReachable;
            return NetworkErrorClassifier.Classify(isConnectionError, isReachable, webRequest.error);
        }

        private static UnityWebRequest CreateUnityWebRequest(HttpRequest request)
        {
            var method = request.Method switch
            {
                HiveHttpMethod.Get => UnityWebRequest.kHttpVerbGET,
                HiveHttpMethod.Post => UnityWebRequest.kHttpVerbPOST,
                HiveHttpMethod.Put => UnityWebRequest.kHttpVerbPUT,
                HiveHttpMethod.Delete => UnityWebRequest.kHttpVerbDELETE,
                HiveHttpMethod.Patch => "PATCH",
                _ => request.Method.ToString().ToUpperInvariant(),
            };

            var webRequest = new UnityWebRequest(request.Url, method);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            if (request.Options?.TimeoutMillis > 0)
            {
                // Ceiling division: 1500ms → 2s, not 1s.
                webRequest.timeout = (request.Options.TimeoutMillis.Value + 999) / 1000;
            }

            if (request.Body != null && request.Body.Length > 0)
            {
                webRequest.uploadHandler = new UploadHandlerRaw(request.Body);
            }

            foreach (var header in request.Headers)
            {
                webRequest.SetRequestHeader(header.Key, header.Value);
            }

            return webRequest;
        }

        private static IReadOnlyDictionary<string, string> ExtractHeaders(UnityWebRequest webRequest)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var responseHeaders = webRequest.GetResponseHeaders();
            if (responseHeaders != null)
            {
                foreach (var kvp in responseHeaders)
                {
                    headers[kvp.Key] = kvp.Value;
                }
            }

            return headers;
        }

        private static Task WaitForOperation(UnityWebRequestAsyncOperation operation)
        {
            var tcs = new TaskCompletionSource<bool>();
            operation.completed += _ => tcs.TrySetResult(true);

            // If already done (synchronous completion), set immediately.
            if (operation.isDone)
            {
                tcs.TrySetResult(true);
            }

            return tcs.Task;
        }
    }
}

#endif
