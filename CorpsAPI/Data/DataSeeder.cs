using CorpsAPI.Constants;
using CorpsAPI.Models;
using Microsoft.AspNetCore.Identity;

namespace CorpsAPI.Data
{
    public static class DataSeeder
    {
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var role in Roles.AllRoles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        public static async Task SeedAdminUser(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();

            var email = "admin@admin.com";
            var password = "Admin123!";
            var dateOfBirth = new DateOnly(1990, 1, 1);

            var adminExists = await userManager.FindByEmailAsync(email);
            if (adminExists == null)
            {
                var adminUser = new AppUser
                {
                    Email = email,
                    UserName = "CorpsAdmin",
                    EmailConfirmed = true,
                    FirstName = "James",
                    LastName = "Ward",
                    DateOfBirth = dateOfBirth
                };

                var result = await userManager.CreateAsync(adminUser, password);

                if (result.Succeeded)
                    await userManager.AddToRoleAsync(adminUser, Roles.Admin);
            }
        }

        public static async Task SeedLocations(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (!context.Locations.Any())
            {
                var locations = new List<Location>
                {
                    new Location { Name = "Invercargill", MascotImgSrc = null },
                    new Location { Name = "Bluff", MascotImgSrc = null },
                    new Location { Name = "Te Anau", MascotImgSrc = null },
                    new Location { Name = "Gore", MascotImgSrc = null },
                    new Location { Name = "Riverton", MascotImgSrc = null },
                    new Location { Name = "Balclutha", MascotImgSrc = null }
                };

                context.Locations.AddRange(locations);
                await context.SaveChangesAsync();
            }
        }
    }
}
