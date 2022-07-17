using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GooglePhotoSync.Google;
using GooglePhotoSync.Google.Api;
using GooglePhotoSync.Local;
using GooglePhotoSync.Sync;
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
                          .AddLogging(builder =>
                                      {
                                          builder.AddConfiguration(configuration.GetSection("Logging"));
                                          builder.AddConsole();
                                      })
                          .AddSingleton<GoogleSettings>()
                          .AddTransient<SyncPhotos>()

                          .AddSingleton<GoogleLogin>()
                          .AddTransient<AuthenticatedHttpClientHandler>()
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
                                                      //new RefitSettings
                                                      //{
                                                      //  ContentSerializer = new SystemTextJsonContentSerializer(
                                                      //                          new JsonSerializerOptions
                                                      //                          {
                                                      //                              NumberHandling = JsonNumberHandling.AllowReadingFromString
                                                      //                          })
                                                      //})
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