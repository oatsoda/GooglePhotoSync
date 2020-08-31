using System;

namespace GooglePhotoSync.Google
{
    public class GoogleAuthState
    {
        private const int _REFRESH_WITHIN_MINUTES = 5;

        public string AccessToken { get; }
        public DateTimeOffset ExpiresUtc { get; }
        public string RefreshToken { get; }

        public GoogleAuthState(string accessToken, int expiresIn, string refreshToken)
        {
            AccessToken = accessToken;
            ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            RefreshToken = refreshToken;
        }

        public bool IsExpiring()
        {
            return (ExpiresUtc - DateTimeOffset.UtcNow).TotalMinutes < _REFRESH_WITHIN_MINUTES;
        }
    }
}