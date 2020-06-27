using System;
using System.Threading.Tasks;
using GooglePhotoSync.Google;
using GooglePhotoSync.Google.Api;
using GooglePhotoSync.Local;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;

namespace GooglePhotoSync
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var configuration = LoadConfiguration();

            var services = new ServiceCollection()
                          .AddLogging(builder => { builder.AddConsole(); })
                          .AddSingleton<GoogleSettings>()
                          .AddTransient<SyncPhotos>()

                          .AddSingleton<GoogleLogin>()
                          .AddSingleton<AuthenticatedHttpClientHandler>()
                          .AddSingleton<IGoogleBearerTokenRetriever, GoogleBearerTokenRetriever>()
                
                          .AddTransient<LocalSource>()
                          .AddTransient<GoogleSource>()
                
                          .Configure<GoogleSettings>(configuration.GetSection(nameof(GoogleSettings)))
                          .Configure<LocalSettings>(configuration.GetSection(nameof(LocalSettings)));

            services.AddRefitClient<IGooglePhotosApi>()
                    .ConfigureHttpClient((sp, c) => c.BaseAddress = new Uri(configuration.GetSection(nameof(GoogleSettings)).Get<GoogleSettings>().GooglePhotosApiBaseUrl))
                    .AddHttpMessageHandler<AuthenticatedHttpClientHandler>();

            var serviceProvider =  services.BuildServiceProvider();
            
            var sync = serviceProvider.GetService<SyncPhotos>();
            await sync.Sync();

            Console.ReadLine();
        }

        private static IConfiguration LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

#if DEBUG 
            builder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
#endif

            return builder.Build();
        }
    }
}