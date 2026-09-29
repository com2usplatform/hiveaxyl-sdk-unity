// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Push.Addon.FCM
{
    /// <summary>
    /// Façade for the Firebase Cloud Messaging addon on Android.
    /// Wraps the codegen-generated <see cref="IFcmServiceBridge"/> for
    /// the request/response RPCs and surfaces the two one-way native events
    /// (<see cref="NotificationReceived"/> / <see cref="TokenRefreshed"/>) that
    /// travel over Core's event bus rather than as RPCs.
    /// <para>
    /// Register the port via <c>HiveBootstrap.Initialize(config, b =&gt; b.AddFCM())</c>
    /// and resolve <see cref="IFCMPlugin"/>. On non-Android build targets (and in
    /// the Editor) the port stays unregistered, so <c>HiveCore.Resolve</c> throws
    /// <see cref="Hive.Axyl.Core.RegistrationNotFoundException"/> — resolve it only
    /// on Android or guard with <c>TryResolve</c>.
    /// </para>
    /// <para>
    /// The plugin is non-orchestrating and stateless: it never
    /// registers tokens with the server, persists tokens, or
    /// requests notification permission. The game wires those flows explicitly.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Prerequisites: the addon ships the Firebase Messaging client and never
    /// initializes Firebase itself, so the project needs a default Firebase app.
    /// On Unity/Android the game supplies only the configuration file — put the
    /// <c>google-services.json</c> downloaded from the Firebase console at
    /// <c>Assets/Plugins/Android/google-services.json</c> and the addon applies
    /// Google's Services Gradle plugin to the generated Android project, which
    /// emits the resources Firebase reads at startup. Without that file the build
    /// warns and calls fail with a <c>FailedPrecondition</c> error rather than
    /// throwing. <see cref="GetTokenAsync"/> additionally requires Google Play
    /// services on the device and fails with an <c>Unavailable</c> error when they
    /// are missing or outdated. The registration token itself does not require the
    /// notification permission — the permission only affects whether notifications
    /// are displayed.
    /// </remarks>
    public interface IFCMPlugin : IDisposable
    {
        /// <summary>
        /// Returns the current FCM registration token
        /// (<c>FirebaseMessaging.getToken</c>). Each call queries the OS
        /// directly — the plugin caches nothing.
        /// </summary>
        Task<FcmServiceGetTokenResult> GetTokenAsync(CancellationToken ct = default);

        /// <summary>
        /// Discards the local registration token
        /// (<c>FirebaseMessaging.deleteToken</c>). The FCM SDK re-issues a
        /// token on the next <see cref="GetTokenAsync"/> call or via
        /// <see cref="TokenRefreshed"/>.
        /// </summary>
        Task<FcmServiceDeleteTokenResult> DeleteTokenAsync(CancellationToken ct = default);

        /// <summary>
        /// Drains the single-use cold-start buffer. On a notification tap the
        /// <c>Success</c> result's <c>Data.Message</c> carries the tapped
        /// notification's payload; on a normal launch or on the second call it
        /// carries an empty <see cref="FcmRemoteMessage"/> (its
        /// <see cref="FcmRemoteMessage.MessageId"/> is empty).
        /// </summary>
        Task<FcmServiceGetColdStartMessageResult> GetColdStartMessageAsync(CancellationToken ct = default);

        /// <summary>
        /// Raised when a remote message is delivered while the app can receive
        /// it (<c>onMessageReceived</c>): any message in the foreground, or a
        /// data message in the background. Background notification messages
        /// shown in the tray do NOT raise this event (FCM SDK behavior). The
        /// raw <see cref="FcmRemoteMessage"/> is passed verbatim. Marshalled to
        /// the engine main thread by the Core dispatcher.
        /// </summary>
        event Action<FcmRemoteMessage> NotificationReceived;

        /// <summary>
        /// Raised when the FCM SDK issues a new registration token
        /// (<c>onNewToken</c>). The game re-registers the token explicitly —
        /// the SDK does not. Marshalled to the engine main thread.
        /// </summary>
        event Action<string> TokenRefreshed;
    }
}
