using System.Reflection.Emit;
using CorpsAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CorpsAPI.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            
        }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Child> Children { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AppUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // set attending user to null if user deleted (retain their bookings for reporting)
            builder.Entity<Booking>()
                .HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // don't touch location when event deleted
            builder.Entity<Event>()
                .HasOne(e => e.Location)
                .WithMany(l => l.Events)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // don't touch event manager when event deleted
            builder.Entity<Event>()
                .HasOne(e => e.EventManager)
                .WithMany(u => u.ManagedEvents)
                .HasForeignKey(e => e.EventManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            // delete associated bookings when event deleted
            builder.Entity<Event>()
                .HasMany(e => e.Bookings)
                .WithOne(b => b.Event)
                .HasForeignKey(b => b.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // if event manager is deleted, set the event's manager to null
            builder.Entity<Event>()
                .HasOne(e => e.EventManager)
                .WithMany(u => u.ManagedEvents)
                .HasForeignKey(e => e.EventManagerId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
