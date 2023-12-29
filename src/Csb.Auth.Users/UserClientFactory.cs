using Grpc.Net.Client;

namespace Csb.Auth.Users
{
    /// <summary>
    /// Provides an implementation of <see cref="IUserClientFactory/>.
    /// </summary>
    public class UserClientFactory : IUserClientFactory
    {
        /// <inheritdoc cref="IUserClientFactory.CreateClient(GrpcChannel)"/>
        public User.UserClient CreateClient(GrpcChannel grpcChannel) => new User.UserClient(grpcChannel);
    }
}
