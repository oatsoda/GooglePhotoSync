using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GooglePhotoSync.Google
{
    public class GoogleBearerTokenRetriever
    {
        private bool m_IsInit;

        private GoogleAuthTokens m_CurrentAuthTokens;

        private readonly GoogleLogin m_GoogleLogin;
        private readonly string m_GoogleScope;
        private readonly ILogger<GoogleBearerTokenRetriever> m_Logger;

        public GoogleBearerTokenRetriever(GoogleLogin googleLogin, IOptions<GoogleSettings> googleSettings, ILogger<GoogleBearerTokenRetriever> logger)
        {
            m_GoogleLogin = googleLogin;
            m_GoogleScope = googleSettings.Value.GooglePhotoScope;
            m_Logger = logger;
        }

        public async Task<bool> Init()
        {
            m_Logger.LogDebug("Debug Check");
            m_Logger.LogInformation("Authenticating");

            if (await CheckForCachedToken())
            {
                m_Logger.LogInformation("Using Cached Token");
            }
            else
            {
                var authState = await m_GoogleLogin.DoOAuth(m_GoogleScope);
                if (authState == null)
                {
                    m_Logger.LogError("Authentication Failed");
                    return false;
                }

                m_CurrentAuthTokens = GoogleAuthTokens.FromAuthState(authState);
                await SaveCachedToken(m_CurrentAuthTokens);
                m_Logger.LogInformation("Authentication succeeded");
            }

            m_IsInit = true;
            return true;
        }

        public async Task<string> GetToken()
        {
            if (!m_IsInit)
                throw new InvalidOperationException($"{GetType().Name} must be initialised before first usage. Ensure you call {nameof(Init)}() first");

            if (m_CurrentAuthTokens.IsExpiring())
            {
                m_Logger.LogInformation("Token Expiring...Refreshing Token...");
                var authState = await m_GoogleLogin.GetNewAccessToken(m_CurrentAuthTokens.RefreshToken);
                m_CurrentAuthTokens = GoogleAuthTokens.FromAuthState(authState, m_CurrentAuthTokens.RefreshToken); // On refreshing the token, the refresh_token will be null so persist the original
                await SaveCachedToken(m_CurrentAuthTokens);
            }

            return await Task.FromResult(m_CurrentAuthTokens.AccessToken);
        }

        private const string _TOKEN_CACHE_FILE_NAME = "tokencache";

        private async Task<bool> CheckForCachedToken()
        {
            if (!File.Exists(_TOKEN_CACHE_FILE_NAME))
                return false;

            var encryptedBytes = await File.ReadAllBytesAsync(_TOKEN_CACHE_FILE_NAME);

#pragma warning disable CA1416 // Validate platform compatibility
            var bytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416 // Validate platform compatibility

            m_CurrentAuthTokens = JsonSerializer.Deserialize<GoogleAuthTokens>(bytes);
            return true;
        }

        private async Task SaveCachedToken(GoogleAuthTokens authTokens)
        {
            var json = JsonSerializer.Serialize(authTokens);
            var bytes = Encoding.UTF8.GetBytes(json);

#pragma warning disable CA1416 // Validate platform compatibility
            var encryptedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416 // Validate platform compatibility

            await File.WriteAllBytesAsync(_TOKEN_CACHE_FILE_NAME, encryptedBytes);
        }
    }
}