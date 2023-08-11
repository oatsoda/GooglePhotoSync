using GooglePhotoSync.Google.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Refit;

namespace GooglePhotoSync.Google
{
    // Code based on https://github.com/googlesamples/oauth-apps-for-windows/blob/master/OAuthConsoleApp/OAuthConsoleApp/Program.cs
    // Changed return type from string to GoogleAuthState. Added GetNewAccessToken method for using refresh token.
    public class GoogleLogin
    {
        private const string _AUTHORIZATION_ENDPOINT = "https://accounts.google.com/o/oauth2/v2/auth";
        
        private readonly string m_ClientId;
        private readonly string m_ClientSecret;
        private readonly IAuthToken m_AuthTokenApi;
        private readonly ILogger<GoogleLogin> m_Logger;

        public GoogleLogin(IOptions<GoogleSettings> googleSettings, IAuthToken authTokenApi, ILogger<GoogleLogin> logger)
        {
            m_ClientId = googleSettings.Value.GoogleClientId;
            m_ClientSecret = googleSettings.Value.GoogleClientSecret;
            m_AuthTokenApi = authTokenApi;
            m_Logger = logger;
        }

        // ref http://stackoverflow.com/a/3978040
        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public async Task<GoogleAuthState> DoOAuth(params string[] additionalScopes)
        {
            // Generates state and PKCE values.
            var state = RandomDataBase64Url(32);
            var codeVerifier = RandomDataBase64Url(32);
            var codeChallenge = Base64UrlencodeNoPadding(Sha256(codeVerifier));
            const string codeChallengeMethod = "S256";

            // Creates a redirect URI using an available port on the loopback address.
            var redirectUri = $"http://{IPAddress.Loopback}:{GetRandomUnusedPort()}/";
            Output("redirect URI: " + redirectUri);

            // Creates an HttpListener to listen for requests on that redirect URI.
            var http = new HttpListener();
            http.Prefixes.Add(redirectUri);
            Output("Listening..");
            http.Start();

            // Creates the OAuth 2.0 authorization request.
            var addScopes = string.Join("%20", additionalScopes.Select(WebUtility.UrlEncode)).Trim();
            if (!string.IsNullOrEmpty(addScopes))
                addScopes = $"%20{addScopes}";

            var authorizationRequest = string.Format("{0}?response_type=code&scope=openid%20profile{6}&redirect_uri={1}&client_id={2}&state={3}&code_challenge={4}&code_challenge_method={5}",
                                                     _AUTHORIZATION_ENDPOINT,
                                                     Uri.EscapeDataString(redirectUri),
                                                     m_ClientId,
                                                     state,
                                                     codeChallenge,
                                                     codeChallengeMethod,
                                                     addScopes);

            Output(authorizationRequest);

            // Opens request in the browser.
            var ps = new System.Diagnostics.ProcessStartInfo(authorizationRequest)
                     {
                         UseShellExecute = true, // Doesn't work without this - but makes it only work on Windows
                         Verb = "open"
                     };
            var process = System.Diagnostics.Process.Start(ps);
            if (process == null)
                m_Logger.LogError("Failed to start browser.");
            else if (process.HasExited)
                m_Logger.LogError("Browser started and exited. pid {pid}", process.Id);
            else
                m_Logger.LogDebug($@"Browser Started pid {process.Id} {process.ProcessName}");

            // Waits for the OAuth authorization response.
            var context = await http.GetContextAsync();

            // Brings the Console to Focus.
            BringConsoleToFront();

            // Sends an HTTP response to the browser.
            var response = context.Response;
            const string responseString = "<html><body>Please return to the app.</body></html>";
            var buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var responseOutput = response.OutputStream;
            await responseOutput.WriteAsync(buffer, 0, buffer.Length).ContinueWith(_ =>
                                                                                   {
                                                                                       responseOutput.Close();
                                                                                       http.Stop();
                                                                                       Output("HTTP server stopped.");
                                                                                   });

            // Checks for errors.
            if (context.Request.QueryString.Get("error") != null)
            {
                Output($"OAuth authorization error: {context.Request.QueryString.Get("error")}.");
                return null;
            }

            if (context.Request.QueryString.Get("code") == null
                || context.Request.QueryString.Get("state") == null)
            {
                Output("Malformed authorization response. " + context.Request.QueryString);
                return null;
            }

            // extracts the code
            var code = context.Request.QueryString.Get("code");
            var incomingState = context.Request.QueryString.Get("state");

            // Compares the receieved state to the expected value, to ensure that
            // this app made the request which resulted in authorization.
            if (incomingState != state)
            {
                Output($"Received request with invalid state ({incomingState})");
                return null;
            }

            Output("Authorization code: " + code);

            // Starts the code exchange at the Token Endpoint.
            return await PerformCodeExchange(code, codeVerifier, redirectUri);
        }

        private async Task<GoogleAuthState> PerformCodeExchange(string code, string codeVerifier, string redirectUri)
        {
            Output("Exchanging code for tokens...");

            return await m_AuthTokenApi.RequestToken(
                                               new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
                                                                         {
                                                                             new("code", code),
                                                                             new("redirect_uri", redirectUri),
                                                                             new("client_id", m_ClientId),
                                                                             new("code_verifier", codeVerifier),
                                                                             new("client_secret", m_ClientSecret),
                                                                             new("scope", ""),
                                                                             new("grant_type", "authorization_code")
                                                                         })
                                               );
        }

        public async Task<GoogleAuthState> GetNewAccessToken(string refreshToken)
        {
            Output("Getting new access token via refresh token...");
            
            return await m_AuthTokenApi.RequestToken(
                                                     new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
                                                                               {
                                                                                   new("client_id", m_ClientId),
                                                                                   new("client_secret", m_ClientSecret),
                                                                                   new("refresh_token", refreshToken),
                                                                                   new("grant_type", "refresh_token")
                                                                               })
                                                    );
        }

        private void Output(string output)
        {
            m_Logger.LogDebug(output);
        }

        /// <summary>
        /// Returns URI-safe data with a given input length.
        /// </summary>
        /// <param name="length">Input length (nb. output will be longer)</param>
        /// <returns></returns>
        private static string RandomDataBase64Url(uint length)
        {
            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[length];
            rng.GetBytes(bytes);
            return Base64UrlencodeNoPadding(bytes);
        }

        /// <summary>
        /// Returns the SHA256 hash of the input string.
        /// </summary>
        private static byte[] Sha256(string input)
        {
            var bytes = Encoding.ASCII.GetBytes(input);
            var sha256 = SHA256.Create();
            return sha256.ComputeHash(bytes);
        }

        /// <summary>
        /// Base64url no-padding encodes the given input buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private static string Base64UrlencodeNoPadding(byte[] buffer)
        {
            var base64 = Convert.ToBase64String(buffer);

            // Converts base64 to base64url.
            base64 = base64.Replace("+", "-");
            base64 = base64.Replace("/", "_");
            // Strips padding.
            base64 = base64.Replace("=", "");

            return base64;
        }

        // Hack to bring the Console window to front.
        // ref: http://stackoverflow.com/a/12066376

        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public static void BringConsoleToFront()
        {
            SetForegroundWindow(GetConsoleWindow());
        }
    }
}