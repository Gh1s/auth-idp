using Csb.Auth.Idp.Controllers.Auth;
using Csb.Auth.Users;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Ory.Hydra.Client.Api;
using Ory.Hydra.Client.Client;
using Ory.Hydra.Client.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Csb.Auth.Idp.Tests.Controllers.Auth
{
    public class AuthControllerTests : IDisposable
    {
        private readonly TestBed _testBed = new();

        public void Dispose() => _testBed.Dispose();

        [Fact]
        public async Task Login_Get_NoSkip()
        {
            // Setup
            var store = "test";
            var loginChallenge = Guid.NewGuid().ToString();
            var loginRequest = new HydraLoginRequest(
                challenge: loginChallenge,
                requestUrl: "https://localhost",
                requestedAccessTokenAudience: new List<string>(),
                requestedScope: new List<string>(),
                skip: false,
                subject: Guid.NewGuid().ToString(),
                _client: new HydraOAuth2Client
                {
                    Metadata = JObject.Parse($"{{\"store\":\"{store}\"}}")
                }
            );
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock
                .Setup(m => m.GetLoginRequestAsync(loginChallenge, cancellationToken))
                .ReturnsAsync(loginRequest);

            // Act
            var result = await _testBed.Subject.Login(loginChallenge, cancellationToken);

            // Assert
            result.Should()
                .BeOfType<ViewResult>()
                .Which.Model
                .Should().BeEquivalentTo(new LoginViewModel { Challenge = loginChallenge, Store = store });
        }

        [Fact]
        public async Task Login_Get_Skip()
        {
            // Setup
            var store = "test";
            var subject = Guid.NewGuid().ToString();
            var loginChallenge = Guid.NewGuid().ToString();
            var loginRequest = new HydraLoginRequest(
                challenge: loginChallenge,
                requestUrl: "https://localhost",
                requestedAccessTokenAudience: new List<string>(),
                requestedScope: new List<string>(),
                skip: true,
                subject: subject,
                _client: new HydraOAuth2Client
                {
                    Metadata = JObject.Parse($"{{\"store\":\"{store}\"}}")
                }
            );
            var completedRequest = new HydraCompletedRequest("https://localhost/signin-oidc");
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock
                .Setup(m => m.GetLoginRequestAsync(loginChallenge, cancellationToken))
                .ReturnsAsync(loginRequest);
            _testBed.AdminApiMock
                .Setup(m => m
                    .AcceptLoginRequestAsync(
                        loginChallenge,
                        It.Is<HydraAcceptLoginRequest>(p =>
                            p.Subject == subject &&
                            (p.Context as Dictionary<string, object>)[IdpConstants.Users.StoreKey].ToString() == store
                        ),
                        cancellationToken
                    )
                )
                .ReturnsAsync(completedRequest);

            // Act
            var result = await _testBed.Subject.Login(loginChallenge, cancellationToken);

            // Assert
            result.Should()
                .BeOfType<RedirectResult>()
                .Which.Url
                .Should().Be(completedRequest.RedirectTo);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Login_Get_MissingChallenge(string loginChallenge)
        {
            // Setup
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await _testBed.Subject.Login(loginChallenge, cancellationToken);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            result.As<RedirectToActionResult>().ActionName.Should().Be("Error");
            result.As<RedirectToActionResult>().ControllerName.Should().Be("Error");
            result.As<RedirectToActionResult>().RouteValues.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                { "trace_identifier", _testBed.Subject.HttpContext.TraceIdentifier },
                { "error", Resources.Errors.InvalidRequest.Code },
                { "error_description", Resources.Errors.InvalidRequest.Description },
                { "error_hint", Resources.Errors.InvalidRequest.LoginChallengeMissingHint },
                { "error_debug", Resources.Errors.InvalidRequest.LoginChallengeMissingDebug }
            });
        }

        [Fact]
        public async Task Login_Get_InvalidChallenge()
        {
            // Setup
            var loginChallenge = Guid.NewGuid().ToString();
            var loginRequestException = new ApiException(404, "Not found", "Not found");
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock
                .Setup(m => m.GetLoginRequestAsync(loginChallenge, cancellationToken))
                .ThrowsAsync(loginRequestException);

            // Act
            var result = await _testBed.Subject.Login(loginChallenge, cancellationToken);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            result.As<RedirectToActionResult>().ActionName.Should().Be("Error");
            result.As<RedirectToActionResult>().ControllerName.Should().Be("Error");
            result.As<RedirectToActionResult>().RouteValues.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                { "trace_identifier", _testBed.Subject.HttpContext.TraceIdentifier },
                { "error", Resources.Errors.InvalidRequest.Code },
                { "error_description", Resources.Errors.InvalidRequest.Description },
                { "error_hint", Resources.Errors.InvalidRequest.LoginChallengeInvalidHint },
                { "error_debug", Resources.Errors.InvalidRequest.LoginChallengeInvalidDebug(loginRequestException.ErrorCode, loginRequestException.ErrorContent.ToString()) }
            });
        }

        [Fact]
        public async Task Login_Get_InvalidClient()
        {
            // Setup
            var loginChallenge = Guid.NewGuid().ToString();
            var loginRequest = new HydraLoginRequest(
                challenge: loginChallenge,
                requestUrl: "https://localhost",
                requestedAccessTokenAudience: new List<string>(),
                requestedScope: new List<string>(),
                subject: Guid.NewGuid().ToString(),
                _client: new HydraOAuth2Client
                {
                    Metadata = JObject.Parse("{}")
                }
            );
            var completedRequest = new HydraCompletedRequest("https://localhost/login-rejected");
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock
                .Setup(m => m.GetLoginRequestAsync(loginChallenge, cancellationToken))
                .ReturnsAsync(loginRequest);
            _testBed.AdminApiMock
                .Setup(m => m
                    .RejectLoginRequestAsync(
                        loginChallenge,
                        It.Is<HydraRejectRequest>(p =>
                            p.StatusCode == StatusCodes.Status500InternalServerError &&
                            p.Error == Resources.Errors.InvalidClient.Code &&
                            p.ErrorDescription == Resources.Errors.InvalidClient.Description &&
                            p.ErrorHint == Resources.Errors.InvalidClient.MissingUserStoreHint &&
                            p.ErrorDebug == Resources.Errors.InvalidClient.MissingUserStoreDebug
                        ),
                        cancellationToken
                    )
                )
                .ReturnsAsync(completedRequest);

            // Act
            var result = await _testBed.Subject.Login(loginChallenge, cancellationToken);

            // Assert
            result.Should()
                .BeOfType<RedirectResult>()
                .Which.Url
                .Should().Be(completedRequest.RedirectTo);
        }

        [Fact]
        public async Task Login_Post_ValidCredentials()
        {
            // Setup
            var model = new LoginViewModel
            {
                Challenge = Guid.NewGuid().ToString(),
                Store = "test",
                Username = "user",
                Password = "password"
            };
            var authResponse = new AuthResponse
            {
                Succeeded = true,
                Subject = Guid.NewGuid().ToString()
            };
            var completedRequest = new HydraCompletedRequest("https://localhost/signin-oidc");
            var cancellationToken = CancellationToken.None;
            _testBed.UserClientMock
                .Setup(m => m
                    .AuthenticateAsync(
                        It.Is<AuthRequest>(p =>
                            p.Username == model.Username &&
                            p.Password == model.Password
                        ),
                        It.IsAny<Metadata>(),
                        It.IsAny<DateTime?>(),
                        cancellationToken
                    )
                )
                .Returns(
                    new AsyncUnaryCall<AuthResponse>(
                        Task.FromResult(authResponse),
                        Task.FromResult(new Metadata()),
                        () => Status.DefaultSuccess,
                        () => new Metadata(),
                        () => { }
                    )
                );
            _testBed.AdminApiMock
                .Setup(m => m
                    .AcceptLoginRequestAsync(
                        model.Challenge,
                        It.Is<HydraAcceptLoginRequest>(p =>
                            p.Subject == authResponse.Subject &&
                            p.Remember == model.RememberMe &&
                            p.RememberFor == _testBed.Options.RememberForSeconds &&
                            (p.Context as Dictionary<string, object>)[IdpConstants.Users.StoreKey].Equals(model.Store) &&
                            (p.Context as Dictionary<string, object>)[IdpConstants.Users.RememberKey].Equals(model.RememberMe)
                        ),
                        cancellationToken
                    )
                )
                .ReturnsAsync(completedRequest);

            // Act
            var result = await _testBed.Subject.Login(model, cancellationToken);

            // Assert
            result.Should()
                .BeOfType<RedirectResult>()
                .Which.Url
                .Should().Be(completedRequest.RedirectTo);
        }

        [Fact]
        public async Task Login_Post_UnsuccessfulGrpcResponse()
        {
            // Setup
            var model = new LoginViewModel
            {
                Challenge = Guid.NewGuid().ToString(),
                Store = "test",
                Username = "user",
                Password = "password"
            };
            var cancellationToken = CancellationToken.None;
            var authResponse = new AuthResponse
            {
                Succeeded = false,
                Error = 1
            };
            var completedRequest = new HydraCompletedRequest("https://localhost/signin-oidc");
            _testBed.UserClientMock
                .Setup(m => m
                    .AuthenticateAsync(
                        It.Is<AuthRequest>(p =>
                            p.Username == model.Username &&
                            p.Password == model.Password
                        ),
                        It.IsAny<Metadata>(),
                        It.IsAny<DateTime?>(),
                        cancellationToken
                    )
                )
                .Returns(
                    new AsyncUnaryCall<AuthResponse>(
                        Task.FromResult(authResponse),
                        Task.FromResult(new Metadata()),
                        () => Status.DefaultSuccess,
                        () => new Metadata(),
                        () => { }
                    )
                );
            var localizedStringName = $"AuthenticationFailure.{model.Store}.{authResponse.Error}";
            var localizedStringValue = "An error has occured";
            _testBed.LocalizerMock
                .SetupGet(m => m[localizedStringName])
                .Returns(new LocalizedString(localizedStringName, localizedStringValue));

            // Act
            var result = await _testBed.Subject.Login(model, cancellationToken);

            // Assert
            result.Should()
                .BeOfType<ViewResult>()
                .Which.Model
                .Should().Be(model);
            _testBed.Subject.ModelState[""].Errors.Single().ErrorMessage.Should().Be(localizedStringValue);
        }

        [Fact]
        public async Task Login_Post_GrcpException()
        {
            // Setup
            var model = new LoginViewModel
            {
                Challenge = Guid.NewGuid().ToString(),
                Store = "test",
                Username = "user",
                Password = "password"
            };
            var cancellationToken = CancellationToken.None;
            _testBed.UserClientMock
                .Setup(m => m
                    .AuthenticateAsync(
                        It.Is<AuthRequest>(p =>
                            p.Username == model.Username &&
                            p.Password == model.Password
                        ),
                        It.IsAny<Metadata>(),
                        It.IsAny<DateTime?>(),
                        cancellationToken
                    )
                )
                .Throws<InvalidOperationException>();
            var localizedStringName = "AuthenticationFailure";
            var localizedStringValue = "An error has occured while interacting with the underlying user store";
            _testBed.LocalizerMock
                .SetupGet(m => m[localizedStringName])
                .Returns(new LocalizedString(localizedStringName, localizedStringValue));

            // Act
            var result = await _testBed.Subject.Login(model, cancellationToken);

            // Assert
            result.Should()
                .BeOfType<ViewResult>()
                .Which.Model
                .Should().Be(model);
            _testBed.Subject.ModelState[""].Errors.Single().ErrorMessage.Should().Be(localizedStringValue);
        }

        [Fact]
        public async Task Login_Post_InvalidModelState()
        {
            // Setup
            var model = new LoginViewModel();
            var cancellationToken = CancellationToken.None;
            _testBed.Subject.ModelState.AddModelError("", "");

            // Act
            var result = await _testBed.Subject.Login(model, cancellationToken);

            // Assert
            result.Should()
                .BeOfType<ViewResult>()
                .Which.Model
                .Should().Be(model);
        }

        [Fact]
        public async Task Consent_Get_Valid()
        {
            // Setup
            var store = "test";
            var consentChallenge = Guid.NewGuid().ToString();
            var consentRequest = new HydraConsentRequest(
                challenge: consentChallenge,
                _client: new HydraOAuth2Client
                {
                    Metadata = JObject.Parse($"{{\"{IdpConstants.Users.StoreKey}\":\"{store}\",\"{IdpConstants.Users.RememberKey}\":true}}")
                },
                requestUrl: "https://localhost",
                requestedAccessTokenAudience: new List<string>(),
                requestedScope: new List<string>()
                {
                    "scope1"
                },
                subject: Guid.NewGuid().ToString()
            );
            var claimsResponse = new ClaimsResponse
            {
                Succeeded = true,
                Claims =
                {
                    { "claim1", "value1" },
                    { "claim2", "value2" }
                }
            };
            var completedRequest = new HydraCompletedRequest("https://localhost/signin-oidc");
            var cancellationToken = CancellationToken.None;
            var utcNow = DateTimeOffset.UtcNow;
            _testBed.SystemClockMock.SetupGet(m => m.UtcNow).Returns(utcNow);
            _testBed.UserClientMock
                .Setup(m => m
                    .FindClaimsAsync(
                        It.Is<ClaimsRequest>(p =>
                            p.Identifier == consentRequest.Subject &&
                            p.IdentifierType == IdentifierType.Subject &&
                            p.Claims.SequenceEqual(new[] { "claim1", "claim2" })
                        ),
                        It.IsAny<Metadata>(),
                        It.IsAny<DateTime?>(),
                        cancellationToken
                    )
                )
                .Returns(
                    new AsyncUnaryCall<ClaimsResponse>(
                        Task.FromResult(claimsResponse),
                        Task.FromResult(new Metadata()),
                        () => Status.DefaultSuccess,
                        () => new Metadata(),
                        () => { }
                    )
                );
            _testBed.AdminApiMock
               .Setup(m => m.GetConsentRequestAsync(consentChallenge, cancellationToken))
               .ReturnsAsync(consentRequest);
            _testBed.AdminApiMock
                .Setup(m => m
                    .AcceptConsentRequestAsync(
                        consentChallenge,
                        It.Is<HydraAcceptConsentRequest>(p =>
                            p.GrantScope == consentRequest.RequestedScope &&
                            p.GrantAccessTokenAudience == consentRequest.RequestedAccessTokenAudience &&
                            p.HandledAt == utcNow &&
                            p.Remember &&
                            p.RememberFor == _testBed.Options.RememberForSeconds &&
                            p.Session.AccessToken == p.Session.IdToken &&
                            (p.Session.AccessToken as Dictionary<string, string>)["claim1"] == "value1" &&
                            (p.Session.AccessToken as Dictionary<string, string>)["claim2"] == "value2"
                        ),
                        cancellationToken
                    )
                )
                .ReturnsAsync(completedRequest);

            // Act
            var result = await _testBed.Subject.Consent(consentChallenge, cancellationToken);

            // Assert
            result.Should()
                .BeOfType<RedirectResult>()
                .Which.Url
                .Should().Be(completedRequest.RedirectTo);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Consent_Get_MissingChallenge(string consentChallenge)
        {
            // Act
            var result = await _testBed.Subject.Consent(consentChallenge, CancellationToken.None);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            result.As<RedirectToActionResult>().ActionName.Should().Be("Error");
            result.As<RedirectToActionResult>().ControllerName.Should().Be("Error");
            result.As<RedirectToActionResult>().RouteValues.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                { "trace_identifier", _testBed.Subject.HttpContext.TraceIdentifier },
                { "error", Resources.Errors.InvalidRequest.Code },
                { "error_description", Resources.Errors.InvalidRequest.Description },
                { "error_hint", Resources.Errors.InvalidRequest.ConsentChallengeMissingHint },
                { "error_debug", Resources.Errors.InvalidRequest.ConsentChallengeMissingDebug }
            });
        }

        [Fact]
        public async Task Consent_Get_InvalidChallenge()
        {
            // Setup
            var consentChallenge = Guid.NewGuid().ToString();
            var consentRequestException = new ApiException(404, "Not found", "Not found");
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock
               .Setup(m => m.GetConsentRequestAsync(consentChallenge, cancellationToken))
               .ThrowsAsync(consentRequestException);

            // Act
            var result = await _testBed.Subject.Consent(consentChallenge, cancellationToken);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            result.As<RedirectToActionResult>().ActionName.Should().Be("Error");
            result.As<RedirectToActionResult>().ControllerName.Should().Be("Error");
            result.As<RedirectToActionResult>().RouteValues.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                { "trace_identifier", _testBed.Subject.HttpContext.TraceIdentifier },
                { "error", Resources.Errors.InvalidRequest.Code },
                { "error_description", Resources.Errors.InvalidRequest.Description },
                { "error_hint", Resources.Errors.InvalidRequest.ConsentChallengeInvalidHint },
                { "error_debug", Resources.Errors.InvalidRequest.ConsentChallengeInvalidDebug(consentRequestException.ErrorCode, consentRequestException.ErrorContent.ToString()) }
            });
        }

        [Fact]
        public async Task Consent_Get_InvalidClient()
        {
            // Setup
            var consentChallenge = Guid.NewGuid().ToString();
            var consentRequest = new HydraConsentRequest(
                challenge: consentChallenge,
                _client: new HydraOAuth2Client
                {
                    Metadata = JObject.Parse("{}")
                },
                requestUrl: "https://localhost",
                requestedAccessTokenAudience: new List<string>(),
                requestedScope: new List<string>()
                {
                    "scope1"
                },
                subject: Guid.NewGuid().ToString()
            );
            var completedRequest = new HydraCompletedRequest("https://localhost/login-rejected");
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock
               .Setup(m => m.GetConsentRequestAsync(consentChallenge, cancellationToken))
               .ReturnsAsync(consentRequest);
            _testBed.AdminApiMock
                .Setup(m => m
                    .RejectConsentRequestAsync(
                        consentChallenge,
                        It.Is<HydraRejectRequest>(p =>
                            p.StatusCode == StatusCodes.Status500InternalServerError &&
                            p.Error == Resources.Errors.InvalidClient.Code &&
                            p.ErrorDescription == Resources.Errors.InvalidClient.Description &&
                            p.ErrorHint == Resources.Errors.InvalidClient.MissingUserStoreDebug &&
                            p.ErrorDebug == Resources.Errors.InvalidClient.MissingUserStoreHint
                        ),
                        cancellationToken
                    )
                )
                .ReturnsAsync(completedRequest);

            // Act
            var result = await _testBed.Subject.Consent(consentChallenge, cancellationToken);

            // Assert
            result.Should()
                .BeOfType<RedirectResult>()
                .Which.Url
                .Should().Be(completedRequest.RedirectTo);
        }

        [Fact]
        public async Task Consent_Get_UnsuccessfulGrpcResponse()
        {
            // Setup
            var store = "test";
            var consentChallenge = Guid.NewGuid().ToString();
            var consentRequest = new HydraConsentRequest(
                challenge: consentChallenge,
                _client: new HydraOAuth2Client
                {
                    Metadata = JObject.Parse($"{{\"{IdpConstants.Users.StoreKey}\":\"{store}\",\"{IdpConstants.Users.RememberKey}\":true}}")
                },
                requestUrl: "https://localhost",
                requestedAccessTokenAudience: new List<string>(),
                requestedScope: new List<string>()
                {
                    "scope1"
                },
                subject: Guid.NewGuid().ToString()
            );
            var completedRequest = new HydraCompletedRequest("https://localhost/login-rejected");
            var claimsResponse = new ClaimsResponse
            {
                Succeeded = false,
                Error = 1
            };
            var cancellationToken = CancellationToken.None;
            _testBed.UserClientMock
                .Setup(m => m
                    .FindClaimsAsync(
                        It.Is<ClaimsRequest>(p =>
                            p.Identifier == consentRequest.Subject &&
                            p.IdentifierType == IdentifierType.Subject &&
                            p.Claims.SequenceEqual(new[] { "claim1", "claim2" })
                        ),
                        It.IsAny<Metadata>(),
                        It.IsAny<DateTime?>(),
                        cancellationToken
                    )
                )
                .Returns(
                    new AsyncUnaryCall<ClaimsResponse>(
                        Task.FromResult(claimsResponse),
                        Task.FromResult(new Metadata()),
                        () => Status.DefaultSuccess,
                        () => new Metadata(),
                        () => { }
                    )
                );
            _testBed.AdminApiMock
               .Setup(m => m.GetConsentRequestAsync(consentChallenge, cancellationToken))
               .ReturnsAsync(consentRequest);
            _testBed.AdminApiMock
                .Setup(m => m
                    .RejectConsentRequestAsync(
                        consentChallenge,
                        It.Is<HydraRejectRequest>(p =>
                            p.StatusCode == StatusCodes.Status500InternalServerError &&
                            p.Error == Resources.Errors.UserStoreInteractionFailure.Code &&
                            p.ErrorDescription == Resources.Errors.UserStoreInteractionFailure.Description &&
                            p.ErrorHint == Resources.Errors.UserStoreInteractionFailure.ClaimsFetchFailedHint &&
                            p.ErrorDebug == Resources.Errors.UserStoreInteractionFailure.ClaimsFetchFailedDebug(store, 1)
                        ),
                        cancellationToken
                    )
                )
                .ReturnsAsync(completedRequest);

            // Act
            var result = await _testBed.Subject.Consent(consentChallenge, cancellationToken);

            // Assert
            result.Should()
                .BeOfType<RedirectResult>()
                .Which.Url
                .Should().Be(completedRequest.RedirectTo);
        }

        [Fact]
        public async Task Logout_Get_Valid()
        {
            // Setup
            var logoutChallenge = Guid.NewGuid().ToString();
            var logoutRequest = new HydraLogoutRequest("/oauth2/sessions/logout?post_logout_redirect_uri=https%3A%2F%2Flocalhost%2Fsignout-callback-oidc");
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock
                .Setup(m => m.GetLogoutRequestAsync(logoutChallenge, cancellationToken))
                .ReturnsAsync(logoutRequest);

            // Act
            var result = await _testBed.Subject.Logout(logoutChallenge, cancellationToken);

            // Assert
            result.Should()
                .BeOfType<ViewResult>()
                .Which.Model
                .Should().BeEquivalentTo(new LogoutViewModel { Challenge = logoutChallenge, RedictUrl = "https://localhost:443/" });
        }

        [Fact]
        public async Task Logout_Get_Global()
        {
            // Setup
            var wellKnown = new HydraWellKnown(
                issuer: "",
                jwksUri: "",
                tokenEndpoint: "",
                userinfoEndpoint: "",
                authorizationEndpoint: "",
                endSessionEndpoint: "https://localhost/oauth2/sessions/logout",
                idTokenSigningAlgValuesSupported: new List<string>(),
                responseTypesSupported: new List<string>(),
                responseModesSupported: new List<string>(),
                scopesSupported: new List<string>(),
                subjectTypesSupported: new List<string>(),
                tokenEndpointAuthMethodsSupported: new List<string>()
            );
            var cancellationToken = CancellationToken.None;
            _testBed.PublicApiMock
                .Setup(m => m.DiscoverOpenIDConfigurationAsync(cancellationToken))
                .ReturnsAsync(wellKnown);

            // Act
            var result = await _testBed.Subject.Logout(logoutChallenge: null, cancellationToken);

            // Assert
            result.Should().BeOfType<RedirectResult>()
                .Which.Url
                .Should().Be(wellKnown.EndSessionEndpoint);
        }

        [Fact]
        public async Task Logout_Get_InvalidChallenge()
        {
            // Setup
            var logoutChallenge = Guid.NewGuid().ToString();
            var logoutRequestException = new ApiException(404, "Not found", "Not found");
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock
                .Setup(m => m.GetLogoutRequestAsync(logoutChallenge, cancellationToken))
                .ThrowsAsync(logoutRequestException);

            // Act
            var result = await _testBed.Subject.Logout(logoutChallenge, cancellationToken);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            result.As<RedirectToActionResult>().ActionName.Should().Be("Error");
            result.As<RedirectToActionResult>().ControllerName.Should().Be("Error");
            result.As<RedirectToActionResult>().RouteValues.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                { "trace_identifier", _testBed.Subject.HttpContext.TraceIdentifier },
                { "error", Resources.Errors.InvalidRequest.Code },
                { "error_description", Resources.Errors.InvalidRequest.Description },
                { "error_hint", Resources.Errors.InvalidRequest.LogoutChallengeInvalidHint },
                { "error_debug", Resources.Errors.InvalidRequest.LogoutChallengeInvalidDebug(logoutRequestException.ErrorCode, logoutRequestException.ErrorContent.ToString()) }
            });
        }

        [Fact]
        public async Task Logout_Post_Yes()
        {
            // Setup
            var model = new LogoutViewModel
            {
                Challenge = Guid.NewGuid().ToString(),
                Action = "yes"
            };
            var completedRequest = new HydraCompletedRequest("https://localhost/signout-callback-oidc");
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock
                .Setup(m => m.AcceptLogoutRequestAsync(model.Challenge, cancellationToken))
                .ReturnsAsync(completedRequest);

            // Act
            var result = await _testBed.Subject.Logout(model, cancellationToken);

            // Assert
            result.Should().BeOfType<RedirectResult>()
                .Which.Url
                .Should().Be(completedRequest.RedirectTo);
        }

        [Fact]
        public async Task Logout_Post_YesCompleteFailed()
        {
            // Setup
            var model = new LogoutViewModel
            {
                Challenge = Guid.NewGuid().ToString(),
                Action = "yes"
            };
            var logoutRequestException = new ApiException(500, "Internal Server Error", "Internal Server Error");
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock
                .Setup(m => m.AcceptLogoutRequestAsync(model.Challenge, cancellationToken))
                .ThrowsAsync(logoutRequestException);

            // Act
            var result = await _testBed.Subject.Logout(model, cancellationToken);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            result.As<RedirectToActionResult>().ActionName.Should().Be("Error");
            result.As<RedirectToActionResult>().ControllerName.Should().Be("Error");
            result.As<RedirectToActionResult>().RouteValues.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                { "trace_identifier", _testBed.Subject.HttpContext.TraceIdentifier },
                { "error", Resources.Errors.AdminApiInteractionFailure.Code },
                { "error_description", Resources.Errors.AdminApiInteractionFailure.Description },
                { "error_hint", Resources.Errors.AdminApiInteractionFailure.CompleteLogoutFailedHint },
                { "error_debug", Resources.Errors.AdminApiInteractionFailure.CompleteLogoutFailedDebug(logoutRequestException.ErrorCode, logoutRequestException.ErrorContent.ToString()) }
            });
        }

        [Fact]
        public async Task Logout_Post_No()
        {
            // Setup
            var model = new LogoutViewModel
            {
                Challenge = Guid.NewGuid().ToString(),
                Action = "no"
            };
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock.Setup(m => m.RejectLogoutRequestAsync(model.Challenge, null, cancellationToken));

            // Act
            var result = await _testBed.Subject.Logout(model, cancellationToken);

            // Assert
            result.Should().BeOfType<ViewResult>()
                .Which.ViewName
                .Should().Be("LogoutRejected");
        }

        [Fact]
        public async Task Logout_Post_NoWithRedirect()
        {
            // Setup
            var model = new LogoutViewModel
            {
                Challenge = Guid.NewGuid().ToString(),
                Action = "no",
                RedictUrl = "https://localhost"
            };
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock.Setup(m => m.RejectLogoutRequestAsync(model.Challenge, null, cancellationToken));

            // Act
            var result = await _testBed.Subject.Logout(model, cancellationToken);

            // Assert
            result.Should().BeOfType<RedirectResult>()
                .Which.Url
                .Should().Be(model.RedictUrl);
        }

        [Fact]
        public async Task Logout_Post_NoCompleteFailed()
        {
            // Setup
            var model = new LogoutViewModel
            {
                Challenge = Guid.NewGuid().ToString(),
                Action = "no"
            };
            var logoutRequestException = new ApiException(500, "Internal Server Error", "Internal Server Error");
            var cancellationToken = CancellationToken.None;
            _testBed.AdminApiMock
                .Setup(m => m.RejectLogoutRequestAsync(model.Challenge, null, cancellationToken))
                .ThrowsAsync(logoutRequestException);

            // Act
            var result = await _testBed.Subject.Logout(model, cancellationToken);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            result.As<RedirectToActionResult>().ActionName.Should().Be("Error");
            result.As<RedirectToActionResult>().ControllerName.Should().Be("Error");
            result.As<RedirectToActionResult>().RouteValues.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                { "trace_identifier", _testBed.Subject.HttpContext.TraceIdentifier },
                { "error", Resources.Errors.AdminApiInteractionFailure.Code },
                { "error_description", Resources.Errors.AdminApiInteractionFailure.Description },
                { "error_hint", Resources.Errors.AdminApiInteractionFailure.RejectLogoutFailedHint },
                { "error_debug", Resources.Errors.AdminApiInteractionFailure.RejectLogoutFailedDebug(logoutRequestException.ErrorCode, logoutRequestException.ErrorContent.ToString()) }
            });
        }

        [Fact]
        public void LoggedOut_Get()
        {
            // Act
            var result = _testBed.Subject.LoggedOut();

            // Assert
            result.Should().BeOfType<ViewResult>()
                .Which.ViewName
                .Should().Be("LoggedOut");
        }

        private class TestBed : IDisposable
        {
            private readonly GrpcChannel _channel;

            public Mock<User.UserClient> UserClientMock { get; }

            public Mock<IUserClientProvider> UserClientProviderMock { get; }

            public Mock<IAdminApiAsync> AdminApiMock { get; }

            public Mock<IPublicApiAsync> PublicApiMock { get; }

            public Mock<ISystemClock> SystemClockMock { get; }

            public Mock<IStringLocalizer<AuthController>> LocalizerMock { get; }

            public AuthOptions Options { get; }

            public AuthController Subject { get; }

            public TestBed()
            {
                _channel = GrpcChannel.ForAddress("https://localhost", new GrpcChannelOptions
                {
                    HttpClient = new HttpClient(),
                    DisposeHttpClient = true
                });
                UserClientMock = new Mock<User.UserClient>(_channel);
                UserClientProviderMock = new Mock<IUserClientProvider>();
                UserClientProviderMock.Setup(m => m.CreateClient(It.IsAny<string>())).Returns(UserClientMock.Object);
                AdminApiMock = new Mock<IAdminApiAsync>();
                PublicApiMock = new Mock<IPublicApiAsync>();
                SystemClockMock = new Mock<ISystemClock>();
                SystemClockMock.SetupGet(m => m.UtcNow).Returns(DateTimeOffset.UtcNow);
                LocalizerMock = new Mock<IStringLocalizer<AuthController>>();
                LocalizerMock.SetupGet(m => m[It.IsAny<string>()]).Returns(new LocalizedString("", ""));
                Options = new AuthOptions
                {
                    RememberForSeconds = 60,
                    Scopes = new Dictionary<string, IEnumerable<string>>
                    {
                        { "scope1", new[] { "claim1", "claim2" } },
                        { "scope2", new[] { "claim3" } }
                    },
                    ShowDebug = true
                };
                var optionsMonitorMock = new Mock<IOptionsMonitor<AuthOptions>>();
                optionsMonitorMock.SetupGet(m => m.CurrentValue).Returns(Options);
                Subject = new AuthController(
                    UserClientProviderMock.Object,
                    AdminApiMock.Object,
                    PublicApiMock.Object,
                    SystemClockMock.Object,
                    optionsMonitorMock.Object,
                    LocalizerMock.Object,
                    new NullLogger<AuthController>()
                )
                {
                    ControllerContext = new ControllerContext
                    {
                        HttpContext = new DefaultHttpContext
                        {
                            TraceIdentifier = Guid.NewGuid().ToString()
                        }
                    }
                };
            }

            public void Dispose() => _channel.Dispose();
        }
    }
}
