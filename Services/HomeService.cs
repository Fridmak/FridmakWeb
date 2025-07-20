using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Controllers;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;

namespace TestingAppWeb.Services
{
    public class HomeService : IHomeService
    {
        private readonly ILogger<HomeService> _logger;
        private readonly AppDbContext _context;

        public HomeService(ILogger<HomeService> logger, AppDbContext context)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> AddFriendRequestAsync(Friend friend)
        {
            friend.Approved = false;
            try
            {
                await _context.Friends.AddAsync(friend);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.InnerException, "Error adding friend");
                return false;
            }
        }

        public async Task<IEnumerable<Friend>> GetFriendListAsync()
        {
            try
            {
                return await _context.Friends.Where(fnd => fnd.Approved).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting friend list");
                return new List<Friend>();
            }
        }
    }
}
