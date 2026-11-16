using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Models;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<ChatMSG> ChatMessages { get; set; }
        public DbSet<Friendship> Friendships { get; set; }

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

            modelBuilder.Entity<ChatMSG>()
                .HasOne(message => message.Sender)
                .WithMany()
                .HasForeignKey(message => message.SenderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMSG>()
                .HasIndex(message => message.SentAt);

            modelBuilder.Entity<ChatMSG>()
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
        }
    }
}
