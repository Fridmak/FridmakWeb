using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface IProfileEditable
    {
        Task<bool> EditProfile(string id, string userName, string bio);
        Task<User> GetUserById(string id);
    }
}
