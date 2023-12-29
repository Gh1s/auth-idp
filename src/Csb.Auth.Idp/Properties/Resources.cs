using System.Globalization;
using System.Resources;

namespace Csb.Auth.Idp
{
    public static class Resources
    {
        private static readonly ResourceManager ResourcesManager = new ResourceManager("Csb.Auth.Idp.Resources", typeof(Resources).Assembly);

        public static class Errors
        {
            public static class InvalidRequest
            {
                public static string Code => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(Code)}");

                public static string Description => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(Description)}");

                public static string LoginChallengeMissingHint => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(LoginChallengeMissingHint)}");

                public static string LoginChallengeMissingDebug => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(LoginChallengeMissingDebug)}");

                public static string LoginChallengeInvalidHint => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(LoginChallengeInvalidHint)}");

                public static string LoginChallengeInvalidDebug(int errorCode, string errorContent) =>
                    string.Format(
                        CultureInfo.InvariantCulture,
                        ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(LoginChallengeInvalidDebug)}"),
                        errorCode,
                        errorContent
                    );

                public static string ConsentChallengeMissingHint => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(ConsentChallengeMissingHint)}");

                public static string ConsentChallengeMissingDebug => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(ConsentChallengeMissingDebug)}");

                public static string ConsentChallengeInvalidHint => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(ConsentChallengeInvalidHint)}");

                public static string ConsentChallengeInvalidDebug(int errorCode, string errorContent) =>
                    string.Format(
                        CultureInfo.InvariantCulture,
                        ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(ConsentChallengeInvalidDebug)}"),
                        errorCode,
                        errorContent
                    );

                public static string LogoutChallengeInvalidHint => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(LogoutChallengeInvalidHint)}");

                public static string LogoutChallengeInvalidDebug(int errorCode, string errorContent) =>
                    string.Format(
                        CultureInfo.InvariantCulture,
                        ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidRequest)}.{nameof(LogoutChallengeInvalidDebug)}"),
                        errorCode,
                        errorContent
                    );
            }

            public static class InvalidClient
            {
                public static string Code => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidClient)}.{nameof(Code)}");

                public static string Description => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidClient)}.{nameof(Description)}");

                public static string MissingUserStoreHint => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidClient)}.{nameof(MissingUserStoreHint)}");

                public static string MissingUserStoreDebug => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(InvalidClient)}.{nameof(MissingUserStoreDebug)}");
            }

            public static class AdminApiInteractionFailure
            {
                public static string Code => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(AdminApiInteractionFailure)}.{nameof(Code)}");

                public static string Description => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(AdminApiInteractionFailure)}.{nameof(Description)}");

                public static string CompleteLogoutFailedHint => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(AdminApiInteractionFailure)}.{nameof(CompleteLogoutFailedHint)}");

                public static string CompleteLogoutFailedDebug(int errorCode, string errorContent) =>
                    string.Format(
                        CultureInfo.InvariantCulture,
                        ResourcesManager.GetString($"{nameof(Errors)}.{nameof(AdminApiInteractionFailure)}.{nameof(CompleteLogoutFailedDebug)}"),
                        errorCode,
                        errorContent
                    );

                public static string RejectLogoutFailedHint => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(AdminApiInteractionFailure)}.{nameof(RejectLogoutFailedHint)}");

                public static string RejectLogoutFailedDebug(int errorCode, string errorContent) =>
                    string.Format(
                        CultureInfo.InvariantCulture,
                        ResourcesManager.GetString($"{nameof(Errors)}.{nameof(AdminApiInteractionFailure)}.{nameof(RejectLogoutFailedDebug)}"),
                        errorCode,
                        errorContent
                    );
            }

            public static class UserStoreInteractionFailure
            {
                public static string Code => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(UserStoreInteractionFailure)}.{nameof(Code)}");

                public static string Description => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(UserStoreInteractionFailure)}.{nameof(Description)}");

                public static string ClaimsFetchFailedHint => ResourcesManager.GetString($"{nameof(Errors)}.{nameof(UserStoreInteractionFailure)}.{nameof(ClaimsFetchFailedHint)}");

                public static string ClaimsFetchFailedDebug(string userStore, int errorCode) =>
                    string.Format(
                        CultureInfo.InvariantCulture,
                        ResourcesManager.GetString($"{nameof(Errors)}.{nameof(UserStoreInteractionFailure)}.{nameof(ClaimsFetchFailedDebug)}"),
                        userStore,
                        errorCode
                    );
            }
        }
    }
}
