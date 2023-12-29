using System.ComponentModel.DataAnnotations;

namespace Csb.Auth.Idp.Controllers.Auth
{
    public class LogoutViewModel
    {
        [Required(ErrorMessage = "Challenge.Required")]
        public string Challenge { get; set; }

        [Required(ErrorMessage = "Store.Required")]
        public string Action { get; set; }

        public string RedictUrl { get; set; }
    }
}
