using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotoFunctions.Configuration;
using PhotoFunctions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // ----- Options -----
        services.Configure<AzureStorageOptions>(
            configuration.GetSection(AzureStorageOptions.SectionName));

        services.Configure<AzureAdOptions>(
            configuration.GetSection(AzureAdOptions.SectionName));

        services.Configure<PlantNetOptions>(
            configuration.GetSection(PlantNetOptions.SectionName));

        // ----- Services (singletons — safe because they are stateless / thread-safe) -----
        services.AddSingleton<IJwtValidationService, JwtValidationService>();
        services.AddSingleton<IBlobStorageService, BlobStorageService>();
        services.AddSingleton<IPhotoTableService, PhotoTableService>();
        services.AddSingleton<IFolderTreeService, FolderTreeService>();

        services.AddHttpClient<IPlantIdentificationService, PlantIdentificationService>();
    })
    .Build();

host.Run();
