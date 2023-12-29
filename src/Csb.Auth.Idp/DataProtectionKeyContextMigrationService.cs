using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Csb.Auth.Idp
{
    public class DataProtectionKeyContextMigrationService : IHostedService
    {
        private readonly IServiceScopeFactory _factory;

        public DataProtectionKeyContextMigrationService(IServiceScopeFactory factory)
        {
            _factory = factory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _factory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DataProtectionKeyContext>().Database.MigrateAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
