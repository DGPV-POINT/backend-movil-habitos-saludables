using HealthyMind.Domain.Users;

namespace HealthyMind.Application.Ports.Out;

public interface IUserCodeRepository
{
    /// <summary>El código más reciente de ese tipo, usado o no (sirve para respetar la espera entre envíos).</summary>
    Task<UserCode?> GetLatestAsync(Guid userId, UserCodePurpose purpose, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserCode>> ListUnusedAsync(Guid userId, UserCodePurpose purpose, CancellationToken cancellationToken);

    void Add(UserCode code);
}

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);

/// <summary>Correo saliente (hoy: SMTP). Sin servidor configurado solo deja el mensaje en el log.</summary>
public interface IEmailSender
{
    /// <exception cref="EmailDeliveryException">El servidor de correo no aceptó el mensaje.</exception>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed class EmailDeliveryException()
    : Exception("No pudimos enviar el correo en este momento. Intenta de nuevo en unos minutos.");

/// <summary>Cifras del tablero del administrador.</summary>
public sealed record AdminDashboard(
    int TotalUsers,
    int ActiveUsers,
    int SuspendedUsers,
    int Admins,
    int PendingVerification,
    int NewUsersLast30Days,
    int UsersActiveLast7Days,
    int ActiveHabits,
    int RecordsLast7Days,
    int RecommendationsLast7Days,
    double? HelpfulPercentage,
    int PushDevices,
    IReadOnlyList<DailyCount> SignUpsLast14Days);

public sealed record DailyCount(DateOnly Date, int Count);

public interface IAdminDashboardReader
{
    Task<AdminDashboard> ReadAsync(DateTimeOffset now, TimeZoneInfo zone, CancellationToken cancellationToken);
}
