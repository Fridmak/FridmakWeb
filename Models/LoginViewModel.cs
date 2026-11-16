using System.ComponentModel.DataAnnotations;

namespace TestingAppWeb.Models
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Login")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        public string ReturnUrl { get; set; } = "/Home/Index";

        [Display(Name = "RememberMe")]
        public bool RememberMe { get; set; }
    }
}
