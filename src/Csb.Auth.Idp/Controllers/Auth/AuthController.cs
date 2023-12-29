using Csb.Auth.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Ory.Hydra.Client.Api;
using Ory.Hydra.Client.Client;
using Ory.Hydra.Client.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Csb.Auth.Idp.Controllers.Auth
{
    public class AuthController : Controller
    {
        private readonly IUserClientProvider _clientProvider;
        private readonly IAdminApiAsync _adminApi;
        private readonly IPublicApiAsync _publicApi;
        private readonly ISystemClock _systemClock;
        private readonly IOptionsMonitor<AuthOptions> _authOptionsMonitor;
        private readonly IStringLocalizer<AuthController> _localizer;
        private readonly ILogger<AuthController> _logger;

        private AuthOptions AuthOptions => _authOptionsMonitor.CurrentValue;

        public AuthController(
            IUserClientProvider clientProvider,
            IAdminApiAsync adminApi,
            IPublicApiAsync publicApi,
            ISystemClock systemClock,
            IOptionsMonitor<AuthOptions> authOptions,
            IStringLocalizer<AuthController> localizer,
            ILogger<AuthController> logger)
        {
            _clientProvider = clientProvider;
            _adminApi = adminApi;
            _publicApi = publicApi;
            _systemClock = systemClock;
            _localizer = localizer;
            _authOptionsMonitor = authOptions;
            _logger = logger;
        }

        [HttpGet("login")]
        public async Task<IActionResult> Login([FromQuery(Name = "login_challenge")] string loginChallenge, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(loginChallenge))
            {
                _logger.LogWarning("Login challenge is null or empty.");
                return RedirectToError(
                    Resources.Errors.InvalidRequest.Code,
                    Resources.Errors.InvalidRequest.Description,
                    Resources.Errors.InvalidRequest.LoginChallengeMissingHint,
                    Resources.Errors.InvalidRequest.LoginChallengeMissingDebug
                );
            }

            HydraLoginRequest loginRequest;

            try
            {
                // This call may throw a 404 error if the login challenge is invalid.
                _logger.LogDebug("Retrieving the login request from Hydra.");
                loginRequest = await _adminApi.GetLoginRequestAsync(loginChallenge, cancellationToken);
                _logger.LogDebug("Login request found.");
            }
            catch (ApiException e)
            {
                _logger.LogError(e, "An error has occured while retrieving the login request from Hydra.");
                return RedirectToError(
                    Resources.Errors.InvalidRequest.Code,
                    Resources.Errors.InvalidRequest.Description,
                    Resources.Errors.InvalidRequest.LoginChallengeInvalidHint,
                    Resources.Errors.InvalidRequest.LoginChallengeInvalidDebug(e.ErrorCode, e.ErrorContent.ToString())
                );
            }

            var clientMetadata = (loginRequest._Client.Metadata as JObject).ToObject<Dictionary<string, object>>();
            if (clientMetadata.TryGetValue(IdpConstants.Users.StoreKey, out var store))
            {
                if (loginRequest.Skip)
                {
                    _logger.LogDebug("Hydra has indicated that showing the user the login form is unnecessary. Completing the login request.");
                    var completedRequest = await _adminApi.AcceptLoginRequestAsync(
                        loginChallenge,
                        new HydraAcceptLoginRequest(
                            subject: loginRequest.Subject,
                            context: new Dictionary<string, object>
                            {
                                { IdpConstants.Users.StoreKey, store }
                            }
                        ),
                        cancellationToken
                    );
                    _logger.LogDebug("Login request completed. Redirecting the user to the URL provided by Hydra.");
                    _logger.LogTrace("Redirect URL: {0}", completedRequest.RedirectTo);
                    return Redirect(completedRequest.RedirectTo);
                }
                else
                {
                    _logger.LogDebug("Showing the user the login form.");
                    return View("Login", new LoginViewModel { Challenge = loginChallenge, Store = store.ToString() });
                }
            }

            _logger.LogWarning("The client doesn't have a store defined, rejecting the login request.");
            // At this point, we know that the login request exists.
            // The probability for the rejection to fail is nearly inexistant.
            var rejectRequest = await _adminApi.RejectLoginRequestAsync(
                loginChallenge,
                CreateRejectRequest(
                    StatusCodes.Status500InternalServerError,
                    Resources.Errors.InvalidClient.Code,
                    Resources.Errors.InvalidClient.Description,
                    Resources.Errors.InvalidClient.MissingUserStoreHint,
                    Resources.Errors.InvalidClient.MissingUserStoreDebug
                ),
                cancellationToken
            );
            _logger.LogDebug("Logout request rejected. Redirecting the user to the URL provided by Hydra.");
            _logger.LogTrace("Redirect URL: {0}", rejectRequest.RedirectTo);
            return Redirect(rejectRequest.RedirectTo);
        }

        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
        {
            if (ModelState.IsValid)
            {
                _logger.LogDebug("Creating the store client.");
                var client = _clientProvider.CreateClient(model.Store);

                _logger.LogDebug("Authenticating the user with the {0} store.", model.Store);
                var authRequest = new AuthRequest
                {
                    Username = model.Username,
                    Password = model.Password
                };

                try
                {
                    var authResult = await client.AuthenticateAsync(authRequest, cancellationToken: cancellationToken);
                    if (authResult.Succeeded)
                    {
                        _logger.LogDebug("Authentication succeeded. Completing the login request.");
                        var completedRequest = await _adminApi.AcceptLoginRequestAsync(
                            model.Challenge,
                            new HydraAcceptLoginRequest(
                                subject: authResult.Subject,
                                remember: model.RememberMe,
                                rememberFor: AuthOptions.RememberForSeconds,
                                context: new Dictionary<string, object>
                                {
                                    { IdpConstants.Users.StoreKey, model.Store },
                                    { IdpConstants.Users.RememberKey, model.RememberMe }
                                }
                            ),
                            cancellationToken
                        );
                        _logger.LogDebug("Login request completed. Redirecting the user to the URL provided by Hydra.");
                        _logger.LogTrace("Redirect URL: {0}", completedRequest.RedirectTo);
                        return Redirect(completedRequest.RedirectTo);
                    }

                    _logger.LogDebug("Authentication failed with error {0}.", authResult.Error);
                    ModelState.AddModelError("", _localizer[$"AuthenticationFailure.{model.Store}.{authResult.Error}"]);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An error has occured while authenticating the user with the underlying user store.");
                    ModelState.AddModelError("", _localizer[$"AuthenticationFailure"]);
                }
            }

            _logger.LogDebug("Showing the user the login form.");
            return View("Login", model);
        }

        // TODO: We might control who can access this endpoint.
        [HttpGet("consent")]
        public async Task<IActionResult> Consent([FromQuery(Name = "consent_challenge")] string consentChallenge, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(consentChallenge))
            {
                _logger.LogWarning("Consent challenge is null or empty.");
                return RedirectToError(
                    Resources.Errors.InvalidRequest.Code,
                    Resources.Errors.InvalidRequest.Description,
                    Resources.Errors.InvalidRequest.ConsentChallengeMissingHint,
                    Resources.Errors.InvalidRequest.ConsentChallengeMissingDebug
                );
            }

            HydraConsentRequest consentRequest;
            HydraRejectRequest rejectRequest;

            try
            {
                // This call may throw a 404 error if the consent challenge is invalid.
                _logger.LogDebug("Retrieving the consent request from Hydra.");
                consentRequest = await _adminApi.GetConsentRequestAsync(consentChallenge, cancellationToken);
                _logger.LogDebug("Consent request found.");
            }
            catch (ApiException e)
            {
                _logger.LogError(e, "An error has occured while retrieving the consent request.");
                return RedirectToError(
                    Resources.Errors.InvalidRequest.Code,
                    Resources.Errors.InvalidRequest.Description,
                    Resources.Errors.InvalidRequest.ConsentChallengeInvalidHint,
                    Resources.Errors.InvalidRequest.ConsentChallengeInvalidDebug(e.ErrorCode, e.ErrorContent.ToString())
                );
            }

            var clientMetadata = (consentRequest._Client.Metadata as JObject).ToObject<Dictionary<string, object>>();
            if (clientMetadata.TryGetValue(IdpConstants.Users.StoreKey, out var store))
            {
                _logger.LogDebug("Creating the store client.");
                var client = _clientProvider.CreateClient(store.ToString());

                _logger.LogDebug("Fetching claims from the store {0}.", store);
                var claimsRequest = new ClaimsRequest
                {
                    Identifier = consentRequest.Subject,
                    IdentifierType = IdentifierType.Subject,
                    Claims =
                    {
                        AuthOptions.GetClaims(consentRequest.RequestedScope)
                    }
                };
                var claimsResponse = await client.FindClaimsAsync(claimsRequest, cancellationToken: cancellationToken);
                if (claimsResponse.Succeeded)
                {
                    _logger.LogDebug("Claims fetch succeeded.");
                    var claims = claimsResponse.Claims.ToDictionary(c => c.Key, c => c.Value);

                    _logger.LogDebug("Accepting the consent request.");
                    var consentResponse = await _adminApi.AcceptConsentRequestAsync(
                        consentChallenge,
                        new HydraAcceptConsentRequest
                        {
                            GrantScope = consentRequest.RequestedScope,
                            GrantAccessTokenAudience = consentRequest.RequestedAccessTokenAudience,
                            HandledAt = _systemClock.UtcNow.UtcDateTime,
                            Remember = consentRequest.Skip || (clientMetadata.TryGetValue(IdpConstants.Users.RememberKey, out var remember) && Convert.ToBoolean(remember)),
                            RememberFor = AuthOptions.RememberForSeconds,
                            Session = new HydraConsentRequestSession(accessToken: claims, idToken: claims)
                        },
                        cancellationToken
                    );
                    _logger.LogDebug("Consent request accepted. Redirecting the user to the URL provided by Hydra.");
                    _logger.LogTrace("Redirect URL: {0}", consentResponse.RedirectTo);
                    return Redirect(consentResponse.RedirectTo);
                }
                else
                {
                    _logger.LogWarning("Claims fetch failed with error {0}.", claimsResponse.Error);
                    rejectRequest = CreateRejectRequest(
                        StatusCodes.Status500InternalServerError,
                        Resources.Errors.UserStoreInteractionFailure.Code,
                        Resources.Errors.UserStoreInteractionFailure.Description,
                        Resources.Errors.UserStoreInteractionFailure.ClaimsFetchFailedHint,
                        Resources.Errors.UserStoreInteractionFailure.ClaimsFetchFailedDebug(store.ToString(), claimsResponse.Error)
                    );
                }
            }
            else
            {
                _logger.LogWarning("The client doesn't have a store defined.");
                rejectRequest = CreateRejectRequest(
                    StatusCodes.Status500InternalServerError,
                    Resources.Errors.InvalidClient.Code,
                    Resources.Errors.InvalidClient.Description,
                    Resources.Errors.InvalidClient.MissingUserStoreDebug,
                    Resources.Errors.InvalidClient.MissingUserStoreHint
                );
            }

            _logger.LogWarning("The consent request has failed, rejecting it.");
            var completedRequest = await _adminApi.RejectConsentRequestAsync(consentChallenge, rejectRequest, cancellationToken);
            _logger.LogDebug("Consent request rejected. Redirecting the user to the URL provided by Hydra.");
            _logger.LogTrace("Redirect URL: {0}", completedRequest.RedirectTo);
            return Redirect(completedRequest.RedirectTo);
        }

        // TODO: We might control who can access this endpoint.
        [HttpGet("logout")]
        public async Task<IActionResult> Logout([FromQuery(Name = "logout_challenge")] string logoutChallenge, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(logoutChallenge))
            {
                // If no logout challenge is present, a global logout is initialized.
                var wellKnown = await _publicApi.DiscoverOpenIDConfigurationAsync(cancellationToken);
                return Redirect(wellKnown.EndSessionEndpoint);
            }

            try
            {
                // This call may throw a 404 error if the logout challenge is invalid.
                _logger.LogDebug("Retrieving the logout request {0}", logoutChallenge);
                var logoutRequest = await _adminApi.GetLogoutRequestAsync(logoutChallenge, cancellationToken);
                var redirectUrl = ParseRedirectUrl(logoutRequest.RequestUrl);
                _logger.LogDebug("Logout request found.");
                return View("Logout", new LogoutViewModel { Challenge = logoutChallenge, RedictUrl = redirectUrl });
            }
            catch (ApiException e)
            {
                _logger.LogError(e, "The logout challenge is invalid.");
                return RedirectToError(
                    Resources.Errors.InvalidRequest.Code,
                    Resources.Errors.InvalidRequest.Description,
                    Resources.Errors.InvalidRequest.LogoutChallengeInvalidHint,
                    Resources.Errors.InvalidRequest.LogoutChallengeInvalidDebug(e.ErrorCode, e.ErrorContent.ToString())
                );
            }
        }

        // TODO: We might control who can access this endpoint.
        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(LogoutViewModel model, CancellationToken cancellationToken)
        {
            string errorHint;
            string errorDebug;

            if (model.Action == "yes")
            {
                try
                {
                    // This call may throw a 404 error if the logout challenge is invalid.
                    _logger.LogDebug("Completing the logout request {0}.", model.Challenge);
                    var completedRequest = await _adminApi.AcceptLogoutRequestAsync(model.Challenge, cancellationToken);
                    _logger.LogDebug("Logout request completed. Redirecting the user to the URL provided by Hydra.");
                    _logger.LogTrace("Redirect URL: {0}", completedRequest.RedirectTo);
                    return Redirect(completedRequest.RedirectTo);
                }
                catch (ApiException e)
                {
                    _logger.LogError(e, "An error has occured while completing the logout request.");
                    errorHint = Resources.Errors.AdminApiInteractionFailure.CompleteLogoutFailedHint;
                    errorDebug = Resources.Errors.AdminApiInteractionFailure.CompleteLogoutFailedDebug(e.ErrorCode, e.ErrorContent.ToString());
                }
            }
            else
            {
                try
                {
                    // This call may throw a 404 error if the logout challenge is invalid.
                    _logger.LogDebug("Rejecting the logout request {0}.", model.Challenge);
                    await _adminApi.RejectLogoutRequestAsync(model.Challenge, cancellationToken: cancellationToken);
                    _logger.LogDebug("Logout request rejected.");

                    if (model.RedictUrl != null)
                    {
                        return Redirect(model.RedictUrl);
                    }
                    return View("LogoutRejected");
                }
                catch (ApiException e)
                {
                    _logger.LogError(e, "An error has occured while rejecting the logout request.");
                    errorHint = Resources.Errors.AdminApiInteractionFailure.RejectLogoutFailedHint;
                    errorDebug = Resources.Errors.AdminApiInteractionFailure.RejectLogoutFailedDebug(e.ErrorCode, e.ErrorContent.ToString());
                }
            }

            return RedirectToError(
                Resources.Errors.AdminApiInteractionFailure.Code,
                Resources.Errors.AdminApiInteractionFailure.Description,
                errorHint,
                errorDebug
            );
        }

        [HttpGet("loggedout")]
        public IActionResult LoggedOut() => View("LoggedOut");

        private HydraRejectRequest CreateRejectRequest(
            int statusCode,
            string error,
            string errorDescription,
            string errorHint,
            string errorDebug)
        {
            var rejectRequest = new HydraRejectRequest
            {
                StatusCode = statusCode,
                Error = error,
                ErrorDescription = errorDescription,
                ErrorHint = errorHint
            };
            if (AuthOptions.ShowDebug)
            {
                rejectRequest.ErrorDebug = errorDebug;
            }
            return rejectRequest;
        }

        private IActionResult RedirectToError(
            string error,
            string errorDescription,
            string errorHint,
            string errorDebug)
        {
            var routeValues = new Dictionary<string, string>
            {
                { "trace_identifier", HttpContext.TraceIdentifier },
                { "error", error },
                { "error_description", errorDescription },
                { "error_hint", errorHint }
            };
            if (AuthOptions.ShowDebug)
            {
                routeValues.Add("error_debug", errorDebug);
            }
            return RedirectToAction("Error", "Error", routeValues);
        }

        private string ParseRedirectUrl(string requestUrl)
        {
            if (requestUrl != null)
            {
                var queryIndex = requestUrl.IndexOf("?");
                if (queryIndex >= 0)
                {
                    var queryParameters = QueryHelpers.ParseQuery(requestUrl[queryIndex..]);
                    if (queryParameters.TryGetValue("post_logout_redirect_uri", out var values) && values.Count > 0)
                    {
                        var postLogoutUriBuilder = new UriBuilder(values.First())
                        {
                            Path = "/",
                            Query = null
                        };
                        return postLogoutUriBuilder.ToString();
                    }
                }
            }
            return null;
        }
    }
}
