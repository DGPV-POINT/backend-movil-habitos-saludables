using System.Text.Json.Serialization;
using HealthyMind.Api.Infrastructure;
using HealthyMind.Application;
using HealthyMind.Application.Features.Users;
using HealthyMind.Application.Ports.In;
using HealthyMind.Infrastructure;
using HealthyMind.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddJwtAuthentication();
builder.Services.AddAuthRateLimiting(builder.Configuration);
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecurityTransformer>());
builder.Services.AddHealthChecks().AddDbContextCheck<HealthyMindDbContext>("postgres");
builder.Services.AddReverseProxySupport(builder.Configuration);

var app = builder.Build();

// Primero: detrás del IIS, la IP real del cliente y el esquema https vienen en X-Forwarded-*.
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("HealthyMind API"));
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// El móvil lo consulta para saber si hay conexión real con el servidor (no solo wifi) antes de sincronizar.
app.MapHealthChecks("/health").AllowAnonymous();

await PrepareDatabaseAsync(app);
await app.RunAsync();

static async Task PrepareDatabaseAsync(WebApplication app)
{
    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await app.Services.MigrateDatabaseAsync();
    }

    var admin = app.Configuration.GetSection("BootstrapAdmin").Get<BootstrapAdminRequest>();
    if (admin is not null && !string.IsNullOrWhiteSpace(admin.Password))
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<IUserAdministrationUseCases>()
            .EnsureAdminExistsAsync(admin, CancellationToken.None);
    }
}

/// <summary>Punto de entrada visible para las pruebas de integración.</summary>
public partial class Program;
