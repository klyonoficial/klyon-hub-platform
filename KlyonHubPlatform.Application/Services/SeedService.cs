using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;

namespace KlyonHubPlatform.Application.Services
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

            // Seed Languages
            if (!_context.Languages.Any())
            {
                _context.Languages.AddRange(
                    new Language
                    {
                        Code = "pt-BR",
                        Name = "Portuguese (Brazil)",
                        NativeName = "Português (Brasil)",
                        FlagIcon = "🇧🇷",
                        IsActive = true,
                        IsDefault = true
                    },
                    new Language
                    {
                        Code = "en-US",
                        Name = "English (United States)",
                        NativeName = "English (United States)",
                        FlagIcon = "🇺🇸",
                        IsActive = true,
                        IsDefault = false
                    },
                    new Language
                    {
                        Code = "es-ES",
                        Name = "Spanish (Spain)",
                        NativeName = "Español (España)",
                        FlagIcon = "🇪🇸",
                        IsActive = true,
                        IsDefault = false
                    }
                );
                await _context.SaveChangesAsync();
            }

            // Seed System Configurations
            if (!_context.SystemConfigurations.Any())
            {
                _context.SystemConfigurations.AddRange(
                    // Email Settings
                    new SystemConfiguration
                    {
                        Key = "Email.SmtpServer",
                        Value = "smtp.gmail.com",
                        Description = "Servidor SMTP para envio de e-mails",
                        Category = "Email",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Email.SmtpPort",
                        Value = "587",
                        Description = "Porta do servidor SMTP",
                        Category = "Email",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Email.SmtpUsername",
                        Value = "seu-email@gmail.com",
                        Description = "Usuário do servidor SMTP",
                        Category = "Email",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Email.SmtpPassword",
                        Value = "sua-senha-de-app",
                        Description = "Senha do servidor SMTP (App Password)",
                        Category = "Email",
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Email.FromEmail",
                        Value = "noreply@klyon.com.br",
                        Description = "E-mail remetente padrão",
                        Category = "Email",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Email.FromName",
                        Value = "Klyon Endereço Fiscal",
                        Description = "Nome do remetente",
                        Category = "Email",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Email.EnableSsl",
                        Value = "true",
                        Description = "Habilitar SSL/TLS para conexão SMTP",
                        Category = "Email",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    // Payment Settings
                    new SystemConfiguration
                    {
                        Key = "Payment.Provider",
                        Value = "Stripe",
                        Description = "Provedor de pagamento (Stripe, PagSeguro, etc.)",
                        Category = "Payment",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Payment.ApiKey",
                        Value = "sk_test_your_key_here",
                        Description = "Chave de API do provedor de pagamento",
                        Category = "Payment",
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Payment.WebhookSecret",
                        Value = "whsec_your_webhook_secret",
                        Description = "Secret do webhook do provedor de pagamento",
                        Category = "Payment",
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Payment.DefaultCurrency",
                        Value = "BRL",
                        Description = "Moeda padrão para pagamentos",
                        Category = "Payment",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    // AWS S3 Settings
                    new SystemConfiguration
                    {
                        Key = "AWS.S3.BucketName",
                        Value = "klyon-hub-platform-documents",
                        Description = "Nome do bucket S3 para armazenamento de documentos",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "AWS.S3.Region",
                        Value = "us-east-1",
                        Description = "Região do bucket S3",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "AWS.S3.AccessKeyId",
                        Value = "your-access-key-id",
                        Description = "Access Key ID da AWS",
                        Category = "Storage",
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "AWS.S3.SecretAccessKey",
                        Value = "your-secret-access-key",
                        Description = "Secret Access Key da AWS",
                        Category = "Storage",
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "AWS.S3.ContractsFolder",
                        Value = "contracts",
                        Description = "Pasta no bucket para armazenar contratos",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "AWS.S3.PhotosFolder",
                        Value = "coworking-photos",
                        Description = "Pasta no bucket para armazenar fotos dos coworkings",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    }
                );
                await _context.SaveChangesAsync();
            }
        }
    }
}
