using CorpsAPI.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CorpsAPI.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Booking>       Bookings           { get; set; }
        public DbSet<Event>         Events             { get; set; }
        public DbSet<Location>      Locations          { get; set; }
        public DbSet<Child>         Children           { get; set; }
        public DbSet<Waitlist>      Waitlists          { get; set; }
        public DbSet<UserDeviceToken> UserDeviceTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Unique email
            builder.Entity<AppUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Child => AppUser (parent)
            builder.Entity<Child>()
                .HasOne(c => c.ParentUser)
                .WithMany(u => u.Children)
                .HasForeignKey(c => c.ParentUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Booking => AppUser
            builder.Entity<Booking>()
                .HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Booking => Child
            builder.Entity<Booking>()
                .HasOne(b => b.Child)
                .WithMany(c => c.Bookings)
                .HasForeignKey(b => b.ChildId)
                // Database will not cascade already null out in code
                .OnDelete(DeleteBehavior.SetNull);

            // Event => Location
            builder.Entity<Event>()
                .HasOne(e => e.Location)
                .WithMany(l => l.Events)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Event => EventManager
            builder.Entity<Event>()
                .HasOne(e => e.EventManager)
                .WithMany(u => u.ManagedEvents)
                .HasForeignKey(e => e.EventManagerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Event => Bookings
            builder.Entity<Event>()
                .HasMany(e => e.Bookings)
                .WithOne(b => b.Event)
                .HasForeignKey(b => b.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // Composite keys
            builder.Entity<Waitlist>()
                .HasKey(w => new { w.UserId, w.EventId });
            builder.Entity<UserDeviceToken>()
                .HasKey(t => new { t.UserId, t.Token });
        }
    }
}
