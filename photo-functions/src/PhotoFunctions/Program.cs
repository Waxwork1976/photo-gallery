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
        services.Configure<BirdApiOptions>(
            configuration.GetSection(BirdApiOptions.SectionName));
        services.Configure<GeminiOptions>(
            configuration.GetSection(GeminiOptions.SectionName));
        services.Configure<TranslatorOptions>(
            configuration.GetSection(TranslatorOptions.SectionName));

        // ----- Services (singletons — safe because they are stateless / thread-safe) -----
        services.AddSingleton<IJwtValidationService, JwtValidationService>();
        services.AddSingleton<IBlobStorageService, BlobStorageService>();
        services.AddSingleton<IPhotoTableService, PhotoTableService>();
        services.AddSingleton<IFolderTreeService, FolderTreeService>();
        services.AddSingleton<ITranslationService, TranslationService>();
        services.AddSingleton<ISlideshowSettingsService, SlideshowSettingsService>();
        services.AddHttpClient<ITranslatorService, TranslatorService>();

        services.AddHttpClient<IPlantIdentificationService, PlantIdentificationService>();
        services.AddHttpClient<IBirdIdentificationService, BirdIdentificationService>();
        services.AddHttpClient<IInsectIdentificationService, InsectIdentificationService>();
    })
    .Build();

host.Run();
