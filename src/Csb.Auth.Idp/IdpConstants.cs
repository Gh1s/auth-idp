namespace Csb.Auth.Idp
{
    /// <summary>
    /// Provides constants.
    /// </summary>
    public static class IdpConstants
    {
        /// <summary>
        /// Provides constants to users related features.
        /// </summary>
        public static class Users
        {
            /// <summary>
            /// Defines the key used to represent the store that has authenticated a user in the context of an authentication, a consent grant or in a <see cref="Ory.Hydra.Client.Model.HydraOAuth2Client"/>'s metadata.
            /// </summary>
            public const string StoreKey = "store";

            /// <summary>
            /// Defines the key used to represent the remember intention in the context of an authentication or a consent grant.
            /// </summary>
            public const string RememberKey = "remember";
        }
    }
}
