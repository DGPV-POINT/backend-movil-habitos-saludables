namespace HealthyMind.Application.Common;

// Los mensajes de estas excepciones llegan tal cual al cliente; la API decide el código HTTP.

/// <summary>El recurso no existe o no pertenece al usuario (no se distingue a propósito).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>El recurso ya existe (nombre o fecha duplicados, usuario o correo tomados).</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Credenciales o sesión inválidas: el cliente debe volver a autenticarse.</summary>
public sealed class AuthenticationFailedException(string message) : Exception(message);

/// <summary>Se sabe quién es, pero no puede hacerlo (cuenta suspendida, acción no permitida).</summary>
public sealed class ForbiddenException(string message) : Exception(message);
