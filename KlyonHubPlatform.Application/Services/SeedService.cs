using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;
using Microsoft.EntityFrameworkCore;
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
            var existingLocations = await _context.Locations.ToListAsync();
            var existingStates = existingLocations.Select(l => l.State).ToHashSet();
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
                    ImageUrl = "/images/services/sala-compartilhada.svg",
                    Category = CategoryService.ClientAndPartner
                },
                new Service
                {
                    Name = "Aluguel de Sala Particular",
                    Code = "PRIVATE_ROOM_RENTAL",
                    Description = "Aluguel de sala privativa para reuniões e eventos",
                    Route = "/Home/Marketplace",
                    DisplayOrder = 2,
                    IsActive = true,
                    ImageUrl = "/images/services/sala-particular.svg",
                    Category = CategoryService.ClientAndPartner
                },
                new Service
                {
                    Name = "Aluguel de Auditório",
                    Code = AuditoriumRental,
                    Description = "Aluguel de auditório para eventos e apresentações",
                    Route = "/Home/Marketplace",
                    DisplayOrder = 3,
                    IsActive = true,
                    ImageUrl = "/images/services/auditorio.svg",
                    Category = CategoryService.ClientAndPartner
                },
                new Service
                {
                    Name = "Ofertar Endereço Fiscal",
                    Code = FiscalAddressOffer,
                    Description = "Ofereça endereço fiscal para empresas em sua unidade",
                    Route = "/Client/ChooseLocation",
                    DisplayOrder = 4,
                    IsActive = true,
                    ImageUrl = "/images/services/endereco-fiscal.svg",
                    Category = CategoryService.Partner
                },
                new Service
                {
                    Name = "Contratar Endereço Fiscal",
                    Code = FiscalAddressContracting,
                    Description = "Contrate um endereço fiscal para sua empresa de forma rápida e segura",
                    Route = "/Client/ChooseLocation",
                    DisplayOrder = 5,
                    IsActive = true,
                    ImageUrl = "/images/services/endereco-fiscal.svg",
                    Category = CategoryService.Client
                },
                new Service
                {
                    Name = "Contratar Endereço Comercial",
                    Code = CommercialAddressContracting,
                    Description = "Use nosso endereço como apoio comercial: cartão de visita, site e imagem profissional — sem uso no CNPJ",
                    Route = "/Client/ChooseLocation",
                    DisplayOrder = 6,
                    IsActive = true,
                    ImageUrl = "/images/services/endereco-fiscal.svg",
                    Category = CategoryService.Client
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
                    s.Code.Equals(existingService.Code, StringComparison.OrdinalIgnoreCase) ||
                    (s.Code == FiscalAddressOffer) ||
                    (s.Code == FiscalAddressContracting));
                if (matchingService != null)
                {
                    if (string.IsNullOrEmpty(existingService.ImageUrl) && !string.IsNullOrEmpty(matchingService.ImageUrl))
                    {
                        existingService.ImageUrl = matchingService.ImageUrl;
                    }
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
                    if (string.IsNullOrEmpty(existingService.Code) && !string.IsNullOrEmpty(matchingService.Code))
                    {
                        existingService.Code = matchingService.Code;
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
                if (!_context.SystemConfigurations.Any())
                {
                    _context.SystemConfigurations.AddRange(
                    new SystemConfiguration
                    {
                        Key = EmailConfigKey.SmtpServer.ToConfigKey(),
                        Value = "smtp.gmail.com",
                        Description = "Servidor SMTP para envio de e-mails",
                        Category = SystemConfigurationCategoryKeys.Email,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = EmailConfigKey.SmtpPort.ToConfigKey(),
                        Value = "587",
                        Description = "Porta do servidor SMTP",
                        Category = SystemConfigurationCategoryKeys.Email,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = EmailConfigKey.SmtpUsername.ToConfigKey(),
                        Value = "klyonoficial@gmail.com",
                        Description = "Usuário do servidor SMTP (Gmail)",
                        Category = SystemConfigurationCategoryKeys.Email,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = EmailConfigKey.SmtpPassword.ToConfigKey(),
                        Value = "yxgb vizz jmxk veci",
                        Description = "Senha do servidor SMTP (App Password do Gmail)",
                        Category = SystemConfigurationCategoryKeys.Email,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = EmailConfigKey.FromEmail.ToConfigKey(),
                        Value = "noreply@klyon.com.br",
                        Description = "E-mail remetente padrão",
                        Category = SystemConfigurationCategoryKeys.Email,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = EmailConfigKey.FromName.ToConfigKey(),
                        Value = "Klyon Endereço Fiscal",
                        Description = "Nome do remetente",
                        Category = SystemConfigurationCategoryKeys.Email,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = EmailConfigKey.EnableSsl.ToConfigKey(),
                        Value = StringBooleanValue.True,
                        Description = "Habilitar SSL/TLS para conexão SMTP",
                        Category = SystemConfigurationCategoryKeys.Email,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = EmailConfigKey.ReplyToEmail.ToConfigKey(),
                        Value = "",
                        Description = "E-mail para resposta (Reply-To). Deixe vazio para usar o FromEmail",
                        Category = SystemConfigurationCategoryKeys.Email,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = AppSettingsConfigKey.BaseUrl.ToConfigKey(),
                        Value = "https://localhost:5001",
                        Description = "URL base da aplicação (usado em links de email)",
                        Category = SystemConfigurationCategoryKeys.General,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = PaymentConfigKey.Provider.ToConfigKey(),
                        Value = "Asaas",
                        Description = "Provedor de pagamento (Asaas é o fixo)",
                        Category = SystemConfigurationCategoryKeys.Payment,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = AsaasConfigKey.ApiKey.ToConfigKey(),
                        Value = "",
                        Description = "API Key do Asaas (painel Asaas > Integrações)",
                        Category = SystemConfigurationCategoryKeys.Payment,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = AsaasConfigKey.UseSandbox.ToConfigKey(),
                        Value = StringBooleanValue.True,
                        Description = "Usar ambiente sandbox do Asaas (true/false)",
                        Category = SystemConfigurationCategoryKeys.Payment,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = AsaasConfigKey.WebhookToken.ToConfigKey(),
                        Value = "",
                        Description = "Token para validação de webhooks do Asaas (opcional)",
                        Category = SystemConfigurationCategoryKeys.Payment,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = PagBankConfigKey.AccessToken.ToConfigKey(),
                        Value = "",
                        Description = "[Legado] Token da API PagBank",
                        Category = SystemConfigurationCategoryKeys.Payment,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = PagBankConfigKey.UseSandbox.ToConfigKey(),
                        Value = StringBooleanValue.True,
                        Description = "[Legado] Sandbox PagBank",
                        Category = SystemConfigurationCategoryKeys.Payment,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = PagBankConfigKey.WebhookSecret.ToConfigKey(),
                        Value = "",
                        Description = "[Legado] Secret webhooks PagBank",
                        Category = SystemConfigurationCategoryKeys.Payment,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = PaymentConfigKey.DefaultCurrency.ToConfigKey(),
                        Value = "BRL",
                        Description = "Moeda padrão para pagamentos",
                        Category = SystemConfigurationCategoryKeys.Payment,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = StorageConfigKeys.Provider,
                        Value = OracleStorageDefaults.ProviderName,
                        Description = "Provedor de armazenamento: Oracle, FTP ou AWS",
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = OracleStorageConfigKey.Namespace.ToConfigKey(),
                        Value = "",
                        Description = OracleStorageConfigKey.Namespace.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = OracleStorageConfigKey.Region.ToConfigKey(),
                        Value = OracleStorageDefaults.Region,
                        Description = OracleStorageConfigKey.Region.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = OracleStorageConfigKey.BucketName.ToConfigKey(),
                        Value = "",
                        Description = OracleStorageConfigKey.BucketName.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = OracleStorageConfigKey.AccessKeyId.ToConfigKey(),
                        Value = "",
                        Description = OracleStorageConfigKey.AccessKeyId.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = OracleStorageConfigKey.SecretAccessKey.ToConfigKey(),
                        Value = "",
                        Description = OracleStorageConfigKey.SecretAccessKey.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = OracleStorageConfigKey.ContractsFolder.ToConfigKey(),
                        Value = OracleStorageDefaults.ContractsFolder,
                        Description = OracleStorageConfigKey.ContractsFolder.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = OracleStorageConfigKey.PhotosFolder.ToConfigKey(),
                        Value = OracleStorageDefaults.PhotosFolder,
                        Description = OracleStorageConfigKey.PhotosFolder.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = AwsS3ConfigKey.BucketName.ToConfigKey(),
                        Value = "klyon-hub-platform-documents",
                        Description = "Nome do bucket S3 para armazenamento de documentos",
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = AwsS3ConfigKey.Region.ToConfigKey(),
                        Value = AwsS3Defaults.Region,
                        Description = "Região do bucket S3",
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = AwsS3ConfigKey.AccessKeyId.ToConfigKey(),
                        Value = "your-access-key-id",
                        Description = "Access Key ID da AWS",
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = AwsS3ConfigKey.SecretAccessKey.ToConfigKey(),
                        Value = "your-secret-access-key",
                        Description = "Secret Access Key da AWS",
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = AwsS3ConfigKey.ContractsFolder.ToConfigKey(),
                        Value = AwsS3Defaults.ContractsFolder,
                        Description = "Pasta no bucket para armazenar contratos",
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = AwsS3ConfigKey.PhotosFolder.ToConfigKey(),
                        Value = AwsS3Defaults.PhotosFolder,
                        Description = "Pasta no bucket para armazenar fotos dos coworkings",
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = FtpConfigKey.Host.ToConfigKey(),
                        Value = "187.110.161.110",
                        Description = FtpConfigKey.Host.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = FtpConfigKey.Port.ToConfigKey(),
                        Value = FtpDefaults.PortString,
                        Description = FtpConfigKey.Port.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = FtpConfigKey.Username.ToConfigKey(),
                        Value = "gestorhub",
                        Description = FtpConfigKey.Username.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = FtpConfigKey.Password.ToConfigKey(),
                        Value = "F@z3rd@rC3rt0",
                        Description = FtpConfigKey.Password.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = FtpConfigKey.RootPath.ToConfigKey(),
                        Value = "/documents",
                        Description = FtpConfigKey.RootPath.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = FtpConfigKey.ContractsFolder.ToConfigKey(),
                        Value = FtpDefaults.ContractsFolder,
                        Description = FtpConfigKey.ContractsFolder.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = FtpConfigKey.PhotosFolder.ToConfigKey(),
                        Value = "coworking-photos",
                        Description = FtpConfigKey.PhotosFolder.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = FtpConfigKey.UsePassiveMode.ToConfigKey(),
                        Value = FtpDefaults.UsePassive,
                        Description = FtpConfigKey.UsePassiveMode.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = FtpConfigKey.UseSSL.ToConfigKey(),
                        Value = FtpDefaults.UseSsl,
                        Description = FtpConfigKey.UseSSL.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = D4SignConfigKeys.TokenAPI,
                        Value = "",
                        Description = "Token da API D4Sign (menu Dev API na conta D4Sign)",
                        Category = SystemConfigurationCategoryKeys.D4Sign,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = D4SignConfigKeys.CryptKey,
                        Value = "",
                        Description = "Chave criptográfica D4Sign (obrigatória se habilitada na conta)",
                        Category = SystemConfigurationCategoryKeys.D4Sign,
                        IsSensitive = true,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = D4SignConfigKeys.UseSandbox,
                        Value = StringBooleanValue.True,
                        Description = "Usar ambiente sandbox D4Sign (true/false). true = testes; false = produção.",
                        Category = SystemConfigurationCategoryKeys.D4Sign,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = D4SignConfigKeys.SafeUuid,
                        Value = "",
                        Description = "UUID do cofre D4Sign onde os contratos serão armazenados (GET /safes)",
                        Category = SystemConfigurationCategoryKeys.D4Sign,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = UploadConfigKey.MaxPhotoSizeMB.ToConfigKey(),
                        Value = "5",
                        Description = "Tamanho máximo por foto (MB) em uploads: galeria do coworking, fotos de sócios, salas, logo do cliente. Ex.: 5 ou 10.",
                        Category = SystemConfigurationCategoryKeys.Upload,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = PlatformConfigKey.CommissionPercentage.ToConfigKey(),
                        Value = "20",
                        Description = "Percentual de comissão da plataforma sobre reservas (ex: 20 = 20%). Alterável em Configurações do Sistema.",
                        Category = SystemConfigurationCategoryKeys.Platform,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey(),
                        Value = StringBooleanValue.False,
                        Description = "Habilita ou desabilita os menus de coworking (Reservar Sala, Reservas, etc.) para clientes e parceiros. Valores: true ou false.",
                        Category = SystemConfigurationCategoryKeys.Features,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = PaymentConfigKey.CreditCardFeePercentage.ToConfigKey(),
                        Value = "10",
                        Description = "Percentual adicional a ser cobrado quando o pagamento for realizado com cartão de crédito. Exemplo: 2.5 para 2,5%.",
                        Category = SystemConfigurationCategoryKeys.Payment,
                        IsSensitive = false,
                        IsEncrypted = false
                    },
                    new SystemConfiguration
                    {
                        Key = PaymentConfigKey.HiddenPaymentOptions.ToConfigKey(),
                        Value = "",
                        Description = "Formas de pagamento a ocultar (separadas por vírgula). Valores: PixMonthly, PixAnnual, PIX, Card1x, CardMonthly, Boleto. Ex.: PixMonthly oculta PIX Automático (mensal); PixAnnual oculta PIX Automático (anual); PIX oculta PIX (pagamento único); Boleto oculta boleto bancário.",
                        Category = SystemConfigurationCategoryKeys.Payment,
                        IsSensitive = false,
                        IsEncrypted = false
                    }
                    );
                    await _context.SaveChangesAsync();
                }
                var hiddenPaymentOptionsConfig = await _context.SystemConfigurations
                    .FirstOrDefaultAsync(c => c.Key == PaymentConfigKey.HiddenPaymentOptions.ToConfigKey());
                if (hiddenPaymentOptionsConfig != null)
                {
                    hiddenPaymentOptionsConfig.Description =
                        "Formas de pagamento a ocultar (separadas por vírgula). Valores: PixMonthly, PixAnnual, PIX, Card1x, CardMonthly, Boleto. Ex.: PixMonthly oculta PIX Automático (mensal); PixAnnual oculta PIX Automático (anual); PIX oculta PIX (pagamento único); Boleto oculta boleto bancário.";
                    await _context.SaveChangesAsync();
                }
                var coworkingMenusConfig = await _context.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
                if (coworkingMenusConfig != null)
                {
                    coworkingMenusConfig.Value = StringBooleanValue.False;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao fazer seed de SystemConfigurations: {ex.Message}", ex);
            }
            try
            {
                if (_context.PlatformCreditBalances.Any() == false)
                {
                    _context.PlatformCreditBalances.Add(new PlatformCreditBalance { TotalCredits = 0 });
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception) { /* Tabela PlatformCreditBalances pode não existir ainda. */ }
        }
    }
}