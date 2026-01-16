using KlyonEF.Data;
using KlyonEF.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace KlyonEF.Services
{
    public class SeedService
    {
        private readonly ApplicationDbContext _context;

        public SeedService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SeedInitialDataAsync()
        {
            // Seed Roles
            if (!_context.Roles.Any())
            {
                _context.Roles.AddRange(
                    new Role { Name = RoleNames.Administrator, Description = "Administrador da Plataforma" },
                    new Role { Name = RoleNames.CoworkingPartner, Description = "Coworking Parceiro" },
                    new Role { Name = RoleNames.Client, Description = "Cliente Final" }
                );
                await _context.SaveChangesAsync();
            }

            // Seed Admin User
            if (!_context.Users.Any(u => u.Email == "admin@klyon.com"))
            {
                var adminUser = new User
                {
                    Email = "admin@klyon.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    FullName = "Administrador",
                    IsActive = true,
                    EmailVerified = true
                };
                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();

                var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Administrator);
                if (adminRole != null)
                {
                    _context.UserRoles.Add(new UserRole
                    {
                        UserId = adminUser.Id,
                        RoleId = adminRole.Id
                    });
                    await _context.SaveChangesAsync();
                }
            }

            // Seed Locations
            if (!_context.Locations.Any())
            {
                _context.Locations.AddRange(
                    new Location { City = "São Paulo", State = "SP", Region = "Sudeste", IsActive = true },
                    new Location { City = "Rio de Janeiro", State = "RJ", Region = "Sudeste", IsActive = true },
                    new Location { City = "Belo Horizonte", State = "MG", Region = "Sudeste", IsActive = true },
                    new Location { City = "Curitiba", State = "PR", Region = "Sul", IsActive = true },
                    new Location { City = "Porto Alegre", State = "RS", Region = "Sul", IsActive = true }
                );
                await _context.SaveChangesAsync();
            }

            // Seed Location Commissions
            if (!_context.LocationCommissions.Any())
            {
                var locations = await _context.Locations.ToListAsync();
                foreach (var location in locations)
                {
                    _context.LocationCommissions.Add(new LocationCommission
                    {
                        LocationId = location.Id,
                        CommissionPercentage = 15.0m,
                        IsActive = true
                    });
                }
                await _context.SaveChangesAsync();
            }
        }
    }
}
