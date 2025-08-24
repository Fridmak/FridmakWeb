namespace TestingAppWeb.Models.ViewModels
{
    public class UserProfileViewModel
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool IsOwnProfile { get; set; }
        public string Bio { get; set; }
    }
}