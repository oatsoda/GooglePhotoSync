using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GooglePhotoSync.Google
{
    public class GoogleBearerTokenRetriever : IGoogleBearerTokenRetriever
    {
        private bool m_IsInit;

        private GoogleAuthState m_CurrentAuthState;

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
            m_CurrentAuthState = await m_GoogleLogin.DoOAuth(m_GoogleScope);
            if (m_CurrentAuthState == null)
            {
                m_Logger.LogError("Authentication Failed");
                return false;
            }

            m_Logger.LogInformation("Authentication succeeded");
            m_IsInit = true;
            return true;
        }

        public async Task<string> GetToken()
        {
            if (!m_IsInit)
                throw new InvalidOperationException($"{GetType().Name} must be initialised before first usage. Ensure you call {nameof(Init)}() first");;

            if (m_CurrentAuthState.IsExpiring())
                m_CurrentAuthState = await m_GoogleLogin.GetNewAccessToken(m_CurrentAuthState);
            
            return await Task.FromResult(m_CurrentAuthState.AccessToken); 
        }
    }
}