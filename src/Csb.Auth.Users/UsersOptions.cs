using System.Collections.Generic;

namespace Csb.Auth.Users
{
    /// <summary>
    /// Provides options for the users features.
    /// </summary>
    public class UsersOptions
    {
        /// <summary>
        /// The clients by type that can be used to interact with their store.
        /// </summary>
        public Dictionary<string, UserClientOptions> Clients { get; set; }
    }
}
