using System.ComponentModel.DataAnnotations;

namespace TestingAppWeb.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Username { get; set; } = null!;
        [Required]
        public string PasswordHash { get; set; } = null!;
        [Required]
        public string Email { get; set; } = null!;
        [Required]
        public string Role { get; set; } = null!;

        [StringLength(500)]
        [Display(Name = "About")]
        public string Bio { get; set; } = string.Empty;
    }
}
