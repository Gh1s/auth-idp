using IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Csb.Auth.Samples.AuthorizationCodeMvc
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Forwarded headers
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
                options.ForwardedHeaders = ForwardedHeaders.All;
                options.ForwardLimit = null;
            });

            // We remap the claim type map to refine them after.
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            // Never do this in production.
            IdentityModelEventSource.ShowPII = true;

            // The cookie authentication scheme is used to store the authentication ticket built after a successful login.
            // The OpenID connect scheme is used to authenticate users, that's why it's setup as the default challenge scheme.
            services
                .AddAuthentication(config =>
                {
                    config.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    config.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, config =>
                {
                    // We check here if the principal has been invalidated by the backchannel logout.
                    config.Events.OnValidatePrincipal = async context =>
                    {
                        if (context.Principal.Identity.IsAuthenticated)
                        {
                            var sessionStore = context.HttpContext.RequestServices.GetRequiredService<SessionStore>();
                            var sub = context.Principal.FindFirst("sub")?.Value;
                            var sid = context.Principal.FindFirst("sid")?.Value;

                            if (sessionStore.IsLoggedOut(new Session(sub, sid)))
                            {
                                context.RejectPrincipal();
                                await context.HttpContext.SignOutAsync();
                            }
                        }
                    };
                })
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, config =>
                {
                    var authSection = Configuration.GetSection("Authentication");
                    authSection.Bind(config);

                    config.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    config.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = JwtClaimTypes.Name,
                        RoleClaimType = JwtClaimTypes.Role
                    };
                    config.Events.OnRedirectToIdentityProvider = ctx =>
                    {
                        ctx.ProtocolMessage.SetParameter("audience", authSection.GetValue<string>("Audience"));
                        return Task.CompletedTask;
                    };

                    // We remap the claims to avoid the mess with the default mapping.
                    config.ClaimActions.MapUniqueJsonKey(JwtClaimTypes.Subject, JwtClaimTypes.Subject, ClaimValueTypes.String);
                    config.ClaimActions.MapUniqueJsonKey(JwtClaimTypes.PreferredUserName, JwtClaimTypes.PreferredUserName, ClaimValueTypes.String);
                    config.ClaimActions.MapUniqueJsonKey(JwtClaimTypes.Name, JwtClaimTypes.Name, ClaimValueTypes.String);
                    config.ClaimActions.MapUniqueJsonKey(JwtClaimTypes.GivenName, JwtClaimTypes.GivenName, ClaimValueTypes.String);
                    config.ClaimActions.MapUniqueJsonKey(JwtClaimTypes.FamilyName, JwtClaimTypes.FamilyName, ClaimValueTypes.String);
                    config.ClaimActions.MapUniqueJsonKey(JwtClaimTypes.Picture, JwtClaimTypes.Picture, ClaimValueTypes.String);
                    config.ClaimActions.MapUniqueJsonKey(JwtClaimTypes.Email, JwtClaimTypes.Email, ClaimValueTypes.Email);
                    config.ClaimActions.MapUniqueJsonKey(JwtClaimTypes.EmailVerified, JwtClaimTypes.EmailVerified, ClaimValueTypes.Boolean);
                    config.ClaimActions.MapUniqueJsonKey(JwtClaimTypes.PhoneNumber, JwtClaimTypes.PhoneNumber, ClaimValueTypes.String);
                    config.ClaimActions.MapUniqueJsonKey(JwtClaimTypes.PhoneNumberVerified, JwtClaimTypes.PhoneNumberVerified, ClaimValueTypes.Boolean);
                });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            });

            var mvcBuilder = services.AddControllersWithViews();
#if DEBUG
            mvcBuilder.AddRazorRuntimeCompilation();
#endif

            services.AddHttpClient();
            services.AddSingleton<SessionStore>();

            services.AddHealthChecks();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseForwardedHeaders();
            }

            app.UsePathBase(Configuration.GetValue<string>("PathBase"));

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health");
                endpoints
                    .MapDefaultControllerRoute()
                    .RequireAuthorization();
            });
        }
    }
}
