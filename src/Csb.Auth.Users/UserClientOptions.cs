namespace Csb.Auth.Users
{
    /// <summary>
    /// Provides configuration options for a <see cref="User.UserClient"/> instance.
    /// </summary>
    public class UserClientOptions
    {
        /// <summary>
        /// The gRPC address.
        /// </summary>
        public string Address { get; set; }
    }
}
