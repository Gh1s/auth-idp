using IdentityModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Csb.Auth.Idp.FunctionalTests
{
    public static class FunctionalTestsConstants
    {
        public static class TestUser
        {
            public const string Store = "test";
            public const string Username = "user";
            public const string Password = "password";
            public static readonly IDictionary<string, string> Claims =
                new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>
                    {
                        { JwtClaimTypes.Subject, "061fa261-bd89-47cd-b972-8f79cc7de875" },
                        { JwtClaimTypes.PreferredUserName, "test" },
                        { JwtClaimTypes.GivenName, "User" },
                        { JwtClaimTypes.FamilyName, "TEST" },
                        { JwtClaimTypes.Name, "TEST User" },
                        { JwtClaimTypes.Picture, "https://localhost/test.jpg" },
                        { JwtClaimTypes.Email, "u.test@csb.nc" },
                        { JwtClaimTypes.EmailVerified, "true" },
                        { JwtClaimTypes.PhoneNumber, "12.34.56" },
                        { JwtClaimTypes.PhoneNumberVerified, "true" }
                    }
                );
        }

        public static class WebDrivers
        {
            public const string Chrome = "Chrome";
            public const string Edge = "Edge";
            public const string Firefox = "Firefox";
        }

        public static class UI
        {
            public const string LoginPageUrl = "https://localhost:5000/login";
            public const string LogoutPageUrl = "https://localhost:5000/logout";
            public const string LoggedOutPageUrl = "https://localhost:5000/loggedout";
        }
    }
}
