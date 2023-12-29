using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Csb.Auth.Idp.Controllers.Error
{
    public class ErrorController : Controller
    {
        [Route("/error")]
        public IActionResult Error(ErrorViewModel model)
        {
            var exceptionHandlerFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            model.TraceIdentifier ??= Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            model.Exception = exceptionHandlerFeature?.Error;
            model.ExceptionPath = exceptionHandlerFeature?.Path;
            return View("Error", model);
        }
    }
}
