using System.Threading.Tasks;

namespace GooglePhotoSync.Google
{
    public interface IGoogleBearerTokenRetriever
    {
        Task<bool> Init();
        Task<string> GetToken();
    }
}