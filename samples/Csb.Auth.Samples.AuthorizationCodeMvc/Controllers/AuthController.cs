using IdentityModel;
using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace Csb.Auth.Samples.AuthorizationCodeMvc.Controllers
{
    public class AuthController : Controller
    {
        private readonly SessionStore _sessionStore;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<OpenIdConnectOptions> _oidcOptionsMonitor;

        private OpenIdConnectOptions OidcOptions => _oidcOptionsMonitor.CurrentValue;

        public AuthController(
            SessionStore sessionStore,
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<OpenIdConnectOptions> oidcOptionsMonitor)
        {
            _sessionStore = sessionStore;
            _httpClientFactory = httpClientFactory;
            _oidcOptionsMonitor = oidcOptionsMonitor;
        }

        [Authorize]
        [HttpPost("/logout")]
        [ValidateAntiForgeryToken]
        public async Task Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
        }

        [Authorize]
        [Route("/frontchannel-logout")]
        public async Task<IActionResult> FrontchannelLogout(string sid)
        {
            if (sid == User.FindFirstValue(JwtClaimTypes.SessionId))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Ok();
            }

            return BadRequest();
        }

        [HttpPost("/backchannel-logout")]
        public async Task<IActionResult> BackchannelLogout(string logout_token)
        {
            Response.Headers.Add("Cache-Control", "no-cache, no-store");
            Response.Headers.Add("Pragma", "no-cache");

            try
            {
                // See : https://openid.net/specs/openid-connect-backchannel-1_0.html#Validation
                var user = await ValidateLogoutTokenAsync(logout_token);

                var sub = user.FindFirstValue(JwtClaimTypes.Subject);
                var sid = user.FindFirstValue(JwtClaimTypes.SessionId);
                _sessionStore.Logout(new Session(sub, sid));

                return Ok();
            }
            catch
            {
                return BadRequest();
            }
        }

        private async Task<ClaimsPrincipal> ValidateLogoutTokenAsync(string logoutToken)
        {
            // See : https://openid.net/specs/openid-connect-backchannel-1_0.html#LogoutToken
            var claims = await ValidateJwtAsync(logoutToken);

            if (claims.FindFirst("sub") == null && claims.FindFirst("sid") == null)
            {
                throw new InvalidOperationException("Invalid logout token.");
            }

            var nonce = claims.FindFirst("nonce")?.Value;
            if (!string.IsNullOrWhiteSpace(nonce))
            {
                throw new InvalidOperationException("Invalid logout token.");
            }

            var eventsJson = claims.FindFirst("events")?.Value;
            if (string.IsNullOrWhiteSpace(eventsJson))
            {
                throw new InvalidOperationException("Invalid logout token.");
            }

            var events = JsonDocument.Parse(eventsJson);
            if (!events.RootElement.TryGetProperty("http://schemas.openid.net/event/backchannel-logout", out _))
            {
                throw new InvalidOperationException("Invalid logout token.");
            }

            return claims;
        }

        private async Task<ClaimsPrincipal> ValidateJwtAsync(string jwt)
        {
            var keys = new List<SecurityKey>();
            var parameters = new TokenValidationParameters
            {
                ValidAudience = OidcOptions.ClientId,
                IssuerSigningKeys = keys,

                NameClaimType = JwtClaimTypes.Name,
                RoleClaimType = JwtClaimTypes.Role
            };

            var httpClient = _httpClientFactory.CreateClient();
            var discoRequest = new DiscoveryDocumentRequest
            {
                Address = OidcOptions.Authority
            };
            var discoResponse = await httpClient.GetDiscoveryDocumentAsync(discoRequest);
            if (!discoResponse.IsError)
            {
                parameters.ValidIssuer = discoResponse.Issuer;

                foreach (var webKey in discoResponse.KeySet.Keys)
                {
                    var e = Base64Url.Decode(webKey.E);
                    var n = Base64Url.Decode(webKey.N);

                    var key = new RsaSecurityKey(new RSAParameters { Exponent = e, Modulus = n })
                    {
                        KeyId = webKey.Kid
                    };

                    keys.Add(key);
                }
            }

            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear();

            var user = handler.ValidateToken(jwt, parameters, out var _);
            return user;
        }
    }
}
