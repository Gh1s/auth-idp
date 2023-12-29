using Csb.Auth.Samples.AuthorizationCodeMvc.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Csb.Auth.Samples.AuthorizationCodeMvc.Controllers
{
    public class HomeController : Controller
    {
        public async Task<IActionResult> Index([FromServices] IAuthenticationService authService)
        {
            var result = await authService.AuthenticateAsync(HttpContext, null);
            if (result != null)
            {
                if (result.Properties.Items.TryGetValue(".Token.id_token", out var idToken))
                {
                    ViewData["IdToken"] = idToken;
                }
                if (result.Properties.Items.TryGetValue(".Token.access_token", out var accessToken))
                {
                    ViewData["AccessToken"] = accessToken;
                }
                if (result.Properties.Items.TryGetValue(".Token.refresh_token", out var refreshToken))
                {
                    ViewData["RefreshToken"] = refreshToken;
                }
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
