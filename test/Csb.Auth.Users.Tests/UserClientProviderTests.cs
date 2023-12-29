using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using Xunit;

namespace Csb.Auth.Users.Tests
{
    public class UserClientProviderTests : IDisposable
    {
        private readonly TestBed _testBed = new();

        public void Dispose() => _testBed.Dispose();

        [Fact]
        public void CreateClient_ValidStore()
        {
            // Setup
            var store = "test";

            // Act
            var client = _testBed.Subject.CreateClient(store);

            // Assert
            client.Should().NotBeNull();
            client.Options.Address.Should().Be(_testBed.Options.Clients[store].Address);
        }

        [Fact]
        public void CreateClient_NullStore()
        {
            // Act & Assert
            _testBed.Subject.Invoking(s => s.CreateClient(null)).Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void CreateClient_UnsupportedStore()
        {
            // Act & Assert
            _testBed.Subject.Invoking(s => s.CreateClient("unsupported")).Should().Throw<InvalidOperationException>();
        }

        private class TestBed : IDisposable
        {
            private readonly List<HttpClient> _httpClients = new List<HttpClient>();

            public Mock<IHttpClientFactory> HttpClientFactoryMock { get; }

            public UsersOptions Options { get; }

            public UserClientProvider Subject { get; }

            public TestBed()
            {
                HttpClientFactoryMock = new Mock<IHttpClientFactory>();
                HttpClientFactoryMock
                    .Setup(m => m.CreateClient(It.IsAny<string>()))
                    .Returns(() =>
                    {
                        var httpClient = new HttpClient();
                        _httpClients.Add(httpClient);
                        return httpClient;
                    });
                Options = new UsersOptions
                {
                    Clients = new Dictionary<string, UserClientOptions>
                    {
                        {
                            "test",
                            new UserClientOptions
                            {
                                Address = "https://localhost"
                            }
                        }
                    }
                };
                var optionsMonitorMock = new Mock<IOptionsMonitor<UsersOptions>>();
                optionsMonitorMock.SetupGet(m => m.CurrentValue).Returns(Options);
                Subject = new UserClientProvider(
                    new UserClientFactory(),
                    HttpClientFactoryMock.Object,
                    optionsMonitorMock.Object,
                    new Mock<ILoggerFactory>().Object
                );
            }

            public void Dispose() => _httpClients.ForEach(c => c.Dispose());
        }
    }
}
