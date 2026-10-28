using System.Text.Json;
using Backend.Middlewares;
using Backend.Repositories;
using Backend.Scanning;
using Backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = false;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddSingleton<IVulnerabilityRepository, VulnerabilityRepository>();
builder.Services.AddSingleton<IScanRepository, ScanRepository>();
builder.Services.AddScoped<IVulnerabilityService, VulnerabilityService>();
builder.Services.Configure<ScanOptions>(builder.Configuration.GetSection("Scan"));
builder.Services.AddSingleton<IScanService, ScanService>();
builder.Services.AddScoped<IRepositoryScanner, RepositoryScanner>();
builder.Services.AddSingleton<RepositoryUrlValidator>();
builder.Services.AddSingleton<IScanTaskQueue, ScanTaskQueue>();
builder.Services.AddHostedService<ScanBackgroundService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "DefectFixer",
        Version = "v1"
    });
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    c.RoutePrefix=string.Empty;
});

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseHttpsRedirection();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
    
app.Run();
