using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotoFunctions.Configuration;
using PhotoFunctions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // ----- Options -----
        services.Configure<AzureStorageOptions>(
            configuration.GetSection(AzureStorageOptions.SectionName));

        services.Configure<AzureAdOptions>(
            configuration.GetSection(AzureAdOptions.SectionName));

        // ----- Services (singletons — safe because they are stateless / thread-safe) -----
        services.AddSingleton<IJwtValidationService, JwtValidationService>();
        services.AddSingleton<IBlobStorageService, BlobStorageService>();
        services.AddSingleton<IPhotoTableService, PhotoTableService>();
    })
    .Build();

host.Run();
