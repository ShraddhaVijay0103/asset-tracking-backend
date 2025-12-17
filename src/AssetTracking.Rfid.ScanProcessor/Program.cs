using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AssetTracking.Rfid.Infrastructure.Persistence;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, configBuilder) =>
    {
        configBuilder
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=assetTracking;Username=postgres;Password=postgres;";

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));
    })
    .Build();

host.Run();
