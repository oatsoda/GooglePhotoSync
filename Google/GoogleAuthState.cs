using System;
using System.Text.Json.Serialization;

namespace GooglePhotoSync.Google
{
    public class GoogleAuthState
    {
        private const int _REFRESH_WITHIN_MINUTES = 5;

        [JsonPropertyName("access_token"), JsonInclude]
        public string AccessToken { get; private set; }

        [JsonIgnore] 
        private int m_ExpiresIn;

        [JsonPropertyName("expires_in"), JsonInclude]
        public int ExpiresIn
        {
            get => m_ExpiresIn;
            private set
            {
                m_ExpiresIn = value;
                ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(m_ExpiresIn);
            }
        }
        
        [JsonPropertyName("refresh_token"), JsonInclude]
        public string RefreshToken { get; private set; }
        
        [JsonIgnore]
        public DateTimeOffset ExpiresUtc { get; private set; }
        //public GoogleAuthState(string accessToken, int expiresIn, string refreshToken)
        //{
        //    AccessToken = accessToken;
        //    ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        //    RefreshToken = refreshToken;
        //}

        public bool IsExpiring()
        {
            return (ExpiresUtc - DateTimeOffset.UtcNow).TotalMinutes < _REFRESH_WITHIN_MINUTES;
        }
    }
}