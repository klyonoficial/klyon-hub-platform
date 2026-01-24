using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;
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
            try
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
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao fazer seed de Roles: {ex.Message}", ex);
            }

            try
            {
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
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao fazer seed de Admin User: {ex.Message}", ex);
            }

            // Seed Locations - Uma localidade por estado brasileiro
            // Dicionário com capital de cada estado e sua região
            var brazilianStates = new Dictionary<string, (string City, string Region)>
            {
                { "AC", ("Rio Branco", "Norte") },
                { "AL", ("Maceió", "Nordeste") },
                { "AP", ("Macapá", "Norte") },
                { "AM", ("Manaus", "Norte") },
                { "BA", ("Salvador", "Nordeste") },
                { "CE", ("Fortaleza", "Nordeste") },
                { "DF", ("Brasília", "Centro-Oeste") },
                { "ES", ("Vitória", "Sudeste") },
                { "GO", ("Goiânia", "Centro-Oeste") },
                { "MA", ("São Luís", "Nordeste") },
                { "MT", ("Cuiabá", "Centro-Oeste") },
                { "MS", ("Campo Grande", "Centro-Oeste") },
                { "MG", ("Belo Horizonte", "Sudeste") },
                { "PA", ("Belém", "Norte") },
                { "PB", ("João Pessoa", "Nordeste") },
                { "PR", ("Curitiba", "Sul") },
                { "PE", ("Recife", "Nordeste") },
                { "PI", ("Teresina", "Nordeste") },
                { "RJ", ("Rio de Janeiro", "Sudeste") },
                { "RN", ("Natal", "Nordeste") },
                { "RS", ("Porto Alegre", "Sul") },
                { "RO", ("Porto Velho", "Norte") },
                { "RR", ("Boa Vista", "Norte") },
                { "SC", ("Florianópolis", "Sul") },
                { "SP", ("São Paulo", "Sudeste") },
                { "SE", ("Aracaju", "Nordeste") },
                { "TO", ("Palmas", "Norte") }
            };

            // Buscar localidades existentes por estado
            var existingLocations = await _context.Locations.ToListAsync();
            var existingStates = existingLocations.Select(l => l.State).ToHashSet();

            // Adicionar apenas as localidades que faltam
            var locationsToAdd = brazilianStates
                .Where(state => !existingStates.Contains(state.Key))
                .Select(state => new Location
                {
                    City = state.Value.City,
                    State = state.Key,
                    Region = state.Value.Region,
                    IsActive = true
                }).ToList();

            try
            {
                if (locationsToAdd.Any())
                {
                    _context.Locations.AddRange(locationsToAdd);
                    await _context.SaveChangesAsync();
                }

                // Seed Location Commissions - Garantir que todas as localidades tenham comissão
                var allLocations = await _context.Locations.ToListAsync();
                var existingCommissions = await _context.LocationCommissions.ToListAsync();
                var locationsWithCommission = existingCommissions.Select(c => c.LocationId).ToHashSet();

                foreach (var location in allLocations)
                {
                    if (!locationsWithCommission.Contains(location.Id))
                    {
                        _context.LocationCommissions.Add(new LocationCommission
                        {
                            LocationId = location.Id,
                            CommissionPercentage = 15.0m,
                            IsActive = true
                        });
                    }
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao fazer seed de Locations: {ex.Message}", ex);
            }

            // Localidades adicionais (além das capitais): mesmo estado pode ter várias cidades
            var additionalLocations = new[] { ("Cariacica", "ES", "Sudeste") };
            var existingByCityState = (await _context.Locations.ToListAsync())
                .Select(l => (l.City.Trim().ToLowerInvariant(), l.State.Trim().ToUpperInvariant()))
                .ToHashSet();

            foreach (var (city, state, region) in additionalLocations)
            {
                var key = (city.Trim().ToLowerInvariant(), state.Trim().ToUpperInvariant());
                if (existingByCityState.Contains(key)) continue;

                var loc = new Location { City = city, State = state, Region = region, IsActive = true };
                _context.Locations.Add(loc);
                await _context.SaveChangesAsync();
                _context.LocationCommissions.Add(new LocationCommission
                {
                    LocationId = loc.Id,
                    CommissionPercentage = 15.0m,
                    IsActive = true
                });
                existingByCityState.Add(key);
            }
            await _context.SaveChangesAsync();

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

            // Seed Services
            var existingServices = await _context.Services.ToListAsync();
            var existingServiceNames = existingServices.Select(s => s.Name.ToLower()).ToHashSet();
            
            var servicesToAdd = new List<Service>
            {
                new Service
                {
                    Name = "Aluguel de Sala Compartilhada",
                    Code = "SHARED_ROOM_RENTAL",
                    Description = "Aluguel de espaço em sala compartilhada para trabalho",
                    Route = "/Home/Marketplace",
                    DisplayOrder = 1,
                    IsActive = true,
                    ImageUrl = "/images/services/sala-compartilhada.svg"
                },
                new Service
                {
                    Name = "Aluguel de Sala Particular",
                    Code = "PRIVATE_ROOM_RENTAL",
                    Description = "Aluguel de sala privativa para reuniões e eventos",
                    Route = "/Home/Marketplace",
                    DisplayOrder = 2,
                    IsActive = true,
                    ImageUrl = "/images/services/sala-particular.svg"
                },
                new Service
                {
                    Name = "Aluguel de Auditório",
                    Code = AuditoriumRental,
                    Description = "Aluguel de auditório para eventos e apresentações",
                    Route = "/Home/Marketplace",
                    DisplayOrder = 3,
                    IsActive = true,
                    ImageUrl = "/images/services/auditorio.svg"
                },
                new Service
                {
                    Name = "Ofertar Endereço Fiscal",
                    Code = FiscalAddressOffer,
                    Description = "Contratação de endereço fiscal para empresas",
                    Route = "/Client/ChooseLocation",
                    DisplayOrder = 4,
                    IsActive = true,
                    ImageUrl = "/images/services/endereco-fiscal.svg"
                }
            };

            var newServices = servicesToAdd
                .Where(s => !existingServiceNames.Contains(s.Name.ToLower()))
                .ToList();

            if (newServices.Any())
            {
                _context.Services.AddRange(newServices);
                await _context.SaveChangesAsync();
            }

            // Atualizar serviços existentes com ImageUrl, rotas e códigos corretos
            var servicesToUpdate = existingServices
                .Where(s => string.IsNullOrEmpty(s.ImageUrl) ||
                           s.Route == "/marketplace" ||
                           (s.Route == "/contracts" && s.Name.Contains("Endereço Fiscal")) ||
                           string.IsNullOrEmpty(s.Code))
                .ToList();

            var needsRouteUpdate = false;

            foreach (var existingService in existingServices)
            {
                var matchingService = servicesToAdd.FirstOrDefault(s =>
                    s.Name.Equals(existingService.Name, StringComparison.OrdinalIgnoreCase) ||
                    (existingService.Name == "Contratar Endereço Fiscal" && s.Name == "Ofertar Endereço Fiscal"));
                
                if (matchingService != null)
                {
                    // Atualizar ImageUrl se não tiver
                    if (string.IsNullOrEmpty(existingService.ImageUrl) && !string.IsNullOrEmpty(matchingService.ImageUrl))
                    {
                        existingService.ImageUrl = matchingService.ImageUrl;
                    }

                    // Corrigir rotas incorretas
                    if (existingService.Route == "/marketplace" ||
                        (existingService.Route.Contains("/marketplace") && !existingService.Route.Contains("/Home/Marketplace")))
                    {
                        existingService.Route = "/Home/Marketplace";
                        needsRouteUpdate = true;
                    }
                    else if (existingService.Route == "/contracts" && existingService.Name.Contains("Endereço Fiscal"))
                    {
                        existingService.Route = "/Client/ChooseLocation";
                        needsRouteUpdate = true;
                    }

                    // Atualizar Code se não tiver ou estiver vazio
                    if (string.IsNullOrEmpty(existingService.Code) && !string.IsNullOrEmpty(matchingService.Code))
                    {
                        existingService.Code = matchingService.Code;
                        needsRouteUpdate = true; // Reutilizando a flag para indicar que houve atualização
                    }

                    // Atualizar nome do serviço se for o antigo "Contratar Endereço Fiscal"
                    if (existingService.Name == "Contratar Endereço Fiscal" && matchingService.Name == "Ofertar Endereço Fiscal")
                    {
                        existingService.Name = matchingService.Name;
                        needsRouteUpdate = true;
                    }
                }
            }

            if (servicesToUpdate.Any() || needsRouteUpdate)
            {
                await _context.SaveChangesAsync();
            }

            try
            {
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
                        Value = "klyonoficial@gmail.com",
                        Description = "Usuário do servidor SMTP (Gmail)",
                        Category = "Email",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Email.SmtpPassword",
                        Value = "yxgb vizz jmxk veci",
                        Description = "Senha do servidor SMTP (App Password do Gmail)",
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
                    new SystemConfiguration
                    {
                        Key = "Email.ReplyToEmail",
                        Value = "",
                        Description = "E-mail para resposta (Reply-To). Deixe vazio para usar o FromEmail",
                        Category = "Email",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "AppSettings.BaseUrl",
                        Value = "https://localhost:5001",
                        Description = "URL base da aplicação (usado em links de email)",
                        Category = "General",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    // Payment Settings
                    new SystemConfiguration
                    {
                        Key = "Payment.Provider",
                        Value = "PagBank",
                        Description = "Provedor de pagamento (PagBank, Stripe, etc.)",
                        Category = "Payment",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "PagBank.AccessToken",
                        Value = "",
                        Description = "Token de acesso da API PagBank (Bearer Token)",
                        Category = "Payment",
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "PagBank.UseSandbox",
                        Value = "true",
                        Description = "Usar ambiente sandbox do PagBank (true/false)",
                        Category = "Payment",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "PagBank.WebhookSecret",
                        Value = "",
                        Description = "Secret para validação de webhooks do PagBank",
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
                    // Storage Provider Settings
                    new SystemConfiguration
                    {
                        Key = "Storage.Provider",
                        Value = "FTP",
                        Description = "Provedor de armazenamento: AWS ou FTP",
                        Category = "Storage",
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
                    },
                    // FTP Settings
                    new SystemConfiguration
                    {
                        Key = "FTP.Host",
                        Value = "187.110.161.110",
                        Description = "Endereço do servidor FTP",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "FTP.Port",
                        Value = "21",
                        Description = "Porta do servidor FTP (padrão: 21)",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "FTP.Username",
                        Value = "gestorhub",
                        Description = "Usuário para acesso ao servidor FTP",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "FTP.Password",
                        Value = "F@z3rd@rC3rt0",
                        Description = "Senha para acesso ao servidor FTP",
                        Category = "Storage",
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "FTP.RootPath",
                        Value = "/documents",
                        Description = "Caminho raiz no servidor FTP para armazenamento",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "FTP.ContractsFolder",
                        Value = "contracts",
                        Description = "Pasta no servidor FTP para armazenar contratos",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "FTP.PhotosFolder",
                        Value = "coworking-photos",
                        Description = "Pasta no servidor FTP para armazenar fotos dos coworkings",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "FTP.UsePassiveMode",
                        Value = "true",
                        Description = "Usar modo passivo no FTP (recomendado para firewalls)",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "FTP.UseSSL",
                        Value = "false",
                        Description = "Usar SSL/TLS para conexão FTP (FTPS)",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    // D4Sign - Assinatura eletrônica de contratos (endereço fiscal)
                    new SystemConfiguration
                    {
                        Key = "D4Sign.TokenAPI",
                        Value = "",
                        Description = "Token da API D4Sign (menu Dev API na conta D4Sign)",
                        Category = "D4Sign",
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "D4Sign.CryptKey",
                        Value = "",
                        Description = "Chave criptográfica D4Sign (obrigatória se habilitada na conta)",
                        Category = "D4Sign",
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "D4Sign.UseSandbox",
                        Value = "true",
                        Description = "Usar ambiente sandbox D4Sign (true/false). Produção: false.",
                        Category = "D4Sign",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "D4Sign.SafeUuid",
                        Value = "",
                        Description = "UUID do cofre D4Sign onde os contratos serão armazenados (GET /safes)",
                        Category = "D4Sign",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Upload.MaxPhotoSizeMB",
                        Value = "5",
                        Description = "Tamanho máximo por foto (MB) em uploads: galeria do coworking, fotos de sócios, salas, logo do cliente. Ex.: 5 ou 10.",
                        Category = "Upload",
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = "Platform.CommissionPercentage",
                        Value = "20",
                        Description = "Percentual de comissão da plataforma sobre reservas (ex: 20 = 20%). Alterável em Configurações do Sistema.",
                        Category = "Platform",
                        IsSensitive = false,
                        IsEncrypted = false
                    }
                    );
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao fazer seed de SystemConfigurations: {ex.Message}", ex);
            }

            // Garantir que existe uma linha em PlatformCreditBalances (conta do administrador).
            // Só roda se a tabela já existir (após a migração AddReservationDoubleConfirmAndPlatformCredits).
            try
            {
                if (_context.PlatformCreditBalances.Any() == false)
                {
                    _context.PlatformCreditBalances.Add(new PlatformCreditBalance { TotalCredits = 0 });
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception) { /* Tabela PlatformCreditBalances pode não existir ainda; executar a migração AddReservationDoubleConfirmAndPlatformCredits. */ }
        }
    }
}
