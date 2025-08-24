using System.ComponentModel.DataAnnotations;

namespace TestingAppWeb.Models.ViewModels
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Имя пользователя обязательно")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "От 3 до 50 символов")]
        [Display(Name = "Имя пользователя")]
        public string Username { get; set; }

        [StringLength(500, ErrorMessage = "Описание не должно превышать 500 символов")]
        [Display(Name = "About")]
        [DataType(DataType.MultilineText)]
        public string Bio { get; set; } = "";
    }
}