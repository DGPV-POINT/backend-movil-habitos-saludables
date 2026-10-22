using HealthyMind.Application.Common;
using HealthyMind.Application.Ports.Out;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HealthyMind.Infrastructure.Persistence;

internal sealed class UnitOfWork(HealthyMindDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dos peticiones simultáneas pasaron la validación previa; la base de datos tiene la última palabra.
            throw new ConflictException("El registro ya existe.");
        }
    }

    public void DiscardChanges() => context.ChangeTracker.Clear();
}
