using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TableMint.Application.Abstractions;
using TableMint.Application.Records;
using TableMint.Application.Tables;
using TableMint.Application.Validation;
using TableMint.Infrastructure.Persistence;
using TableMint.WebApi.Runtime;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ITableRepository, PostgresTableRepository>();
builder.Services.AddScoped<IRecordRepository, PostgresRecordRepository>();
builder.Services.AddScoped<TableManagement>();
builder.Services.AddScoped<RecordManagement>();
builder.Services.AddSingleton<DynamicValidator>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ITenantContext>(serviceProvider =>
    new ConfiguredTenantContext(
        serviceProvider.GetRequiredService<IConfiguration>()));
builder.Services.AddScoped<DemoDataSeeder>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    if (app.Configuration.GetValue<bool>("SeedData:Enabled"))
    {
        await scope.ServiceProvider
            .GetRequiredService<DemoDataSeeder>()
            .SeedAsync();
    }
}

app.UseExceptionHandler();

app.MapOpenApi();
app.MapGet("/health", () => Results.Text("healthy"));
app.MapGet("/swagger", () => Results.Content(
    """
    <!doctype html>
    <html lang="zh-Hant">
    <head>
      <meta charset="utf-8">
      <meta name="viewport" content="width=device-width, initial-scale=1">
      <title>TableMint API</title>
      <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui.css">
    </head>
    <body>
      <div id="swagger-ui"></div>
      <script src="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
      <script>
        SwaggerUIBundle({ url: '/openapi/v1.json', dom_id: '#swagger-ui', deepLinking: true });
      </script>
    </body>
    </html>
    """,
    "text/html"));

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
