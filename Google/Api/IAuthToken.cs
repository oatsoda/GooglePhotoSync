using Refit;
using System.Net.Http;
using System.Threading.Tasks;

namespace GooglePhotoSync.Google.Api
{
    public interface IAuthToken
    {
        [Headers("Content-Type: application/x-www-form-urlencoded", "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8")]
        [Post("")]
        public Task<GoogleAuthState> RequestToken([Body(BodySerializationMethod.UrlEncoded)] FormUrlEncodedContent content);
    }
}
