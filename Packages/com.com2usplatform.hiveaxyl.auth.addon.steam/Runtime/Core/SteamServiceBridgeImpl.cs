// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;
using Hive.Axyl.Steamworks;
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
using Steamworks;
#endif

namespace Hive.Axyl.Auth.Addon.Steam
{
    /// <summary>
    /// Managed Steamworks.NET implementation of <see cref="ISteamServiceBridge"/>.
    /// Wraps <c>ISteamUser::GetAuthTicketForWebApi</c> via Steamworks.NET on Windows
    /// and macOS. All other platforms resolve as unsupported.
    /// </summary>
    internal sealed class SteamServiceBridgeImpl : ISteamServiceBridge, ISteamTicketReleaser, IDisposable
    {
        private const string NativeSteamworksCallFailed = "Native Steamworks call failed.";

        private readonly ISteamworksContext m_context;
        private readonly IDispatcher m_dispatcher;
        private bool m_disposed;

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
        private readonly ISteamUserAdapter m_steamUser;
        private readonly ConcurrentDictionary<HAuthTicket, TaskCompletionSource<SteamServiceGetAuthTicketForWebApiResult>> m_pending;
        // Successful tickets handed to the caller and kept alive: a WebApi ticket cancelled before the
        // backend validates it via AuthenticateUserTicket is rejected as invalid. Keyed by the hex
        // string returned to the caller so ReleaseTicket can cancel the exact handle once validation
        // is done; any ticket still held is cancelled on Dispose as a shutdown backstop.
        private readonly ConcurrentDictionary<string, HAuthTicket> m_issued;
        private readonly IDisposable m_callbackRegistration;
#endif

        internal SteamServiceBridgeImpl()
            : this(SteamworksContext.CreateDefault(), HiveCore.Resolve<IDispatcher>())
        {
        }

        internal SteamServiceBridgeImpl(ISteamworksContext context, IDispatcher dispatcher)
            : this(context, dispatcher,
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
                new SteamUserAdapter()
#else
                null
#endif
            )
        {
        }

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
        internal SteamServiceBridgeImpl(ISteamworksContext context, IDispatcher dispatcher, ISteamUserAdapter steamUser)
        {
            m_context = context ?? throw new ArgumentNullException(nameof(context));
            m_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            m_steamUser = steamUser ?? throw new ArgumentNullException(nameof(steamUser));
            m_pending = new ConcurrentDictionary<HAuthTicket, TaskCompletionSource<SteamServiceGetAuthTicketForWebApiResult>>();
            m_issued = new ConcurrentDictionary<string, HAuthTicket>();
            m_callbackRegistration = m_steamUser.RegisterTicketCallback(OnTicketResponse);
        }
#else
        private SteamServiceBridgeImpl(ISteamworksContext context, IDispatcher dispatcher, object _)
        {
            m_context = context ?? throw new ArgumentNullException(nameof(context));
            m_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }
#endif

        public Task<SteamServiceGetAuthTicketForWebApiResult> GetAuthTicketForWebApiAsync(
            GetAuthTicketForWebApiRequest request,
            BridgeCallContext? context = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(SteamServiceBridgeImpl));
            }

            if (!m_context.IsSupported)
            {
                return Task.FromResult(
                    SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                        new HiveError(HiveErrorCode.Unavailable, "Steam is not supported on this platform.")));
            }

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
            if (!m_context.IsInitialized)
            {
                return Task.FromResult(
                    SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                        new HiveError(
                            HiveErrorCode.FailedPrecondition,
                            "SteamAPI has not been initialized. Call SteamAPI.Init() before using SteamPlugin.")));
            }

            bool isLoggedOn;
            try
            {
                isLoggedOn = m_steamUser.IsLoggedOn;
            }
            catch (Exception ex)
            {
                return Task.FromResult(
                    SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                        new HiveError(HiveErrorCode.Internal, NativeSteamworksCallFailed, ex.GetType().Name)));
            }

            if (!isLoggedOn)
            {
                return Task.FromResult(SteamServiceGetAuthTicketForWebApiResult.ForNotAuthenticated());
            }

            return AcquireTicketAsync(request, context?.Token ?? default);
#else
            // Unreachable: IsSupported returns false on non-desktop platforms,
            // so the guard above already returned before reaching this branch.
            throw new InvalidOperationException("unreachable");
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
        private async Task<SteamServiceGetAuthTicketForWebApiResult> AcquireTicketAsync(
            GetAuthTicketForWebApiRequest request,
            CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                    new HiveError(HiveErrorCode.Cancelled, "The request was cancelled before it started."));
            }

            HAuthTicket handle;
            try
            {
                handle = m_steamUser.GetAuthTicketForWebApi(request.Identity);
            }
            catch (Exception ex)
            {
                return SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                    new HiveError(HiveErrorCode.Internal, NativeSteamworksCallFailed, ex.GetType().Name));
            }

            if (handle == HAuthTicket.Invalid)
            {
                return SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                    new HiveError(HiveErrorCode.Internal, NativeSteamworksCallFailed));
            }

            // RunContinuationsAsynchronously prevents awaiter continuations from running
            // synchronously on whichever thread completes the TCS (dispatcher or callback thread),
            // avoiding re-entrant execution on the Unity main-thread SynchronizationContext.
            var tcs = new TaskCompletionSource<SteamServiceGetAuthTicketForWebApiResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            m_pending[handle] = tcs;

            // Guards against double-delivery if disposal already removed this handle.
            using var reg = ct.Register(() =>
            {
                if (m_pending.TryRemove(handle, out var pending))
                {
                    SafeCancelAuthTicket(handle);
                    var result = SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                        new HiveError(HiveErrorCode.Cancelled, "The request was cancelled."));
                    m_dispatcher.Post(() => pending.TrySetResult(result));
                }
            });

            return await tcs.Task;
        }

        private void OnTicketResponse(GetTicketForWebApiResponse_t cb)
        {
            if (!m_pending.TryRemove(cb.m_hAuthTicket, out var tcs))
            {
                // Already removed by cancellation or Dispose.
                return;
            }

            var response = MapResponse(cb);

            // Keep a successful ticket alive so the backend can validate it via AuthenticateUserTicket;
            // cancelling it here (before validation) would make the server reject it as invalid. The
            // caller releases it through ReleaseTicket once validation completes (Dispose is the
            // shutdown backstop). Any other outcome yielded no usable ticket — including a hex-encoding
            // failure that turned an OK result into a Failure — so the handle is released now.
            if (response is SteamServiceGetAuthTicketForWebApiResult.Success success)
            {
                m_issued[success.Data.TicketHex] = cb.m_hAuthTicket;
            }
            else
            {
                SafeCancelAuthTicket(cb.m_hAuthTicket);
            }

            m_dispatcher.Post(() => tcs.TrySetResult(response));
        }

        private static SteamServiceGetAuthTicketForWebApiResult MapResponse(GetTicketForWebApiResponse_t cb)
        {
            if (cb.m_eResult == EResult.k_EResultOK)
            {
                try
                {
                    var bytes = new byte[cb.m_cubTicket];
                    Array.Copy(cb.m_rgubTicket, bytes, cb.m_cubTicket);
                    var ticketHex = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                    return SteamServiceGetAuthTicketForWebApiResult.ForSuccess(ticketHex);
                }
                catch (Exception ex)
                {
                    return SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                        new HiveError(HiveErrorCode.Internal, "Ticket hex encoding failed.", ex.GetType().Name));
                }
            }

            return cb.m_eResult switch
            {
                EResult.k_EResultNoConnection =>
                    SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                        new HiveError(HiveErrorCode.Unavailable, "Steam backend is unreachable.", cb.m_eResult.ToString())),

                EResult.k_EResultDuplicateRequest =>
                    SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                        new HiveError(HiveErrorCode.FailedPrecondition, "A duplicate ticket request is already in progress.", cb.m_eResult.ToString())),

                _ =>
                    SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                        new HiveError(HiveErrorCode.Unknown, "Steamworks returned an unexpected result.", cb.m_eResult.ToString()))
            };
        }

        // Releases the HAuthTicket handle. A native failure here must not abort the
        // surrounding flow (cancellation, callback handling, or disposal), so it is
        // swallowed and logged.
        private void SafeCancelAuthTicket(HAuthTicket handle)
        {
            try
            {
                m_steamUser.CancelAuthTicket(handle);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[SteamPlugin] CancelAuthTicket failed: {ex}");
            }
        }
#endif

        /// <inheritdoc/>
        public void ReleaseTicket(string ticketHex)
        {
            if (ticketHex == null)
            {
                throw new ArgumentNullException(nameof(ticketHex));
            }

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
            // Cancel only the exact handle that produced this hex. Unknown or already-released
            // tickets — including every call after Dispose has drained the map — are a no-op.
            if (m_issued.TryRemove(ticketHex, out var handle))
            {
                SafeCancelAuthTicket(handle);
            }
#endif
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
            m_callbackRegistration.Dispose();

            var cancelled = SteamServiceGetAuthTicketForWebApiResult.ForUntypedProblem(
                new HiveError(HiveErrorCode.Cancelled, "SteamPlugin was disposed while a request was in flight."));

            foreach (var pair in m_pending)
            {
                SafeCancelAuthTicket(pair.Key);
                pair.Value.TrySetResult(cancelled);
            }

            m_pending.Clear();

            // Backstop: release any issued ticket the caller never released via ReleaseTicket.
            foreach (var handle in m_issued.Values)
            {
                SafeCancelAuthTicket(handle);
            }

            m_issued.Clear();
#endif
        }
    }
}
