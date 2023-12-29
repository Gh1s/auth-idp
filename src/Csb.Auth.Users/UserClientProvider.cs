using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Csb.Auth.Users
{
    /// <summary>
    /// Provides an implementation of <see cref="IUserClientProvider"/>.
    /// </summary>
    public sealed class UserClientProvider : IDisposable, IUserClientProvider
    {
        private readonly List<GrpcChannel> _channels = new();

        private readonly IUserClientFactory _userClientFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<UsersOptions> _usersOptionsMonitor;
        private readonly ILoggerFactory _loggerFactory;

        private UsersOptions UsersOptions => _usersOptionsMonitor.CurrentValue;

        /// <summary>
        /// Creates a new instance of <see cref="UserClientProvider"/>.
        /// </summary>
        /// <param name="userClientFactory">The user client factory.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="optionsMonitor">The service to monitor options changes.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        public UserClientProvider(
            IUserClientFactory userClientFactory,
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<UsersOptions> optionsMonitor,
            ILoggerFactory loggerFactory)
        {
            _userClientFactory = userClientFactory;
            _httpClientFactory = httpClientFactory;
            _usersOptionsMonitor = optionsMonitor;
            _loggerFactory = loggerFactory;
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            _channels.ForEach(c => c.Dispose());
            _channels.Clear();
        }

        /// <inheritdoc cref="IUserClientProvider.CreateClient(string)"/>
        public User.UserClient CreateClient(string store)
        {
            if (store is null)
            {
                throw new ArgumentNullException(nameof(store));
            }
            if (!UsersOptions.Clients.TryGetValue(store, out var storeOptions))
            {
                throw new InvalidOperationException("Unsupported user store.");
            }

            var httpClient = _httpClientFactory.CreateClient(store);
            httpClient.BaseAddress = new Uri(storeOptions.Address);

            var grpcChannel = GrpcChannel.ForAddress(storeOptions.Address, new GrpcChannelOptions
            {
                HttpClient = httpClient,
                DisposeHttpClient = false,
                LoggerFactory = _loggerFactory
            });
            _channels.Add(grpcChannel);

            var client = _userClientFactory.CreateClient(grpcChannel);
            client.Options = storeOptions;
            return client;
        }
    }
}
