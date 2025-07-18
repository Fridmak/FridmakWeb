using System.ComponentModel.DataAnnotations;

namespace TestingAppWeb.Models
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Login")]
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        public string ReturnUrl {  get; set; }

        [Display(Name = "RememberMe")]
        public bool RememberMe { get; set; }
    }
}
