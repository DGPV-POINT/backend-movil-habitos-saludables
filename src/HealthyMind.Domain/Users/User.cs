using HealthyMind.Domain.Common;

namespace HealthyMind.Domain.Users;

public sealed class User : Entity
{
    public const int UsernameMinLength = 4;
    public const int UsernameMaxLength = 50;
    public const int EmailMaxLength = 150;
    public const int NameMaxLength = 100;
    public const int TimeZoneMaxLength = 64;
    public const string DefaultTimeZone = "America/Guayaquil";
    public const int MaxFailedLoginAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User()
    {
    }

    private User(Guid id) : base(id)
    {
    }

    public string Username { get; private set; } = null!;

    /// <summary>Usuario en minúsculas: la unicidad no distingue mayúsculas.</summary>
    public string NormalizedUsername { get; private set; } = null!;

    /// <summary>Siempre en minúsculas.</summary>
    public string Email { get; private set; } = null!;

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public UserStatus Status { get; private set; }

    /// <summary>Zona IANA del usuario: define qué es "hoy" para sus registros y estadísticas.</summary>
    public string TimeZoneId { get; private set; } = DefaultTimeZone;

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset? SuspendedAt { get; private set; }

    public int FailedLoginAttempts { get; private set; }

    public DateTimeOffset? LockoutEndsAt { get; private set; }

    /// <summary>Hasta cuándo vio las recomendaciones (en cualquier teléfono): apaga el punto de la campana en todos.</summary>
    public DateTimeOffset? RecommendationsSeenAt { get; private set; }

    /// <summary>Cuándo confirmó su correo con el código enviado. Sin esto no puede iniciar sesión.</summary>
    public DateTimeOffset? EmailVerifiedAt { get; private set; }

    public bool IsActive => Status == UserStatus.Active;

    public bool IsEmailVerified => EmailVerifiedAt is not null;

    public void MarkEmailVerified(DateTimeOffset now)
    {
        if (EmailVerifiedAt is null)
        {
            EmailVerifiedAt = now.ToUniversalTime();
            UpdatedAt = EmailVerifiedAt.Value;
        }
    }

    public void ChangeRole(UserRole role, DateTimeOffset now)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainException("El rol no es válido.");
        }

        Role = role;
        UpdatedAt = now.ToUniversalTime();
    }

    /// <summary>
    /// Nueva contraseña con un código enviado al correo: prueba que el correo es suyo
    /// y quita el bloqueo por intentos fallidos.
    /// </summary>
    public void ResetPassword(string passwordHash, DateTimeOffset now)
    {
        SetPasswordHash(passwordHash);
        FailedLoginAttempts = 0;
        LockoutEndsAt = null;
        MarkEmailVerified(now);
        UpdatedAt = now.ToUniversalTime();
    }

    /// <summary>Solo avanza: un teléfono con la hora atrasada no vuelve a encender el aviso en los demás.</summary>
    public void MarkRecommendationsSeen(DateTimeOffset seenAt, DateTimeOffset now)
    {
        var value = (seenAt > now ? now : seenAt).ToUniversalTime();
        if (RecommendationsSeenAt is null || value > RecommendationsSeenAt)
        {
            RecommendationsSeenAt = value;
        }
    }

    /// <summary>Hora local del usuario (0–23) en ese instante; sirve para no avisar de madrugada.</summary>
    public int LocalHour(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, GetTimeZone()).Hour;

    public static User Register(
        string username,
        string email,
        string firstName,
        string lastName,
        string passwordHash,
        UserRole role,
        string? timeZoneId,
        DateTimeOffset now)
    {
        var user = new User(Guid.CreateVersion7())
        {
            CreatedAt = now.ToUniversalTime(),
            Role = role,
            Status = UserStatus.Active,
        };

        user.Username = Guard.Text(username, "usuario", UsernameMaxLength);
        user.NormalizedUsername = NormalizeUsername(user.Username);
        user.ApplyProfile(firstName, lastName, email, timeZoneId);
        user.SetPasswordHash(passwordHash);
        user.UpdatedAt = user.CreatedAt;
        return user;
    }

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void UpdateProfile(string firstName, string lastName, string email, string? timeZoneId, DateTimeOffset now)
    {
        ApplyProfile(firstName, lastName, email, timeZoneId);
        UpdatedAt = now.ToUniversalTime();
    }

    public void ChangePasswordHash(string passwordHash, DateTimeOffset now)
    {
        SetPasswordHash(passwordHash);
        UpdatedAt = now.ToUniversalTime();
    }

    public void Suspend(DateTimeOffset now)
    {
        if (Status == UserStatus.Suspended)
        {
            throw new DomainException("La cuenta ya está suspendida.");
        }

        Status = UserStatus.Suspended;
        SuspendedAt = now.ToUniversalTime();
        UpdatedAt = SuspendedAt.Value;
    }

    public void Reactivate(DateTimeOffset now)
    {
        if (Status == UserStatus.Active)
        {
            throw new DomainException("La cuenta ya está activa.");
        }

        Status = UserStatus.Active;
        SuspendedAt = null;
        FailedLoginAttempts = 0;
        LockoutEndsAt = null;
        UpdatedAt = now.ToUniversalTime();
    }

    public bool IsLockedOut(DateTimeOffset now) => LockoutEndsAt is not null && LockoutEndsAt > now;

    public void RegisterFailedLogin(DateTimeOffset now)
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            LockoutEndsAt = now.ToUniversalTime().Add(LockoutDuration);
            FailedLoginAttempts = 0;
        }
    }

    public void RegisterSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginAttempts = 0;
        LockoutEndsAt = null;
        LastLoginAt = now.ToUniversalTime();
    }

    /// <summary>Fecha local del usuario para un instante dado.</summary>
    public DateOnly LocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, GetTimeZone()).DateTime);

    public TimeZoneInfo GetTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static bool IsValidTimeZone(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);

    private void ApplyProfile(string firstName, string lastName, string email, string? timeZoneId)
    {
        FirstName = Guard.Text(firstName, "nombre", NameMaxLength);
        LastName = Guard.Text(lastName, "apellido", NameMaxLength);
        Email = NormalizeEmail(Guard.Text(email, "correo", EmailMaxLength));

        var zone = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZone : timeZoneId.Trim();
        if (!IsValidTimeZone(zone))
        {
            throw new DomainException($"La zona horaria '{zone}' no es válida.");
        }

        TimeZoneId = zone;
    }

    private void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("La contraseña es obligatoria.");
        }

        PasswordHash = passwordHash;
    }
}
