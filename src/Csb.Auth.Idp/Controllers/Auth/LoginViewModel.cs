using System.ComponentModel.DataAnnotations;

namespace Csb.Auth.Idp.Controllers.Auth
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Challenge.Required")]
        public string Challenge { get; set; }

        [Required(ErrorMessage = "Store.Required")]
        public string Store { get; set; }

        [Required(ErrorMessage = "Username.Required")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password.Required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }
}
