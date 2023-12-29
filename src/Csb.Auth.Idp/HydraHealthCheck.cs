using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Csb.Auth.Idp
{
    public class HydraHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _factory;
        private readonly IConfiguration _configuration;

        public HydraHealthCheck(IHttpClientFactory factory, IConfiguration configuration)
        {
            _factory = factory;
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var hydraAdminTask = _factory.CreateClient("hydra_admin").GetAsync(_configuration.GetValue<string>("Hydra:AdminApi:HealthEndpoint"), cancellationToken);
            var hydraPublicTask = _factory.CreateClient("hydra_public").GetAsync(_configuration.GetValue<string>("Hydra:PublicApi:HealthEndpoint"), cancellationToken);
            await Task.WhenAll(new[] { hydraAdminTask, hydraPublicTask });

            if (hydraAdminTask.Result.IsSuccessStatusCode && hydraPublicTask.Result.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy();
            }

            return HealthCheckResult.Unhealthy("Hydra services are unhealthy");
        }
    }
}
