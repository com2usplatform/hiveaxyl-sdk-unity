// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;

namespace Hive.Axyl.Push.Addon.FCM
{
    /// <summary>
    /// Convenience accessors for <see cref="FcmRemoteMessage"/>. The DTO carries the
    /// raw wire fields (<see cref="FcmRemoteMessage.SentTimeUnixMillis"/> as a
    /// <c>long</c> and <see cref="FcmRemoteMessage.TtlSeconds"/> as an <c>int</c>);
    /// these convenience accessors surface them in the engine-standard signature
    /// types — a UTC <see cref="DateTimeOffset"/> and a <see cref="TimeSpan"/>.
    /// </summary>
    // Design note: hand-written companion to the generated FcmRemoteMessage DTO (dto.g.cs).
    // FCM reports these values as Unix epoch milliseconds and seconds, so the proto carries
    // the external formats verbatim on the wire and the engine layer performs the conversion.
    public partial class FcmRemoteMessage
    {
        /// <summary>
        /// <c>RemoteMessage.getSentTime()</c> as a UTC <see cref="DateTimeOffset"/>,
        /// converted from the raw <see cref="SentTimeUnixMillis"/> (Unix epoch
        /// milliseconds). An out-of-range value clamps to
        /// <see cref="DateTimeOffset.UnixEpoch"/> — FCM stamps sane values, so the
        /// fallback is unreachable in practice; the guard keeps a malformed
        /// payload from faulting the accessor.
        /// </summary>
        public DateTimeOffset SentTime
        {
            get
            {
                try
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(SentTimeUnixMillis);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return DateTimeOffset.UnixEpoch;
                }
            }
        }

        /// <summary>
        /// <c>RemoteMessage.getTtl()</c> as a <see cref="TimeSpan"/>, converted from
        /// the raw <see cref="TtlSeconds"/>. The wire value is a proto <c>int32</c>
        /// (max ~2.1e9 s), always within <see cref="TimeSpan"/> range, so no
        /// overflow guard is needed (unlike <see cref="SentTime"/>'s <c>long</c>).
        /// </summary>
        public TimeSpan Ttl => TimeSpan.FromSeconds(TtlSeconds);
    }
}
