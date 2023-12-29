using Grpc.Net.Client;

namespace Csb.Auth.Users
{
    /// <summary>
    /// Provides an abstraction to create <see cref="User.UserClient"/> instances to facilitate testing.
    /// </summary>
    public interface IUserClientFactory
    {
        /// <summary>
        /// Creates a new <see cref="User.UserClient"/> with the underlying gRPC channel.
        /// </summary>
        /// <param name="grpcChannel">The gRPC channel.</param>
        /// <returns>The <see cref="User.UserClient"/> instance.</returns>
        User.UserClient CreateClient(GrpcChannel grpcChannel);
    }
}
