using Microsoft.AspNetCore.Identity;

namespace BookBorrowingSystem.Data;

public class SeedData {
    public static async Task SeedRolesAndUsersAsync(IServiceProvider services) {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "User", "Librarian", "Admin" };
        foreach (var role in roles) {
            if (!await roleManager.RoleExistsAsync(role)) {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
        
        var users = new[]
        {
            ("user@test.com", "User"),
            ("librarian@test.com", "Librarian"),
            ("admin@test.com", "Admin")
        };

        foreach (var (email, role) in users)
        {
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(user, "Password1@");
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}