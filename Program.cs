using GooglePhotoSync.Google;
using GooglePhotoSync.Google.Api;
using GooglePhotoSync.Local;
using GooglePhotoSync.Sync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;
using System;
using System.Threading.Tasks;

namespace GooglePhotoSync
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var configuration = LoadConfiguration();

            var services = new ServiceCollection()
                          .AddLogging(builder =>
                                      {
                                          builder.AddConfiguration(configuration.GetSection("Logging"));
                                          builder.AddConsole();
                                      })
                          .AddSingleton<GoogleSettings>()
                          .AddTransient<SyncPhotos>()

                          .AddSingleton<GoogleLogin>()
                          .AddTransient<AuthenticatedHttpClientHandler>()
                          .AddTransient<TokenRequestHttpClientHandler>()
                          .AddSingleton<IGoogleBearerTokenRetriever, GoogleBearerTokenRetriever>()
                
                          .AddTransient<LocalSource>()
                          .AddTransient<GoogleSource>()

                          .AddTransient<CollectionSync>()
                          .AddTransient<AlbumSync>()
                          .AddSingleton<Func<AlbumSync>>(sp => sp.GetService<AlbumSync>)

                          .AddTransient<SyncStateFile>()
                
                          .Configure<GoogleSettings>(configuration.GetSection(nameof(GoogleSettings)))
                          .Configure<LocalSettings>(configuration.GetSection(nameof(LocalSettings)));

            services.AddRefitClient<IGooglePhotosApi>()
                    .ConfigureHttpClient((_, c) => c.BaseAddress = new Uri(configuration.GetSection(nameof(GoogleSettings)).Get<GoogleSettings>().GooglePhotosApiBaseUrl))
                    .AddHttpMessageHandler<AuthenticatedHttpClientHandler>();

            services.AddRefitClient<IAuthToken>()
                    .ConfigureHttpClient((_, c) => c.BaseAddress = new Uri("https://www.googleapis.com/oauth2/v4/token"))
                    .AddHttpMessageHandler<TokenRequestHttpClientHandler>();
            
            var serviceProvider =  services.BuildServiceProvider();
            
            var sync = serviceProvider.GetRequiredService<SyncPhotos>();
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