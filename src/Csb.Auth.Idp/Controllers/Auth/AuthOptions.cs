using System.Collections.Generic;
using System.Linq;

namespace Csb.Auth.Idp.Controllers.Auth
{
    /// <summary>
    /// Provides configuration for the authentication features.
    /// </summary>
    public class AuthOptions
    {
        /// <summary>
        /// Defines if debug informations should be shown when an error occurs.
        /// </summary>
        public bool ShowDebug { get; set; }

        /// <summary>
        /// Duration in seconds to remember the connexion when requested by the user.
        /// </summary>
        public int RememberForSeconds { get; set; }

        /// <summary>
        /// Scopes to claims mapping.
        /// </summary>
        public Dictionary<string, IEnumerable<string>> Scopes { get; set; }

        /// <summary>
        /// Gets the claims associated to the requested scopes.
        /// </summary>
        /// <param name="scopes">The requested scopes.</param>
        /// <returns>The claims list.</returns>
        public IEnumerable<string> GetClaims(IEnumerable<string> scopes) =>
            Scopes.Where(s => scopes.Contains(s.Key)).SelectMany(s => s.Value).ToList();
    }
}
