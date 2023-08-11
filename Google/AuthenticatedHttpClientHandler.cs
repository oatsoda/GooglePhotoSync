using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace GooglePhotoSync.Google
{
    /// <summary>
    /// Handler to intercept each HttpRequest and Replace Authorization header with with Bearer Token from Google.
    /// </summary>
    public class AuthenticatedHttpClientHandler : DelegatingHandler
    {
        private readonly GoogleBearerTokenRetriever m_TokenRetriever;

        public AuthenticatedHttpClientHandler(GoogleBearerTokenRetriever tokenRetriever)
        {
            m_TokenRetriever = tokenRetriever;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // See if the request has an authorize header
            var auth = request.Headers.Authorization;
            if (auth != null)
            {
                var token = await m_TokenRetriever.GetToken().ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue(auth.Scheme, token);
            }
            
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}