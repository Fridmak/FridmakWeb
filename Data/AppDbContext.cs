using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Models;
using TestingAppWeb.Models.Chat;
using TestingAppWeb.Models.Messaging;

namespace TestingAppWeb.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<DirectMessage> DirectMessages { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("citext");

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(user => user.Username).HasColumnType("citext");
                entity.Property(user => user.Email).HasColumnType("citext");
                entity.HasIndex(user => user.Username).IsUnique();
                entity.HasIndex(user => user.Email).IsUnique();
            });

            modelBuilder.Entity<ChatMessage>()
                .HasOne(message => message.Sender)
                .WithMany()
                .HasForeignKey(message => message.SenderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(message => message.SentAt);

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(message => new { message.SenderId, message.SentAt });

            modelBuilder.Entity<Friendship>(entity =>
            {
                entity.HasOne(friendship => friendship.Requester)
                    .WithMany()
                    .HasForeignKey(friendship => friendship.RequesterId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(friendship => friendship.Addressee)
                    .WithMany()
                    .HasForeignKey(friendship => friendship.AddresseeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(friendship => new { friendship.RequesterId, friendship.AddresseeId })
                    .IsUnique();

                entity.HasIndex(friendship => new { friendship.AddresseeId, friendship.Status });
                entity.HasIndex(friendship => new { friendship.RequesterId, friendship.Status });
            });

            modelBuilder.Entity<Conversation>(entity =>
            {
                entity.HasOne(conversation => conversation.User1)
                    .WithMany()
                    .HasForeignKey(conversation => conversation.User1Id)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(conversation => conversation.User2)
                    .WithMany()
                    .HasForeignKey(conversation => conversation.User2Id)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(conversation => new { conversation.User1Id, conversation.User2Id })
                    .IsUnique();

                entity.HasIndex(conversation => conversation.LastMessageAt);
            });

            modelBuilder.Entity<DirectMessage>(entity =>
            {
                entity.HasOne(message => message.Conversation)
                    .WithMany(conversation => conversation.Messages)
                    .HasForeignKey(message => message.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(message => message.Sender)
                    .WithMany()
                    .HasForeignKey(message => message.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(message => new { message.ConversationId, message.SentAt });
                entity.HasIndex(message => new { message.ConversationId, message.ReadAt });
            });
        }
    }
}
