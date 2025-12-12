using Microsoft.EntityFrameworkCore;
using AssetTracking.Rfid.Infrastructure.Persistence;
using AssetTracking.Rfid.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy
                .AllowAnyOrigin()      // Allow Angular localhost
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

// Configure PostgreSQL connection
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=assetTracking;Username=postgres;Password=postgres;";

// In Program.cs
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=assetTracking;Username=postgres;Password=postgres;")
           .UseSnakeCaseNamingConvention());

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("AllowAngular");

app.UseAuthorization();

// Audit logging middleware
app.UseMiddleware<AuditLoggingMiddleware>();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
