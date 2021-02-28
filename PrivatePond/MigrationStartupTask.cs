using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrivatePond.Data.EF;

namespace PrivatePond
{
    public class MigrationStartupTask : IStartupTask
    {
        private readonly IDbContextFactory<PrivatePondDbContext> _privatePondDbContext;
        private readonly ILogger<MigrationStartupTask> _logger;

        public MigrationStartupTask(
            IDbContextFactory<PrivatePondDbContext> privatePondDbContext, ILogger<MigrationStartupTask> logger)
        {
            _privatePondDbContext = privatePondDbContext;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation($"Migrating database to latest version");
                await using var context = _privatePondDbContext.CreateDbContext();
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
                _logger.LogInformation(pendingMigrations.Any()
                    ? $"Running migrations: {string.Join(", ", pendingMigrations)}"
                    : $"Database already at latest version");
                await context.Database.MigrateAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error on the MigrationStartupTask");
                throw;
            }
        }
    }
}