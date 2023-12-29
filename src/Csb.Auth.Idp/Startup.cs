using Csb.Auth.Idp.Controllers.Auth;
using Csb.Auth.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Ory.Hydra.Client.Api;
using Ory.Hydra.Client.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace Csb.Auth.Idp
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }

        public IWebHostEnvironment Environment { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Forwarded headers
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
                options.ForwardedHeaders = ForwardedHeaders.All;
                options.ForwardLimit = null;
            });

            // Data protection.
            var key = new X509Certificate2(
                Configuration.GetValue<string>("DataProtection:CertificatePath"),
                Configuration.GetValue<string>("DataProtection:CertificatePassword")
            );
            var dataProtectionBuilder = services.AddDataProtection()
                .ProtectKeysWithCertificate(key)
                .UnprotectKeysWithAnyCertificate(key);
            if (Configuration.GetValue<string>("DataProtection:StorageMode") == "FileSystem")
            {
                dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(Configuration.GetValue<string>("DataProtection:StoragePath")));
            }
            else if (Configuration.GetValue<string>("DataProtection:StorageMode") == "DbContext")
            {
                services.AddHostedService<DataProtectionKeyContextMigrationService>();
                services.AddDbContext<DataProtectionKeyContext>(options => options.UseNpgsql(Configuration.GetConnectionString("DataProtectionKeyContext")));
                dataProtectionBuilder.PersistKeysToDbContext<DataProtectionKeyContext>();
            }

            // Mvc.
            var mvcBuilder = services
                .AddControllersWithViews()
                .AddMvcLocalization(options => options.ResourcesPath = "Resources");
            
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            /*services.AddCors(options =>
            {
                var section = Configuration.GetSection("Cors");
                options.AddDefaultPolicy(p => p
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
                //.WithOrigins(section.GetSection("AllowedOrigins").Get<string[]>())
                //.WithMethods(section.GetSection("AllowedMethods").Get<string[]>()));
                Console.WriteLine("Cors options loaded");
            });*/
            
#if DEBUG
            mvcBuilder.AddRazorRuntimeCompilation();
#endif

            // Configures authentication
            services.TryAddSingleton<ISystemClock, SystemClock>();
            services.Configure<AuthOptions>(Configuration.GetSection("Auth"));

            // Configures Hydra client.
            services.AddScoped(p =>
            {
                var key = Configuration.GetValue<string>("Hydra:AdminApi:Key");
                var config = new Configuration
                {
                    BasePath = Configuration.GetValue<string>("Hydra:AdminApi:Url")
                };
                if (!string.IsNullOrWhiteSpace(key))
                {
                    config.DefaultHeaders = new Dictionary<string, string>
                    {
                        {
                            HeaderNames.Authorization,
                            $"ApiKey {key}"
                        }
                    };
                }
                return new AdminApi(config);
            });
            services.AddScoped<IAdminApi>(p => p.GetRequiredService<AdminApi>());
            services.AddScoped<IAdminApiAsync>(p => p.GetRequiredService<AdminApi>());
            services.AddScoped(p =>
            {
                var config = new Configuration
                {
                    BasePath = Configuration.GetValue<string>("Hydra:PublicApi:Url")
                };
                return new PublicApi(config);
            });
            services.AddScoped<IPublicApi>(p => p.GetRequiredService<PublicApi>());
            services.AddScoped<IPublicApiAsync>(p => p.GetRequiredService<PublicApi>());

            // Configures user stores.
            foreach (var section in Configuration.GetSection("Users:Clients").GetChildren())
            {
                services
                    .AddHttpClient(section.Key, client =>
                    {
                        client.BaseAddress = new Uri(section.GetValue<string>("Address"));
                    })
                    .ConfigureHttpMessageHandlerBuilder(builder =>
                    {
                        if (builder.PrimaryHandler is HttpClientHandler handler)
                        {
                            var serialNumber = X509Certificate
                                .CreateFromCertFile(section.GetValue<string>("CertificatePath"))
                                .GetSerialNumberString();
                            handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
                                cert.SerialNumber.Equals(serialNumber, StringComparison.OrdinalIgnoreCase);
                        }
                    });
            }
            services.AddHttpClient();

            services.AddTransient<IUserClientFactory, UserClientFactory>();
            services.AddScoped<IUserClientProvider, UserClientProvider>();
            services.Configure<UsersOptions>(Configuration.GetSection("Users"));

            // Health checks.
            services
                .AddHttpClient("hydra_admin", client =>
                {
                    client.BaseAddress = new Uri(Configuration.GetValue<string>("Hydra:AdminApi:Url"));
                })
                .ConfigureHttpMessageHandlerBuilder(builder =>
                {
                    if (Configuration.GetValue<bool>("Hydra:AdminApi:BypassCertificateValidation") && builder.PrimaryHandler is HttpClientHandler clientHandler)
                    {
                        // Disable invalid TLS certificate validation.
                        clientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    }
                });
            services
                .AddHttpClient("hydra_public", client => client.BaseAddress = new Uri(Configuration.GetValue<string>("Hydra:PublicApi:Url")))
                .ConfigureHttpMessageHandlerBuilder(builder =>
                {
                    if (Configuration.GetValue<bool>("Hydra:PublicApi:BypassCertificateValidation") && builder.PrimaryHandler is HttpClientHandler clientHandler)
                    {
                        // Disable invalid TLS certificate validation.
                        clientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    }
                });
            services.AddHealthChecks().AddCheck<HydraHealthCheck>("hydra");
        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseForwardedHeaders();
            }

            app.UseRequestLocalization(options =>
            {
                var cultures = Configuration.GetSection("Localization:SupportedCultures").Get<string[]>();
                options.AddSupportedCultures(cultures);
                options.AddSupportedUICultures(cultures);
                options.SetDefaultCulture(Configuration.GetSection("Localization:DefaultCulture").Get<string>());
                options.FallBackToParentCultures = true;
                options.FallBackToParentUICultures = true;
            });

            app.UseStaticFiles();

            app.UseRouting();

            //app.UseAuthentication();
            //app.UseAuthorization();
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health");
                endpoints.MapControllers();
            });
        }
    }
}
