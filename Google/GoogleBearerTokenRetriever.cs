﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GooglePhotoSync.Google
{
    public class GoogleBearerTokenRetriever : IGoogleBearerTokenRetriever
    {
        private bool m_IsInit;

        private string m_CurrentToken;

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
            m_Logger.LogInformation("Authenticating");
            m_CurrentToken = await m_GoogleLogin.doOAuth(m_GoogleScope);
            if (m_CurrentToken == null)
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

            // TODO: Implement token expiry and refresh
            return await Task.FromResult(m_CurrentToken); 
        }
    }
}