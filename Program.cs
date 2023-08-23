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
                          //.AddSingleton<GoogleSettings>()
                          .AddTransient<SyncPhotos>()

                          .AddSingleton<GoogleLogin>()
                          .AddTransient<AuthenticatedHttpClientHandler>()
                          .AddSingleton<GoogleBearerTokenRetriever>()
                          .AddTransient<GoogleSource>()
                
                          .ConfigureSettings<LocalSettings>(configuration)
                          .AddTransient<LocalSource>()

                          .AddTransient<CollectionComparer>()
                          .AddTransient<CollectionSync>()
                          .AddTransient<AlbumSync>()
                          .ConfigureSettings<SyncSettings>(configuration)
                          //.AddSingleton<Func<AlbumSync>>(sp => sp.GetService<AlbumSync>)
                          ;
                          
            var googleSettings = services.ConfigureAndGet<GoogleSettings>(configuration);

            services.AddRefitClient<IGooglePhotosApi>()
                    .ConfigureHttpClient((_, c) => c.BaseAddress = new Uri(googleSettings.GooglePhotosApiBaseUrl))
                    .AddHttpMessageHandler<AuthenticatedHttpClientHandler>();

            services.AddRefitClient<IAuthToken>()
                    .ConfigureHttpClient((_, c) => c.BaseAddress = new Uri(googleSettings.GoogleTokenUrl));

            var sync =  services.BuildServiceProvider().GetRequiredService<SyncPhotos>();
            await sync.Sync();
        }

        private static IConfiguration LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

#if DEBUG 
            builder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
#else
            builder.AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true);
#endif

            return builder.Build();
        }
    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureSettings<T>(this IServiceCollection services, IConfiguration configuration) where T : class 
        {
            var settings = configuration.GetRequiredSection(typeof(T).Name);
            services.Configure<T>(settings);
            return services;
        }

        public static T ConfigureAndGet<T>(this IServiceCollection services, IConfiguration configuration) where T : class 
        {
            var settings = configuration.GetRequiredSection(typeof(T).Name);
            services.Configure<T>(settings);
            return settings.Get<T>();
        }
    }
}