using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Models;
using TestingAppWeb.Models.ViewModels;

namespace TestingAppWeb.Services.Friends
{
    public interface IFriendshipService
    {
        Task<FriendRequestResult> SendRequestAsync(
            int requesterId,
            int addresseeId,
            CancellationToken cancellationToken = default);

        Task<bool> AcceptRequestAsync(
            int friendshipId,
            int currentUserId,
            CancellationToken cancellationToken = default);

        Task<bool> RejectRequestAsync(
            int friendshipId,
            int currentUserId,
            CancellationToken cancellationToken = default);

        Task<bool> CancelRequestAsync(
            int friendshipId,
            int currentUserId,
            CancellationToken cancellationToken = default);

        Task<bool> RemoveFriendAsync(
            int friendshipId,
            int currentUserId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FriendListItem>> GetFriendsAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FriendRequestItem>> GetIncomingRequestsAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FriendRequestItem>> GetOutgoingRequestsAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<FriendshipInfo?> GetFriendshipAsync(
            int userId,
            int otherUserId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(
            int currentUserId,
            string query,
            int take = 20,
            CancellationToken cancellationToken = default);

        Task<int> GetFriendCountAsync(
            int userId,
            CancellationToken cancellationToken = default);
    }

    public class FriendshipService : IFriendshipService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FriendshipService> _logger;

        public FriendshipService(AppDbContext context, ILogger<FriendshipService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FriendRequestResult> SendRequestAsync(
            int requesterId,
            int addresseeId,
            CancellationToken cancellationToken = default)
        {
            if (requesterId == addresseeId)
            {
                return FriendRequestResult.CannotAddSelf;
            }

            var requesterExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == requesterId, cancellationToken);

            var addressee = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.Id == addresseeId, cancellationToken);

            if (!requesterExists || addressee == null || addressee.Role == "Bot")
            {
                return FriendRequestResult.UserNotFound;
            }

            var existing = await FindBetweenAsync(requesterId, addresseeId, cancellationToken);

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
                        await _context.SaveChangesAsync(cancellationToken);
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
                await _context.SaveChangesAsync(cancellationToken);
                return FriendRequestResult.Success;
            }
            catch (DbUpdateException ex)
            {
                // Уникальный индекс (RequesterId, AddresseeId) защищает от гонки.
                _logger.LogWarning(
                    ex,
                    "Duplicate friend request from {RequesterId} to {AddresseeId}.",
                    requesterId,
                    addresseeId);
                return FriendRequestResult.AlreadyPending;
            }
        }

        public async Task<bool> AcceptRequestAsync(
            int friendshipId,
            int currentUserId,
            CancellationToken cancellationToken = default)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(
                    item => item.Id == friendshipId &&
                            item.AddresseeId == currentUserId &&
                            item.Status == FriendshipStatus.Pending,
                    cancellationToken);

            if (friendship == null)
            {
                return false;
            }

            friendship.Status = FriendshipStatus.Accepted;
            friendship.RespondedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> RejectRequestAsync(
            int friendshipId,
            int currentUserId,
            CancellationToken cancellationToken = default)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(
                    item => item.Id == friendshipId &&
                            item.AddresseeId == currentUserId &&
                            item.Status == FriendshipStatus.Pending,
                    cancellationToken);

            if (friendship == null)
            {
                return false;
            }

            friendship.Status = FriendshipStatus.Rejected;
            friendship.RespondedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> CancelRequestAsync(
            int friendshipId,
            int currentUserId,
            CancellationToken cancellationToken = default)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(
                    item => item.Id == friendshipId &&
                            item.RequesterId == currentUserId &&
                            item.Status == FriendshipStatus.Pending,
                    cancellationToken);

            if (friendship == null)
            {
                return false;
            }

            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> RemoveFriendAsync(
            int friendshipId,
            int currentUserId,
            CancellationToken cancellationToken = default)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(
                    item => item.Id == friendshipId &&
                            item.Status == FriendshipStatus.Accepted &&
                            (item.RequesterId == currentUserId || item.AddresseeId == currentUserId),
                    cancellationToken);

            if (friendship == null)
            {
                return false;
            }

            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<IReadOnlyList<FriendListItem>> GetFriendsAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            var friendships = await _context.Friendships
                .AsNoTracking()
                .Include(friendship => friendship.Requester)
                .Include(friendship => friendship.Addressee)
                .Where(friendship =>
                    friendship.Status == FriendshipStatus.Accepted &&
                    (friendship.RequesterId == userId || friendship.AddresseeId == userId))
                .ToListAsync(cancellationToken);

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

        public async Task<IReadOnlyList<FriendRequestItem>> GetIncomingRequestsAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            var requests = await _context.Friendships
                .AsNoTracking()
                .Include(friendship => friendship.Requester)
                .Where(friendship =>
                    friendship.AddresseeId == userId &&
                    friendship.Status == FriendshipStatus.Pending)
                .OrderByDescending(friendship => friendship.CreatedAt)
                .ToListAsync(cancellationToken);

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

        public async Task<IReadOnlyList<FriendRequestItem>> GetOutgoingRequestsAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            var requests = await _context.Friendships
                .AsNoTracking()
                .Include(friendship => friendship.Addressee)
                .Where(friendship =>
                    friendship.RequesterId == userId &&
                    friendship.Status == FriendshipStatus.Pending)
                .OrderByDescending(friendship => friendship.CreatedAt)
                .ToListAsync(cancellationToken);

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

        public async Task<FriendshipInfo?> GetFriendshipAsync(
            int userId,
            int otherUserId,
            CancellationToken cancellationToken = default)
        {
            var friendship = await FindBetweenAsync(userId, otherUserId, cancellationToken, asNoTracking: true);
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
            int take = 20,
            CancellationToken cancellationToken = default)
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
                .ToListAsync(cancellationToken);

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
                .ToListAsync(cancellationToken);

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

        public async Task<int> GetFriendCountAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Friendships
                .AsNoTracking()
                .CountAsync(
                    friendship =>
                        friendship.Status == FriendshipStatus.Accepted &&
                        (friendship.RequesterId == userId || friendship.AddresseeId == userId),
                    cancellationToken);
        }

        private async Task<Friendship?> FindBetweenAsync(
            int firstUserId,
            int secondUserId,
            CancellationToken cancellationToken,
            bool asNoTracking = false)
        {
            var query = _context.Friendships.AsQueryable();

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            return await query.FirstOrDefaultAsync(
                friendship =>
                    (friendship.RequesterId == firstUserId && friendship.AddresseeId == secondUserId) ||
                    (friendship.RequesterId == secondUserId && friendship.AddresseeId == firstUserId),
                cancellationToken);
        }
    }
}
