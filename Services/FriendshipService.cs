using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;
using TestingAppWeb.Models.ViewModels;

namespace TestingAppWeb.Services
{
    public class FriendshipService : IFriendshipService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FriendshipService> _logger;

        public FriendshipService(AppDbContext context, ILogger<FriendshipService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FriendRequestResult> SendRequestAsync(int requesterId, int addresseeId)
        {
            if (requesterId == addresseeId)
            {
                return FriendRequestResult.CannotAddSelf;
            }

            var requesterExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == requesterId);

            var addressee = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.Id == addresseeId);

            if (!requesterExists || addressee == null || addressee.Role == "Bot")
            {
                return FriendRequestResult.UserNotFound;
            }

            var existing = await _context.Friendships
                .FirstOrDefaultAsync(friendship =>
                    (friendship.RequesterId == requesterId && friendship.AddresseeId == addresseeId) ||
                    (friendship.RequesterId == addresseeId && friendship.AddresseeId == requesterId));

            if (existing != null)
            {
                switch (existing.Status)
                {
                    case FriendshipStatus.Accepted:
                        return FriendRequestResult.AlreadyFriends;

                    case FriendshipStatus.Pending:
                        return FriendRequestResult.AlreadyPending;

                    case FriendshipStatus.Blocked:
                        return FriendRequestResult.Blocked;

                    case FriendshipStatus.Rejected:
                        existing.RequesterId = requesterId;
                        existing.AddresseeId = addresseeId;
                        existing.Status = FriendshipStatus.Pending;
                        existing.CreatedAt = DateTimeOffset.UtcNow;
                        existing.RespondedAt = null;
                        await _context.SaveChangesAsync();
                        return FriendRequestResult.Success;
                }
            }

            _context.Friendships.Add(new Friendship
            {
                RequesterId = requesterId,
                AddresseeId = addresseeId,
                Status = FriendshipStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync();
                return FriendRequestResult.Success;
            }
            catch (DbUpdateException ex)
            {
                // Уникальный индекс (RequesterId, AddresseeId) защищает от гонки.
                _logger.LogWarning(ex, "Duplicate friend request from {RequesterId} to {AddresseeId}.", requesterId, addresseeId);
                return FriendRequestResult.AlreadyPending;
            }
        }

        public async Task<bool> AcceptRequestAsync(int friendshipId, int currentUserId)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(friendship =>
                    friendship.Id == friendshipId &&
                    friendship.AddresseeId == currentUserId &&
                    friendship.Status == FriendshipStatus.Pending);

            if (friendship == null)
            {
                return false;
            }

            friendship.Status = FriendshipStatus.Accepted;
            friendship.RespondedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectRequestAsync(int friendshipId, int currentUserId)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(friendship =>
                    friendship.Id == friendshipId &&
                    friendship.AddresseeId == currentUserId &&
                    friendship.Status == FriendshipStatus.Pending);

            if (friendship == null)
            {
                return false;
            }

            friendship.Status = FriendshipStatus.Rejected;
            friendship.RespondedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CancelRequestAsync(int friendshipId, int currentUserId)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(friendship =>
                    friendship.Id == friendshipId &&
                    friendship.RequesterId == currentUserId &&
                    friendship.Status == FriendshipStatus.Pending);

            if (friendship == null)
            {
                return false;
            }

            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveFriendAsync(int friendshipId, int currentUserId)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(friendship =>
                    friendship.Id == friendshipId &&
                    friendship.Status == FriendshipStatus.Accepted &&
                    (friendship.RequesterId == currentUserId || friendship.AddresseeId == currentUserId));

            if (friendship == null)
            {
                return false;
            }

            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IReadOnlyList<FriendListItem>> GetFriendsAsync(int userId)
        {
            var friendships = await _context.Friendships
                .AsNoTracking()
                .Include(friendship => friendship.Requester)
                .Include(friendship => friendship.Addressee)
                .Where(friendship =>
                    friendship.Status == FriendshipStatus.Accepted &&
                    (friendship.RequesterId == userId || friendship.AddresseeId == userId))
                .ToListAsync();

            return friendships
                .Select(friendship =>
                {
                    var other = friendship.RequesterId == userId
                        ? friendship.Addressee
                        : friendship.Requester;

                    return new FriendListItem
                    {
                        FriendshipId = friendship.Id,
                        UserId = other.Id,
                        Username = other.Username,
                        Bio = other.Bio,
                        FriendsSince = friendship.RespondedAt ?? friendship.CreatedAt
                    };
                })
                .OrderBy(friend => friend.Username)
                .ToList();
        }

        public async Task<IReadOnlyList<FriendRequestItem>> GetIncomingRequestsAsync(int userId)
        {
            var requests = await _context.Friendships
                .AsNoTracking()
                .Include(friendship => friendship.Requester)
                .Where(friendship =>
                    friendship.AddresseeId == userId &&
                    friendship.Status == FriendshipStatus.Pending)
                .OrderByDescending(friendship => friendship.CreatedAt)
                .ToListAsync();

            return requests
                .Select(friendship => new FriendRequestItem
                {
                    FriendshipId = friendship.Id,
                    UserId = friendship.RequesterId,
                    Username = friendship.Requester.Username,
                    Bio = friendship.Requester.Bio,
                    CreatedAt = friendship.CreatedAt
                })
                .ToList();
        }

        public async Task<IReadOnlyList<FriendRequestItem>> GetOutgoingRequestsAsync(int userId)
        {
            var requests = await _context.Friendships
                .AsNoTracking()
                .Include(friendship => friendship.Addressee)
                .Where(friendship =>
                    friendship.RequesterId == userId &&
                    friendship.Status == FriendshipStatus.Pending)
                .OrderByDescending(friendship => friendship.CreatedAt)
                .ToListAsync();

            return requests
                .Select(friendship => new FriendRequestItem
                {
                    FriendshipId = friendship.Id,
                    UserId = friendship.AddresseeId,
                    Username = friendship.Addressee.Username,
                    Bio = friendship.Addressee.Bio,
                    CreatedAt = friendship.CreatedAt
                })
                .ToList();
        }

        public async Task<FriendshipInfo?> GetFriendshipAsync(int userId, int otherUserId)
        {
            var friendship = await _context.Friendships
                .AsNoTracking()
                .FirstOrDefaultAsync(friendship =>
                    (friendship.RequesterId == userId && friendship.AddresseeId == otherUserId) ||
                    (friendship.RequesterId == otherUserId && friendship.AddresseeId == userId));

            if (friendship == null)
            {
                return null;
            }

            return new FriendshipInfo
            {
                FriendshipId = friendship.Id,
                Status = friendship.Status,
                IsIncomingRequest = friendship.AddresseeId == userId
            };
        }

        public async Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(
            int currentUserId,
            string query,
            int take = 20)
        {
            query = query?.Trim() ?? string.Empty;
            if (query.Length < 2)
            {
                return Array.Empty<UserSearchResult>();
            }

            var normalized = query.ToLowerInvariant();

            var users = await _context.Users
                .AsNoTracking()
                .Where(user =>
                    user.Id != currentUserId &&
                    user.Role != "Bot" &&
                    (user.Username.ToLower().Contains(normalized) ||
                     user.Email.ToLower().Contains(normalized)))
                .OrderBy(user => user.Username)
                .Take(Math.Clamp(take, 1, 50))
                .Select(user => new
                {
                    user.Id,
                    user.Username,
                    user.Bio
                })
                .ToListAsync();

            if (users.Count == 0)
            {
                return Array.Empty<UserSearchResult>();
            }

            var userIds = users.Select(user => user.Id).ToList();

            var friendships = await _context.Friendships
                .AsNoTracking()
                .Where(friendship =>
                    (friendship.RequesterId == currentUserId && userIds.Contains(friendship.AddresseeId)) ||
                    (friendship.AddresseeId == currentUserId && userIds.Contains(friendship.RequesterId)))
                .ToListAsync();

            return users
                .Select(user =>
                {
                    var friendship = friendships.FirstOrDefault(candidate =>
                        candidate.RequesterId == user.Id || candidate.AddresseeId == user.Id);

                    return new UserSearchResult
                    {
                        UserId = user.Id,
                        Username = user.Username,
                        Bio = user.Bio,
                        Status = friendship?.Status,
                        FriendshipId = friendship?.Id,
                        IsIncomingRequest = friendship?.AddresseeId == currentUserId
                    };
                })
                .ToList();
        }

        public async Task<int> GetFriendCountAsync(int userId)
        {
            return await _context.Friendships
                .AsNoTracking()
                .CountAsync(friendship =>
                    friendship.Status == FriendshipStatus.Accepted &&
                    (friendship.RequesterId == userId || friendship.AddresseeId == userId));
        }
    }
}
