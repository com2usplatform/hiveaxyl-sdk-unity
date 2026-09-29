// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;

namespace Hive.Axyl.Auth
{
    /// <summary>
    /// Auth-side <see cref="ITokenRefreshHandler"/>. Performs a single call to the Token
    /// capability's refresh endpoint over a refresh-safe transport and maps the result to
    /// <see cref="TokenRefreshResponse"/> (<c>expiresIn</c> → <c>ExpiresAtSec</c>).
    /// <para>
    /// It deliberately does not retry. The <c>refresh_token</c> grant rotates on every
    /// use (server-confirmed: reissue immediately invalidates the prior token, no grace
    /// window), so it is handled as one-shot and non-idempotent. Retrying an ambiguous
    /// failure — a timeout, 5xx, or connection reset
    /// where the server may already have rotated the token before the response was lost —
    /// could replay a now-invalid token and force a logout. Automatic retry of safe
    /// (idempotent) requests is the transport layer's responsibility, not this handler's.
    /// </para>
    /// <para>
    /// Result classification: only the token endpoint's <c>invalid_grant_refresh_token</c>
    /// outcome — the server's verdict on the refresh token itself — maps to
    /// <see cref="TokenRefreshResponse.Rejected"/>. Every other unsuccessful outcome maps
    /// to <see cref="TokenRefreshResponse.Failure"/> (no verdict; Core preserves the
    /// session) or, for caller cancellation, <see cref="TokenRefreshResponse.Cancelled"/>.
    /// </para>
    /// </summary>
    internal sealed class AuthTokenRefreshHandler : ITokenRefreshHandler
    {
        private readonly ITokenService m_tokenService;
        private readonly string m_clientId;
        private readonly Func<long> m_nowUnixSeconds;

        public AuthTokenRefreshHandler(
            ITokenService tokenService,
            string clientId,
            Func<long>? nowUnixSeconds = null)
        {
            m_tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            m_clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            m_nowUnixSeconds = nowUnixSeconds
                ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public async Task<TokenRefreshResponse> RefreshAsync(string refreshToken, CancellationToken ct)
        {
            var request = new RefreshTokenTokenRequest
            {
                GrantType = "refresh_token",
                ClientId = m_clientId,
                RefreshToken = refreshToken,
            };

            // Token carries the caller's cancellation into the actual HTTP attempt.
            // IdempotencyKey is cleared because the refresh_token grant rotates the refresh
            // token and must not be tagged for idempotent replay.
            var context = new ApiCallContext { Token = ct, IdempotencyKey = null };

            TokenIssueTokenResult result;
            try
            {
                result = await m_tokenService.IssueTokenAsync(request, context).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return TokenRefreshResponse.Cancelled("Refresh cancelled.");
            }
            catch (Exception ex)
            {
                return TokenRefreshResponse.Failure($"Refresh threw: {ex.Message}");
            }

            if (result is TokenIssueTokenResult.Success s)
            {
                // Both tokens must be usable before Core can commit the refreshed session.
                if (string.IsNullOrWhiteSpace(s.Data.AccessToken) || string.IsNullOrWhiteSpace(s.Data.RefreshToken))
                {
                    return TokenRefreshResponse.Failure(
                        "Refresh succeeded but the tokens are unusable (missing or empty access/refresh token).");
                }

                long exp = ComputeExpiresAtSec(
                    s.Data.ExpiresIn, s.Data.AccessToken, m_nowUnixSeconds);
                return TokenRefreshResponse.Success(s.Data.AccessToken, s.Data.RefreshToken, exp);
            }

            // The transport reports caller cancellation as Failure(Cancelled). Recognizing
            // the code here keeps it off the failure path, so the caller that cancelled
            // sees its own Cancelled code instead of a no-verdict failure.
            if (result is TokenIssueTokenResult.Failure f
                && f.Problem.Code == HiveErrorCode.Cancelled)
            {
                return TokenRefreshResponse.Cancelled(f.Problem.Message);
            }

            // invalid_grant_refresh_token is the server's verdict on the refresh token
            // itself. The sibling invalid_grant* outcomes judge the authorization-code
            // grant and every other outcome says
            // nothing about the refresh token, so they all stay no-verdict failures.
            if (result is TokenIssueTokenResult.InvalidGrantRefreshToken)
            {
                return TokenRefreshResponse.Rejected(
                    "Refresh rejected: the server reported the refresh token invalid (invalid_grant_refresh_token).");
            }

            return TokenRefreshResponse.Failure(Describe(result));
        }

        /// <summary>
        /// ExpiresAtSec source: now + the response's
        /// <paramref name="expiresIn"/> (RFC 6749 §5.1 relative seconds),
        /// saturating instead of wrapping on
        /// an absurd server value — the result is a scheduling hint, never a
        /// verdict. The JWT exp claim is only a fallback for a response that
        /// omits the field, and a missing value must not discard the freshly
        /// rotated tokens — fall back to 0 (unknown) rather than fail.
        /// </summary>
        internal static long ComputeExpiresAtSec(
            long expiresIn, string accessToken, Func<long> nowUnixSeconds)
        {
            if (expiresIn <= 0)
            {
                JwtExp.TryReadExp(accessToken, out long fallback);
                return fallback;
            }

            long now = nowUnixSeconds();
            return expiresIn > long.MaxValue - now
                ? long.MaxValue
                : now + expiresIn;
        }

        private static string Describe(TokenIssueTokenResult result)
            => result is TokenIssueTokenResult.Failure f
                ? $"Refresh failed: {f.Problem.Code}"
                : $"Refresh failed: {result.GetType().Name}";
    }
}
