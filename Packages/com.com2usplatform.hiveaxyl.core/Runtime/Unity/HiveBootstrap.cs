// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_2022_3_OR_NEWER

#nullable enable

using System;
using UnityEngine;

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// Unity adapter that bridges <see cref="HiveCore"/> with the Unity application lifecycle.
    /// Creates a hidden <see cref="GameObject"/> that persists across scene loads and forwards
    /// pause, resume, and quit events to <see cref="HiveCore"/>.
    /// <para>
    /// Game developers call <see cref="Initialize"/> once from a main-thread context
    /// (e.g., <c>MonoBehaviour.Start</c>). The method assembles Unity-specific adapters
    /// and delegates to <see cref="HiveCore.Initialize"/>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// // GameStartup.cs
    /// void Start()
    /// {
    ///     var config = CoreConfig.CreateBuilder("com.example.game")
    ///         .SetTimeoutMillis(10000)
    ///         .Build();
    ///
    ///     HiveBootstrap.Initialize(config);
    /// }
    /// </code>
    /// </example>
    public sealed class HiveBootstrap : MonoBehaviour
    {
        // Unity WebGL runs managed code on a single thread with no ThreadPool service;
        // Core resumes its awaits on the captured context there (see PlatformAdapters).
#if UNITY_WEBGL
        private const bool k_SingleThreadedHost = true;
#else
        private const bool k_SingleThreadedHost = false;
#endif

        private static HiveBootstrap? s_instance;

        /// <summary>
        /// Initializes the Hive Axyl SDK for Unity.
        /// Must be called on the main thread. Creates a persistent <see cref="GameObject"/>,
        /// assembles Unity adapters (<see cref="UnityDispatcher"/>, <see cref="UnityTransport"/>,
        /// <see cref="UnityAppEnvironment"/>), and delegates to <see cref="HiveCore.Initialize"/>.
        /// </summary>
        /// <param name="config">
        /// SDK configuration created via <see cref="CoreConfig.CreateBuilder"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="config"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the SDK is already initialized.
        /// </exception>
        /// <exception cref="DllNotFoundException">
        /// Windows only. Thrown when the native plugin cannot be loaded — either it is
        /// absent from the build, or the machine does not have the Microsoft Visual C++
        /// 2015–2022 Redistributable (x64) that the plugin links against. Windows does
        /// not include that runtime and Unity does not install it with the player, so it
        /// is a prerequisite on every machine that runs the game. The exception message
        /// carries the installer link.
        /// </exception>
        public static void Initialize(CoreConfig config)
            => Initialize(config, (Action<IHiveBuilder>)null);

        /// <summary>
        /// Initializes the SDK for Unity and runs an assembly closure to register
        /// capabilities and platform ports (see
        /// <see cref="HiveCore.Initialize(CoreConfig, PlatformAdapters, System.Action{IHiveBuilder})"/>).
        /// </summary>
        /// <param name="config">
        /// SDK configuration created via <see cref="CoreConfig.CreateBuilder"/>.
        /// </param>
        /// <param name="assemble">
        /// Optional registration closure for capabilities and platform ports; may be null.
        /// </param>
        /// <param name="decorateTransport">
        /// Optional wrapper applied to the Unity transport before it enters the pipeline.
        /// Identity when null; must not return null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="config"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="decorateTransport"/> returns null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the SDK is already initialized.
        /// </exception>
        /// <exception cref="DllNotFoundException">
        /// Windows only. Thrown when the native plugin cannot be loaded — either it is
        /// absent from the build, or the machine does not have the Microsoft Visual C++
        /// 2015–2022 Redistributable (x64) that the plugin links against. Windows does
        /// not include that runtime and Unity does not install it with the player, so it
        /// is a prerequisite on every machine that runs the game. The exception message
        /// carries the installer link.
        /// </exception>
        public static void Initialize(
            CoreConfig config,
            Action<IHiveBuilder> assemble,
            Func<ITransport, ITransport>? decorateTransport = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (HiveCore.IsInitialized)
            {
                throw new InvalidOperationException(
                    "HiveCore is already initialized. Call Shutdown() before re-initializing.");
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // Hand the current Activity to the engine-neutral
            // AxylAndroidActivity holder before any native plugin runs.
            // Downstream Android-only addons (Credential Manager, IAP
            // dialogs, etc.) consume current() through the holder; the
            // platform-integration AARs intentionally do not import
            // UnityPlayer themselves so the AARs stay reusable across
            // Unity / Unreal / pure native hosts.
            AndroidActivityBinder.Bind();
#endif

            // Assemble Unity adapters (main thread guaranteed by caller).
            var dispatcher = new UnityDispatcher();
            var environment = new UnityAppEnvironment();
            ITransport transport = new UnityTransport(dispatcher);
            if (decorateTransport != null)
            {
                // App-supplied wrapper (e.g. QA traffic logging). Runs once,
                // synchronously, on the main thread before HiveCore.Initialize.
                // Guard the return so a caller that mistakenly yields null fails
                // here with a clear cause, not a misleading ArgumentNullException
                // from the PlatformAdapters constructor.
                transport = decorateTransport(transport)
                    ?? throw new ArgumentException(
                        "decorateTransport returned null.", nameof(decorateTransport));
            }

            var sinks = config.Log.EnableConsole
                ? new ILogSink[] { new UnityConsoleSink() }
                : null;

            // CreatePlatformInvoker is passed as a method group; NativeBridge invokes it
            // exactly once during its own ctor, after the composer's sinks are wired up.
            var adapters = new PlatformAdapters(
                dispatcher, transport, environment, sinks, CreatePlatformInvoker, k_SingleThreadedHost);
            HiveCore.Initialize(config, adapters, assemble);

            // Create the persistent GameObject only after HiveCore init succeeds,
            // so a mid-init exception does not leave an orphan GameObject in the scene.
            var go = new GameObject("HiveBootstrap");
            s_instance = go.AddComponent<HiveBootstrap>();
            DontDestroyOnLoad(go);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                HiveCore.Suspend();
            }
            else
            {
                HiveCore.Resume();
            }
        }

        private void OnApplicationQuit()
        {
            HiveCore.Shutdown();
        }

        private void OnDestroy()
        {
            // Idempotent: handles Editor play-mode stop where OnApplicationQuit
            // may not be called reliably.
            HiveCore.Shutdown();
            s_instance = null;
        }

        /// <summary>
        /// Selects the <see cref="IPlatformInvoker"/> matching the current Unity build target.
        /// Conditional compilation is concentrated here so <see cref="Initialize"/> stays
        /// platform-agnostic. Linux Editor and macOS Editor (deferred) fall through to
        /// <see cref="NoOpPlatformInvoker"/>, which keeps an <see cref="INativeBridge"/>
        /// registered and surfaces calls as UNIMPLEMENTED envelopes instead of crashing.
        /// </summary>
        /// <param name="onAsyncResponse">
        /// Composer-side <c>(callbackId, responseJson)</c> sink; forwarded to the invoker ctor.
        /// </param>
        /// <param name="onEvent">
        /// Composer-side <c>(eventName, jsonPayload)</c> sink; forwarded to the invoker ctor.
        /// </param>
        private static IPlatformInvoker CreatePlatformInvoker(
            Action<int, string> onAsyncResponse,
            Action<string, string> onEvent)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidPlatformInvoker(onAsyncResponse, onEvent);
#elif (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            return new ApplePlatformInvoker(onAsyncResponse, onEvent);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return new WindowsPlatformInvoker(onAsyncResponse, onEvent);
#elif UNITY_WEBGL && !UNITY_EDITOR
            return new WebGLPlatformInvoker(onAsyncResponse, onEvent);
#else
            return new NoOpPlatformInvoker(onAsyncResponse, onEvent);
#endif
        }
    }
}

#endif
