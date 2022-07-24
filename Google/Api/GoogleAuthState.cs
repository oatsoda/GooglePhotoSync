using System.Text.Json.Serialization;

namespace GooglePhotoSync.Google.Api
{
    public class GoogleAuthState
    {
        [JsonPropertyName("access_token"), JsonInclude]
        public string AccessToken { get; private set; }
        
        [JsonPropertyName("expires_in"), JsonInclude]
        public int ExpiresIn { get; private set; }
        
        [JsonPropertyName("refresh_token"), JsonInclude]
        public string RefreshToken { get; private set; }

    }
}