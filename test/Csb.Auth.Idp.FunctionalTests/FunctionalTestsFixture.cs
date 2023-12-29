using Csb.Auth.Users;
using Grpc.Core;
using Grpc.Net.Client;
using IdentityModel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using Ory.Hydra.Client.Api;
using Ory.Hydra.Client.Client;
using Ory.Hydra.Client.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Csb.Auth.Idp.FunctionalTests
{
    public class FunctionalTestsFixture<TStartup> : IDisposable where TStartup : class
    {
        public IHost IdpHost { get; }

        public IHost AppHost { get; }

        public FunctionalTestsFixture()
        {
            IdpHost = SetupIdpHost();
            AppHost = SetupAppHost();
        }

        public void Dispose()
        {
            AppHost.Dispose();
            IdpHost.Dispose();
        }

        private IHost SetupIdpHost()
        {
            var host = Program
                .CreateHostBuilder(Array.Empty<string>())
                .UseContentRoot("../../../../../src/Csb.Auth.Idp")
                .ConfigureHostConfiguration(builder =>
                {
                    builder.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "Environment", "Development" },
                        // The URL is the same as in the /src/Csb.Auth.Idp/Properties/launchSettings.json
                        { "Urls", "https://localhost:5000" }
                    });
                })
                .ConfigureAppConfiguration(builder =>
                {
                    builder.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "DataProtection:CertificatePath", "../../../../../docker/certs/data-protection.pfx" },
                        { "Users:Clients:ldap:CertificatePath", "../../../../../docker/certs/tls-grpc-ldap.crt" },
                        { "Users:Clients:accounts:CertificatePath", "../../../../../docker/certs/tls-grpc-accounts.crt" },
                        { "Users:Clients:test:Address", "https://localhost:5700" }
                    });
                })
                .ConfigureServices(services =>
                {
                    var userClientOptions = new UserClientOptions
                    {
                        // This is a false address, no service is listening on that endpoint.
                        Address = "https://localhost:5700"
                    };
                    var userClientChannel = GrpcChannel.ForAddress(
                        userClientOptions.Address,
                        new GrpcChannelOptions
                        {
                            HttpClient = new HttpClient(),
                            DisposeHttpClient = true
                        }
                    );
                    // We assume that the user store is valid, hence we mock it.
                    var userClientMock = new Mock<User.UserClient>(userClientChannel);
                    userClientMock.SetupGet(m => m.Options).Returns(userClientOptions);
                    userClientMock
                        .Setup(m => m
                            .AuthenticateAsync(
                                It.IsAny<AuthRequest>(),
                                It.IsAny<Metadata>(),
                                It.IsAny<DateTime?>(),
                                It.IsAny<CancellationToken>()
                            )
                        )
                        .Returns<AuthRequest, Metadata, DateTime?, CancellationToken>((request, metadata, deadline, cancellationToken) =>
                        {
                            var response = new AuthResponse();

                            if (string.Equals(request.Username, FunctionalTestsConstants.TestUser.Username, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(request.Password, FunctionalTestsConstants.TestUser.Password, StringComparison.Ordinal))
                            {
                                response.Succeeded = true;
                                response.Subject = FunctionalTestsConstants.TestUser.Claims[JwtClaimTypes.Subject];
                            }
                            else
                            {
                                response.Succeeded = false;
                                response.Error = 1;
                            }

                            return WrapAsyncUnaryCall(response);
                        });
                    userClientMock
                        .Setup(m => m
                            .FindClaimsAsync(
                                It.IsAny<ClaimsRequest>(),
                                It.IsAny<Metadata>(),
                                It.IsAny<DateTime?>(),
                                It.IsAny<CancellationToken>()
                            )
                        )
                        .Returns<ClaimsRequest, Metadata, DateTime?, CancellationToken>((request, metadata, deadline, cancellationToken) =>
                        {
                            var response = new ClaimsResponse();

                            if ((request.IdentifierType == IdentifierType.Subject && request.Identifier == FunctionalTestsConstants.TestUser.Claims[JwtClaimTypes.Subject]) ||
                                (request.IdentifierType == IdentifierType.UserName && string.Equals(request.Identifier, FunctionalTestsConstants.TestUser.Username, StringComparison.OrdinalIgnoreCase)))
                            {
                                response.Succeeded = true;
                                foreach (var claimType in request.Claims)
                                {
                                    if (FunctionalTestsConstants.TestUser.Claims.TryGetValue(claimType, out var claimValue))
                                    {
                                        response.Claims[claimType] = claimValue;
                                    }
                                }
                            }
                            else
                            {
                                response.Succeeded = false;
                                response.Error = 1;
                            }

                            return WrapAsyncUnaryCall(response);
                        });
                    static AsyncUnaryCall<TResponse> WrapAsyncUnaryCall<TResponse>(TResponse response) =>
                        new AsyncUnaryCall<TResponse>(
                            Task.FromResult(response),
                            Task.FromResult(new Metadata()),
                            () => Status.DefaultSuccess,
                            () => new Metadata(),
                            () => { }
                        );

                    // We mock the UserClient factory to return or mock UserClient.
                    var userClientFactoryMock = new Mock<IUserClientFactory>();
                    userClientFactoryMock.Setup(m => m.CreateClient(It.IsAny<GrpcChannel>())).Returns(userClientMock.Object);

                    services.AddSingleton(userClientChannel);
                    services.AddSingleton(userClientFactoryMock.Object);
                })
                .Build();
            host.Start();

            using (var scope = host.Services.CreateScope())
            {
                var adminApi = scope.ServiceProvider.GetRequiredService<AdminApi>();
                var clientsJson = File.ReadAllText("clients.json");
                var clients = JsonConvert.DeserializeObject<IEnumerable<HydraOAuth2Client>>(clientsJson);
                foreach (var client in clients)
                {
                    try
                    {
                        adminApi.CreateOAuth2Client(client);
                    }
                    catch (ApiException e)
                    {
                        if (e.ErrorCode == 409)
                        {
                            // The client already exists in Hydra. We can ignore this error.
                            continue;
                        }
                        throw;
                    }
                }
            }

            return host;
        }

        private IHost SetupAppHost()
        {
            var host = Host
                .CreateDefaultBuilder(Array.Empty<string>())
                .UseContentRoot("../../../../../samples/Csb.Auth.Samples.AuthorizationCodeMvc")
                .ConfigureHostConfiguration(builder =>
                {
                    builder.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "Environment", "Development" },
                        // The URL is the same as in the /samples/Csb.Auth.Samples.AuthorizationCodeMvc/Properties/launchSettings.json
                        { "Urls", "https://localhost:5900" }
                    });
                })
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<TStartup>())
                .Build();
            host.Start();
            return host;
        }

        private WebDriver CreateChromeDriver()
        {
            var options = new ChromeOptions
            {
                AcceptInsecureCertificates = true
            };
            options.AddArgument("lang=en-US");
            options.AddArgument("headless");
            return new ChromeDriver(options);
        }

        private WebDriver CreateEdgeDriver()
        {
            var options = new EdgeOptions
            {
                AcceptInsecureCertificates = true
            };
            options.AddArgument("lang=en-US");
            options.AddArgument("headless");
            return new EdgeDriver(options);
        }

        private WebDriver CreateFirefoxDriver()
        {
            var options = new FirefoxOptions
            {
                AcceptInsecureCertificates = true
            };
            options.SetPreference("intl.accept_languages", "en-US");
            options.AddArgument("--headless");
            return new FirefoxDriver(options);
        }

        public WebDriver CreateWebDriver(string name)
        {
            var driver = name switch
            {
                FunctionalTestsConstants.WebDrivers.Chrome => CreateChromeDriver(),
                FunctionalTestsConstants.WebDrivers.Edge => CreateEdgeDriver(),
                FunctionalTestsConstants.WebDrivers.Firefox => CreateFirefoxDriver(),
                _ => throw new InvalidOperationException("Unsupported driver."),
            };
            return driver;
        }
    }
}
