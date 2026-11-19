using FluentValidation;
using HealthyMind.Application.Features.Auth;
using HealthyMind.Application.Features.HabitRecords;
using HealthyMind.Application.Features.Habits;
using HealthyMind.Application.Features.Notifications;
using HealthyMind.Application.Features.Recommendations;
using HealthyMind.Application.Features.Statistics;
using HealthyMind.Application.Features.Sync;
using HealthyMind.Application.Features.Users;
using HealthyMind.Application.Ports.In;
using Microsoft.Extensions.DependencyInjection;

namespace HealthyMind.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>()
            .BindConfiguration(AuthOptions.SectionName)
            .Validate(options => options.RefreshTokenLifetimeDays > 0, "Auth:RefreshTokenLifetimeDays debe ser mayor que cero.")
            .ValidateOnStart();

        services.AddOptions<RecommendationOptions>()
            .BindConfiguration(RecommendationOptions.SectionName)
            .Validate(
                options => options.MinHoursBetween > 0 && options.LookbackDays > 0 && options.BatchSize > 0,
                "La sección Recommendations tiene valores inválidos.")
            .ValidateOnStart();

        services.AddOptions<AdminOptions>().BindConfiguration(AdminOptions.SectionName);

        services.AddOptions<PushOptions>()
            .BindConfiguration(PushOptions.SectionName)
            .Validate(options => options.StaleAfterDays > 0, "Push:StaleAfterDays debe ser mayor que cero.")
            .ValidateOnStart();

        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>(includeInternalTypes: true);
        services.AddSingleton(TimeProvider.System);

        // Piezas internas compartidas entre casos de uso.
        services.AddScoped<UserRegistration>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<AccountCodes>();
        services.AddScoped<OwnedResources>();
        services.AddScoped<ComplianceReader>();

        // Puertos de entrada.
        services.AddScoped<IAuthUseCases, AuthUseCases>();
        services.AddScoped<ISessionUseCases, SessionUseCases>();
        services.AddScoped<IProfileUseCases, ProfileUseCases>();
        services.AddScoped<IUserAdministrationUseCases, UserAdministrationUseCases>();
        services.AddScoped<IHabitUseCases, HabitUseCases>();
        services.AddScoped<IHabitRecordUseCases, HabitRecordUseCases>();
        services.AddScoped<IStatisticsUseCases, StatisticsUseCases>();
        services.AddScoped<IRecommendationUseCases, RecommendationUseCases>();
        services.AddScoped<IRecommendationGenerationUseCases, RecommendationGenerationUseCases>();
        services.AddScoped<ISyncUseCases, SyncUseCases>();
        services.AddScoped<PushUseCases>();
        services.AddScoped<IPushUseCases>(provider => provider.GetRequiredService<PushUseCases>());
        services.AddScoped<IRecommendationPublisher>(provider => provider.GetRequiredService<PushUseCases>());

        return services;
    }
}
