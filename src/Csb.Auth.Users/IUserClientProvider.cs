namespace Csb.Auth.Users
{
    /// <summary>
    /// Provides an abstraction to bootstrap <see cref="User.UserClient"/>.
    /// </summary>
    public interface IUserClientProvider
    {
        /// <summary>
        /// Creates a new instance of <see cref="User.UserClient"/> with the specified <paramref name="store"/>.
        /// </summary>
        /// <param name="store">The user store.</param>
        /// <returns>An instance of <see cref="User.UserClient"/>.</returns>
        User.UserClient CreateClient(string store);
    }
}