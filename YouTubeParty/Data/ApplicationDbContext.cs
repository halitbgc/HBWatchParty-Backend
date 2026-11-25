using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using YouTubeParty.Models;

namespace YouTubeParty.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.DisplayName).HasMaxLength(20);
        });

        builder.Entity<Room>(entity =>
        {
            entity.HasIndex(r => r.RoomCode).IsUnique();

            entity.HasOne(r => r.CreatedByUser)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<RoomMember>(entity =>
        {
            entity.HasIndex(rm => new { rm.RoomId, rm.UserId }).IsUnique();

            entity.HasOne(rm => rm.Room)
                .WithMany(r => r.Members)
                .HasForeignKey(rm => rm.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rm => rm.User)
                .WithMany(u => u.RoomMemberships)
                .HasForeignKey(rm => rm.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
