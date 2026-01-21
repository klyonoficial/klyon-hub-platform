using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace KlyonHubPlatform.Api.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly IUserService _userService;
        private readonly IReportService _reportService;
        private readonly ILocationService _locationService;
        private readonly IAdminService _adminService;
        private readonly ISystemConfigurationService _systemConfigurationService;
        private readonly IPaymentService _paymentService;
        private readonly ApplicationDbContext _context;
        private readonly IPagBankService _pagBankService;

        public AdminController(
            IUserService userService,
            IReportService reportService,
            ILocationService locationService,
            IAdminService adminService,
            ISystemConfigurationService systemConfigurationService,
            IPaymentService paymentService,
            ApplicationDbContext context,
            IPagBankService pagBankService)
        {
            _userService = userService;
            _reportService = reportService;
            _locationService = locationService;
            _adminService = adminService;
            _systemConfigurationService = systemConfigurationService;
            _paymentService = paymentService;
            _context = context; // Mantido apenas para operações de roles que ainda não foram movidas
            _pagBankService = pagBankService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var dashboardData = await _reportService.GetDashboardDataAsync();

            ViewBag.TotalUsers = dashboardData.TotalUsers;
            ViewBag.TotalCoworkings = dashboardData.TotalCoworkings;
            ViewBag.ActiveContracts = dashboardData.ActiveContracts;
            ViewBag.TotalRevenue = dashboardData.TotalRevenue;
            ViewBag.TotalLocations = dashboardData.TotalLocations;
            ViewBag.PendingPayments = dashboardData.PendingPayments;
            ViewBag.TotalCommissions = dashboardData.TotalCommissions;
            ViewBag.InactiveUsers = dashboardData.InactiveUsers;
            ViewBag.UnverifiedUsers = dashboardData.UnverifiedUsers;
            ViewBag.MonthlyRevenue = dashboardData.MonthlyRevenue;
            ViewBag.UsersByRole = dashboardData.UsersByRole;
            ViewBag.ContractsByStatus = dashboardData.ContractsByStatus;
            ViewBag.TopLocations = dashboardData.TopLocations;
            ViewBag.RecentPayments = dashboardData.RecentPayments;

            // Estatísticas de produtos
            try
            {
                var productsStats = await _context.CoworkingProducts
                    .Where(p => p.IsActive)
                    .GroupBy(p => p.ProductType)
                    .Select(g => new { productType = g.Key, count = g.Count() })
                    .ToListAsync();
                ViewBag.ProductsByType = productsStats;
                
                var totalReservations = await _context.Interests.CountAsync();
                ViewBag.TotalReservations = totalReservations;
                
                var reservationsByStatus = await _context.Interests
                    .GroupBy(i => i.Status)
                    .Select(g => new { status = g.Key, count = g.Count() })
                    .ToListAsync();
                ViewBag.ReservationsByStatus = reservationsByStatus;
            }
            catch
            {
                ViewBag.ProductsByType = new List<dynamic>();
                ViewBag.TotalReservations = 0;
                ViewBag.ReservationsByStatus = new List<dynamic>();
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Users(string? search = null, string? role = null, string? status = null, string? emailVerified = null, int page = 1, int pageSize = 15)
        {
            var users = await _adminService.GetUsersFilteredAsync(search, role, status, emailVerified, page, pageSize);
            var totalItems = await _adminService.GetUsersFilteredCountAsync(search, role, status, emailVerified);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            ViewBag.Search = search;
            ViewBag.SelectedRole = role;
            ViewBag.Status = status;
            ViewBag.EmailVerified = emailVerified;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.AvailableRoles = await _adminService.GetAvailableRolesAsync();

            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> ViewUser(int id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            
            var allRoles = await _context.Roles.ToListAsync();
            var userRoles = user.UserRoles.Select(ur => ur.RoleId).ToList();
            
            ViewBag.AllRoles = allRoles;
            ViewBag.UserRoles = userRoles;
            
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(int id, string fullName, string email, string? phone, string isActive, int[]? roleIds)
        {
            try
            {
                var user = await _userService.GetByIdAsync(id);
                if (user == null)
                {
                    return NotFound();
                }

                user.FullName = fullName;
                user.Email = email;
                user.Phone = phone;
                user.IsActive = !string.IsNullOrEmpty(isActive) && isActive.ToLower() == "true";
                user.UpdatedAt = DateTime.UtcNow;

                await _userService.UpdateAsync(user);

                // Atualizar roles
                if (roleIds != null)
                {
                    var existingUserRoles = await _context.UserRoles
                        .Where(ur => ur.UserId == id)
                        .ToListAsync();
                    
                    _context.UserRoles.RemoveRange(existingUserRoles);
                    
                    foreach (var roleId in roleIds)
                    {
                        _context.UserRoles.Add(new UserRole
                        {
                            UserId = id,
                            RoleId = roleId
                        });
                    }
                    
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Usuário atualizado com sucesso!";
                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar usuário: {ex.Message}";
                return RedirectToAction("EditUser", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyUserEmail(int id)
        {
            try
            {
                await _adminService.VerifyUserEmailAsync(id);
                TempData["Success"] = "E-mail verificado com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao verificar e-mail: {ex.Message}";
            }

            return RedirectToAction("ViewUser", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(int userId, string newPassword, string confirmNewPassword)
        {
            if (string.IsNullOrEmpty(newPassword) || newPassword != confirmNewPassword)
            {
                TempData["Error"] = "As senhas não coincidem ou estão vazias.";
                return RedirectToAction("ViewUser", new { id = userId });
            }

            try
            {
                await _adminService.ResetUserPasswordAsync(userId, newPassword, confirmNewPassword);
                TempData["Success"] = "Senha resetada com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao resetar senha: {ex.Message}";
            }

            return RedirectToAction("ViewUser", new { id = userId });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> FixPasswordHash(string email, string newPassword = "password")
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    return Content($"<h1>Usuário não encontrado</h1><p>Email: {email}</p>", "text/html");
                }

                var hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                user.PasswordHash = hash;
                await _context.SaveChangesAsync();

                var savedUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                var hashValid = savedUser?.PasswordHash?.StartsWith("$2a$") ?? false;

                return Content($"<h1>Senha Resetada!</h1>" +
                    $"<p><strong>Email:</strong> {user.Email}</p>" +
                    $"<p><strong>Nova Senha:</strong> {newPassword}</p>" +
                    $"<p><strong>Hash válido:</strong> {(hashValid ? "SIM ✓" : "NÃO ✗")}</p>" +
                    $"<p><strong>Hash (primeiros 30 chars):</strong> {(savedUser?.PasswordHash != null ? savedUser.PasswordHash.Substring(0, Math.Min(30, savedUser.PasswordHash.Length)) : "N/A")}</p>" +
                    $"<hr><p><a href='/Auth/Login'>Fazer Login</a></p>", "text/html");
            }
            catch (Exception ex)
            {
                return Content($"<h1>Erro</h1><p>{ex.Message}</p><pre>{ex.StackTrace}</pre>", "text/html");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Coworkings(string? search = null, string? status = null, string? isPartner = null, int page = 1, int pageSize = 15)
        {
            var coworkings = await _adminService.GetCoworkingsFilteredAsync(search, status, isPartner, page, pageSize);
            var totalItems = await _adminService.GetCoworkingsFilteredCountAsync(search, status, isPartner);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.IsPartner = isPartner;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(coworkings);
        }

        [HttpGet]
        public async Task<IActionResult> ViewCoworking(int id)
        {
            var coworking = await _context.Coworkings
                .Include(c => c.User)
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.Location)
                .Include(c => c.Photos)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (coworking == null)
            {
                TempData["Error"] = "Coworking não encontrado.";
                return RedirectToAction("Coworkings");
            }

            return View(coworking);
        }

        [HttpGet]
        public async Task<IActionResult> EditCoworking(int id)
        {
            var coworking = await _context.Coworkings
                .Include(c => c.User)
                .Include(c => c.Addresses)
                .Include(c => c.Photos)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (coworking == null)
            {
                TempData["Error"] = "Coworking não encontrado.";
                return RedirectToAction("Coworkings");
            }

            return View(coworking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCoworkingStatus(int id)
        {
            try
            {
                var coworking = await _context.Coworkings.FindAsync(id);

                if (coworking == null)
                {
                    TempData["Error"] = "Coworking não encontrado.";
                    return RedirectToAction("Coworkings");
                }

                coworking.IsActive = !coworking.IsActive;
                coworking.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var statusText = coworking.IsActive ? "ativado" : "inativado";
                TempData["Success"] = $"Coworking '{coworking.Name}' foi {statusText} com sucesso.";

                return RedirectToAction("Coworkings", new { 
                    search = Request.Query["search"],
                    status = Request.Query["status"],
                    isPartner = Request.Query["isPartner"],
                    page = Request.Query["page"]
                });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao alterar status do coworking: {ex.Message}";
                return RedirectToAction("Coworkings");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Contracts(string? search = null, string? status = null, int? clientId = null, int page = 1, int pageSize = 15)
        {
            var contracts = await _adminService.GetContractsFilteredAsync(search, status, clientId, page, pageSize);
            var totalItems = await _adminService.GetContractsFilteredCountAsync(search, status, clientId);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.ClientId = clientId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.Clients = await _adminService.GetAllClientsAsync();

            return View(contracts);
        }

        [HttpGet]
        public async Task<IActionResult> Locations(string? search = null, string? state = null, string? region = null, string? status = null, int page = 1, int pageSize = 12)
        {
            var locations = await _locationService.GetFilteredAsync(search, state, region, status, page, pageSize);
            var totalItems = await _locationService.GetFilteredCountAsync(search, state, region, status);
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            ViewBag.Commissions = await _locationService.GetActiveCommissionsAsync();
            ViewBag.Search = search;
            ViewBag.State = state;
            ViewBag.Region = region;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.States = await _locationService.GetStatesAsync();
            ViewBag.Regions = await _locationService.GetRegionsAsync();

            return View(locations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLocationCommission(int locationId, decimal commissionPercentage)
        {
            try
            {
                await _locationService.UpdateCommissionAsync(locationId, commissionPercentage);
                TempData["Success"] = $"Comissão atualizada para {commissionPercentage}%";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar comissão: {ex.Message}";
            }

            return RedirectToAction("Locations");
        }

        [HttpGet]
        public IActionResult CreateLocation()
        {
            // Lista de estados brasileiros
            ViewBag.States = new List<string>
            {
                "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
                "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
                "RS", "RO", "RR", "SC", "SP", "SE", "TO"
            };

            // Lista de regiões
            ViewBag.Regions = new List<string>
            {
                "Norte", "Nordeste", "Centro-Oeste", "Sudeste", "Sul"
            };

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLocation(string city, string state, string? region, string? isActive, decimal commissionPercentage = 15.0m)
        {
            try
            {
                var isActiveBool = !string.IsNullOrEmpty(isActive) && isActive.ToLower() == "true";
                await _locationService.CreateAsync(city, state, region, isActiveBool, commissionPercentage);

                TempData["Success"] = $"Localidade {city}/{state} criada com sucesso!";
                return RedirectToAction("Locations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao criar localidade: {ex.Message}";
                
                // Recarregar ViewBag para não perder os dados ao retornar a view
                ViewBag.States = new List<string>
                {
                    "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
                    "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
                    "RS", "RO", "RR", "SC", "SP", "SE", "TO"
                };
                ViewBag.Regions = new List<string>
                {
                    "Norte", "Nordeste", "Centro-Oeste", "Sudeste", "Sul"
                };
                
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditLocation(int id)
        {
            var location = await _locationService.GetByIdAsync(id);
            if (location == null)
            {
                return NotFound();
            }

            // Lista de estados brasileiros
            ViewBag.States = new List<string>
            {
                "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
                "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
                "RS", "RO", "RR", "SC", "SP", "SE", "TO"
            };

            // Lista de regiões
            ViewBag.Regions = new List<string>
            {
                "Norte", "Nordeste", "Centro-Oeste", "Sudeste", "Sul"
            };

            return View(location);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLocation(int id, string city, string state, string? region, string? isActive)
        {
            try
            {
                var isActiveBool = !string.IsNullOrEmpty(isActive) && isActive.ToLower() == "true";
                await _locationService.UpdateAsync(id, city, state, region, isActiveBool);

                TempData["Success"] = $"Localidade {city}/{state} atualizada com sucesso!";
                return RedirectToAction("Locations");
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Localidade não encontrada.";
                return RedirectToAction("Locations");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("EditLocation", new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar localidade: {ex.Message}";
                return RedirectToAction("EditLocation", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            try
            {
                var deleted = await _locationService.DeleteAsync(id);
                if (!deleted)
                {
                    TempData["Error"] = "Localidade não encontrada.";
                    return RedirectToAction("Locations");
                }

                TempData["Success"] = "Localidade excluída com sucesso!";
                return RedirectToAction("Locations");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Locations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao excluir localidade: {ex.Message}";
                return RedirectToAction("Locations");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            var startDate = DateTime.UtcNow.AddMonths(-1);
            var endDate = DateTime.UtcNow;

            // Buscar pagamentos dos últimos 30 dias incluindo pendentes
            var payments = await _reportService.GetRecentPaymentsAsync(30, 500);

            ViewBag.Payments = payments;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SystemConfigurations()
        {
            var configurations = await _systemConfigurationService.GetAllAsync();
            var groupedByCategory = configurations.GroupBy(c => c.Category).ToList();
            
            // Obter o provedor de storage atual
            var currentProvider = await _systemConfigurationService.GetValueAsync("Storage.Provider") ?? "AWS";
            
            ViewBag.GroupedConfigurations = groupedByCategory;
            ViewBag.Categories = groupedByCategory.Select(g => g.Key).ToList();
            ViewBag.CurrentStorageProvider = currentProvider;
            
            return View(configurations);
        }

        [HttpGet]
        public async Task<IActionResult> EditSystemConfiguration(int id)
        {
            var configuration = await _systemConfigurationService.GetByIdAsync(id);
            if (configuration == null)
            {
                TempData["Error"] = "Configuração não encontrada.";
                return RedirectToAction("SystemConfigurations");
            }
            
            return View(configuration);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSystemConfiguration(int id, string value, string? description)
        {
            try
            {
                var configuration = await _systemConfigurationService.GetByIdAsync(id);
                if (configuration == null)
                {
                    TempData["Error"] = "Configuração não encontrada.";
                    return RedirectToAction("SystemConfigurations");
                }

                configuration.Value = value;
                configuration.Description = description ?? configuration.Description;
                configuration.UpdatedBy = User.Identity?.Name;

                await _systemConfigurationService.UpdateAsync(configuration, User.Identity?.Name);

                TempData["Success"] = $"Configuração '{configuration.Key}' atualizada com sucesso!";
                return RedirectToAction("SystemConfigurations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar configuração: {ex.Message}";
                return RedirectToAction("EditSystemConfiguration", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStorageToFTP()
        {
            try
            {
                // Atualizar Storage.Provider para FTP
                var storageProvider = await _context.SystemConfigurations
                    .FirstOrDefaultAsync(c => c.Key == "Storage.Provider");
                if (storageProvider != null)
                {
                    storageProvider.Value = "FTP";
                    storageProvider.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.SystemConfigurations.Add(new Domain.Entities.SystemConfiguration
                    {
                        Key = "Storage.Provider",
                        Value = "FTP",
                        Description = "Provedor de armazenamento: AWS ou FTP",
                        Category = "Storage",
                        IsSensitive = false,
                        IsEncrypted = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Atualizar configurações FTP
                var ftpConfigs = new Dictionary<string, string>
                {
                    { "FTP.Host", "187.110.161.110" },
                    { "FTP.Port", "21" },
                    { "FTP.Username", "gestorhub" },
                    { "FTP.Password", "F@z3rd@rC3rt0" },
                    { "FTP.UsePassiveMode", "true" },
                    { "FTP.UseSSL", "false" }
                };

                foreach (var config in ftpConfigs)
                {
                    var existing = await _context.SystemConfigurations
                        .FirstOrDefaultAsync(c => c.Key == config.Key);
                    
                    if (existing != null)
                    {
                        existing.Value = config.Value;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _context.SystemConfigurations.Add(new Domain.Entities.SystemConfiguration
                        {
                            Key = config.Key,
                            Value = config.Value,
                            Description = config.Key switch
                            {
                                "FTP.Host" => "Endereço do servidor FTP",
                                "FTP.Port" => "Porta do servidor FTP (padrão: 21)",
                                "FTP.Username" => "Usuário para acesso ao servidor FTP",
                                "FTP.Password" => "Senha para acesso ao servidor FTP",
                                "FTP.UsePassiveMode" => "Usar modo passivo no FTP (recomendado para firewalls)",
                                "FTP.UseSSL" => "Usar SSL/TLS para conexão FTP (FTPS)",
                                _ => ""
                            },
                            Category = "Storage",
                            IsSensitive = config.Key == "FTP.Password",
                            IsEncrypted = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Configurações FTP atualizadas com sucesso! O sistema agora usa FTP como provedor de armazenamento.";
                return RedirectToAction("SystemConfigurations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar configurações: {ex.Message}";
                return RedirectToAction("SystemConfigurations");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAddressDeclarationsTable()
        {
            try
            {
                // Verificar se a tabela já existe usando information_schema
                var tableExists = false;
                try
                {
                    var connection = _context.Database.GetDbConnection();
                    var wasOpen = connection.State == System.Data.ConnectionState.Open;
                    if (!wasOpen)
                    {
                        await connection.OpenAsync();
                    }
                    try
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = @"
                            SELECT COUNT(*) 
                            FROM information_schema.tables 
                            WHERE table_schema = DATABASE() 
                            AND table_name = 'AddressDeclarations'";
                        var result = await command.ExecuteScalarAsync();
                        tableExists = Convert.ToInt32(result) > 0;
                    }
                    finally
                    {
                        if (!wasOpen)
                        {
                            await connection.CloseAsync();
                        }
                    }
                }
                catch
                {
                    // Se houver erro na verificação, tentar criar mesmo assim
                    tableExists = false;
                }

                if (!tableExists)
                {
                    // Criar a tabela AddressDeclarations
                    // Dividir em comandos menores para evitar problemas com múltiplos statements
                    var createTableSql = @"
                        CREATE TABLE IF NOT EXISTS `AddressDeclarations` (
                            `Id` int NOT NULL AUTO_INCREMENT,
                            `ContractId` int NOT NULL,
                            `DeclarationNumber` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
                            `Version` int NOT NULL,
                            `StartDate` datetime(6) NOT NULL,
                            `EndDate` datetime(6) NOT NULL,
                            `FilePath` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
                            `DigitalSignature` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
                            `IsValid` tinyint(1) NOT NULL,
                            `InvalidatedAt` datetime(6) NULL,
                            `InvalidationReason` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
                            `CreatedAt` datetime(6) NOT NULL,
                            `UpdatedAt` datetime(6) NULL,
                            PRIMARY KEY (`Id`),
                            CONSTRAINT `FK_AddressDeclarations_Contracts_ContractId` FOREIGN KEY (`ContractId`) REFERENCES `Contracts` (`Id`) ON DELETE CASCADE,
                            INDEX `IX_AddressDeclarations_ContractId_Version` (`ContractId`, `Version`),
                            INDEX `IX_AddressDeclarations_DeclarationNumber` (`DeclarationNumber`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

                    await _context.Database.ExecuteSqlRawAsync(createTableSql);

                    TempData["Success"] = "Tabela AddressDeclarations criada com sucesso!";
                }
                else
                {
                    TempData["Info"] = "A tabela AddressDeclarations já existe no banco de dados.";
                }

                return RedirectToAction("SystemConfigurations");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData["Error"] = errorMessage;
                return RedirectToAction("SystemConfigurations");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfigurePagBankSandbox()
        {
            try
            {
                // Atualizar configurações do PagBank para Sandbox
                var configs = new Dictionary<string, string>
                {
                    { "PagBank.AccessToken", "db3453dd-8c96-46c8-9c91-b5549372ed51c6e68eb54829b960fbe67c1d261e73a27c75-0f2a-47d9-9fbe-da8211abbff4" },
                    { "PagBank.UseSandbox", "true" }
                };

                foreach (var config in configs)
                {
                    var existing = await _context.SystemConfigurations
                        .FirstOrDefaultAsync(c => c.Key == config.Key);
                    
                    if (existing != null)
                    {
                        existing.Value = config.Value;
                        existing.UpdatedAt = DateTime.UtcNow;
                        existing.UpdatedBy = User.Identity?.Name;
                    }
                    else
                    {
                        _context.SystemConfigurations.Add(new Domain.Entities.SystemConfiguration
                        {
                            Key = config.Key,
                            Value = config.Value,
                            Description = config.Key switch
                            {
                                "PagBank.AccessToken" => "Token de acesso da API PagBank (Bearer Token)",
                                "PagBank.UseSandbox" => "Usar ambiente sandbox do PagBank (true/false)",
                                _ => ""
                            },
                            Category = "Payment",
                            IsSensitive = config.Key == "PagBank.AccessToken",
                            IsEncrypted = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Configurações do PagBank (Sandbox) atualizadas com sucesso!";
                return RedirectToAction("SystemConfigurations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao configurar PagBank: {ex.Message}";
                return RedirectToAction("SystemConfigurations");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestPagBankConnection()
        {
            try
            {
                // Tentar fazer uma requisição simples para testar a conexão
                // Vamos tentar criar um pedido de teste muito pequeno (R$ 0,01)
                var testOrder = await _pagBankService.CreateOrderAsync(
                    referenceId: $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    amount: 0.01m,
                    method: PaymentMethod.PIX,
                    clientName: "Teste Conexão",
                    clientEmail: "teste@teste.com",
                    clientTaxId: "12345678909"
                );

                TempData["Success"] = $"Conexão com PagBank testada com sucesso! Pedido de teste criado: {testOrder.Id}";
                return RedirectToAction("SystemConfigurations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao testar conexão PagBank: {ex.Message}";
                return RedirectToAction("SystemConfigurations");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Payments(string? search = null, string? status = null, int page = 1, int pageSize = 15)
        {
            var query = _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                .AsQueryable();

            // Filtro de busca
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => 
                    p.ExternalPaymentId.Contains(search) ||
                    (p.TransactionId != null && p.TransactionId.Contains(search)) ||
                    p.Subscription.Contract.ContractNumber.Contains(search) ||
                    (p.Subscription.Contract.Client.RazaoSocial != null && p.Subscription.Contract.Client.RazaoSocial.Contains(search)) ||
                    (p.Subscription.Contract.Client.NomeFantasia != null && p.Subscription.Contract.Client.NomeFantasia.Contains(search)));
            }

            // Filtro por status
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<PaymentStatus>(status, out var paymentStatus))
                {
                    query = query.Where(p => p.Status == paymentStatus);
                }
            }

            // Ordenação
            query = query.OrderByDescending(p => p.CreatedAt);

            // Contagem total para paginação
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Paginação
            var payments = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            return View(payments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int paymentId)
        {
            try
            {
                await _paymentService.ConfirmPaymentAsync(paymentId, Guid.NewGuid().ToString());
                TempData["Success"] = "Pagamento confirmado com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao confirmar pagamento: {ex.Message}";
            }

            return RedirectToAction("Payments");
        }

        [HttpGet]
        public IActionResult DatabaseManager()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInterestsTable()
        {
            try
            {
                // Verificar se a tabela já existe
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                bool tableExists = false;
                try
                {
                    using var checkCommand = connection.CreateCommand();
                    checkCommand.CommandText = @"
                        SELECT COUNT(*) 
                        FROM information_schema.tables 
                        WHERE table_schema = DATABASE() 
                        AND table_name = 'Interests'";
                    
                    var result = await checkCommand.ExecuteScalarAsync();
                    tableExists = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    // Se der erro ao verificar, assume que não existe e tenta criar
                    tableExists = false;
                }

                if (tableExists)
                {
                    TempData["Info"] = "A tabela Interests já existe no banco de dados.";
                    await connection.CloseAsync();
                    return RedirectToAction("DatabaseManager");
                }

                var createTableSql = @"
CREATE TABLE IF NOT EXISTS Interests (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CoworkingAddressId INT NOT NULL,
    AllocationDate DATETIME NULL,
    StartTime TIME NULL,
    EndTime TIME NULL,
    Name VARCHAR(255) NOT NULL,
    LastName VARCHAR(255) NOT NULL,
    Email VARCHAR(255) NOT NULL,
    Phone VARCHAR(50) NULL,
    Status VARCHAR(50) DEFAULT 'Pending',
    Notes TEXT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    ContactedByUserId INT NULL,
    ContactedAt DATETIME NULL,
    FOREIGN KEY (CoworkingAddressId) REFERENCES CoworkingAddresses(Id) ON DELETE CASCADE,
    FOREIGN KEY (ContactedByUserId) REFERENCES Users(Id) ON DELETE SET NULL
);";

                await _context.Database.ExecuteSqlRawAsync(createTableSql);
                await connection.CloseAsync();

                TempData["Success"] = "Tabela Interests criada com sucesso!";
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela Interests: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData["Error"] = errorMessage;
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCoworkingProductsTable()
        {
            try
            {
                // Verificar se a tabela já existe
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                bool tableExists = false;
                try
                {
                    using var checkCommand = connection.CreateCommand();
                    checkCommand.CommandText = @"
                        SELECT COUNT(*) 
                        FROM information_schema.tables 
                        WHERE table_schema = DATABASE() 
                        AND table_name = 'CoworkingProducts'";
                    
                    var result = await checkCommand.ExecuteScalarAsync();
                    tableExists = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    // Se der erro ao verificar, assume que não existe e tenta criar
                    tableExists = false;
                }

                if (tableExists)
                {
                    TempData["Info"] = "A tabela CoworkingProducts já existe no banco de dados.";
                    await connection.CloseAsync();
                    return RedirectToAction("DatabaseManager");
                }

                var createTableSql = @"
CREATE TABLE IF NOT EXISTS CoworkingProducts (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CoworkingId INT NOT NULL,
    ProductType VARCHAR(50) NOT NULL,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    FOREIGN KEY (CoworkingId) REFERENCES Coworkings(Id) ON DELETE CASCADE,
    UNIQUE KEY UK_CoworkingProduct (CoworkingId, ProductType)
);";

                await _context.Database.ExecuteSqlRawAsync(createTableSql);
                await connection.CloseAsync();

                TempData["Success"] = "Tabela CoworkingProducts criada com sucesso!";
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela CoworkingProducts: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData["Error"] = errorMessage;
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteSql(string sqlQuery)
        {
            if (string.IsNullOrWhiteSpace(sqlQuery))
            {
                TempData["Error"] = "A query SQL não pode estar vazia.";
                return RedirectToAction("DatabaseManager");
            }

            // Validações de segurança básicas
            var upperQuery = sqlQuery.ToUpper().Trim();
            
            // Bloquear operações perigosas
            var blockedKeywords = new[] { "DROP DATABASE", "DROP TABLE", "TRUNCATE", "DELETE FROM", "UPDATE", "ALTER DATABASE" };
            if (blockedKeywords.Any(keyword => upperQuery.Contains(keyword)))
            {
                TempData["Error"] = "Operações perigosas não são permitidas. Use apenas ALTER TABLE, CREATE TABLE, ADD COLUMN ou SELECT.";
                return RedirectToAction("DatabaseManager");
            }
            
            // Permitir apenas operações seguras
            var allowedKeywords = new[] { "ALTER TABLE", "CREATE TABLE", "ADD COLUMN", "ADD", "SELECT", "SHOW", "DESCRIBE", "DESC" };
            if (!allowedKeywords.Any(keyword => upperQuery.Contains(keyword)))
            {
                TempData["Error"] = "Apenas operações ALTER TABLE, CREATE TABLE, ADD COLUMN, SELECT, SHOW e DESCRIBE são permitidas.";
                return RedirectToAction("DatabaseManager");
            }

            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = sqlQuery;
                command.CommandTimeout = 300; // 5 minutos

                var result = new System.Text.StringBuilder();
                var rowsAffected = 0;

                // Verificar se é uma query SELECT, SHOW ou DESCRIBE
                if (upperQuery.TrimStart().StartsWith("SELECT") || 
                    upperQuery.TrimStart().StartsWith("SHOW") || 
                    upperQuery.TrimStart().StartsWith("DESCRIBE") ||
                    upperQuery.TrimStart().StartsWith("DESC"))
                {
                    using var reader = await command.ExecuteReaderAsync();
                    
                    if (reader.HasRows)
                    {
                        // Cabeçalho das colunas
                        var columnNames = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columnNames.Add(reader.GetName(i));
                        }
                        result.AppendLine(string.Join(" | ", columnNames));
                        result.AppendLine(new string('-', Math.Min(200, columnNames.Sum(c => c.Length) + (columnNames.Count * 3))));

                        // Dados (limitar a 1000 linhas para não sobrecarregar)
                        int rowCount = 0;
                        while (await reader.ReadAsync() && rowCount < 1000)
                        {
                            var row = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "NULL";
                                // Limitar tamanho de cada campo para não quebrar a visualização
                                if (value.Length > 50)
                                {
                                    value = value.Substring(0, 47) + "...";
                                }
                                row.Add(value);
                            }
                            result.AppendLine(string.Join(" | ", row));
                            rowCount++;
                        }
                        
                        if (rowCount >= 1000)
                        {
                            result.AppendLine($"\n... (limitado a 1000 linhas)");
                        }
                    }
                    else
                    {
                        result.AppendLine("Nenhum resultado encontrado.");
                    }
                }
                else
                {
                    // Para comandos não-SELECT (ALTER, CREATE, etc.)
                    rowsAffected = await command.ExecuteNonQueryAsync();
                    result.AppendLine($"Comando executado com sucesso!");
                    result.AppendLine($"Linhas afetadas: {rowsAffected}");
                }

                await connection.CloseAsync();

                TempData["Success"] = "Query executada com sucesso!";
                TempData["QueryResult"] = result.ToString();
                TempData["ExecutedQuery"] = sqlQuery;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao executar query: {ex.Message}";
                TempData["ExecutedQuery"] = sqlQuery;
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCoworkingRatingsTable()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                bool tableExists = false;
                try
                {
                    using var checkCommand = connection.CreateCommand();
                    checkCommand.CommandText = @"
                        SELECT COUNT(*) 
                        FROM information_schema.tables 
                        WHERE table_schema = DATABASE() 
                        AND table_name = 'CoworkingRatings'";
                    
                    var result = await checkCommand.ExecuteScalarAsync();
                    tableExists = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    tableExists = false;
                }

                if (tableExists)
                {
                    TempData["Info"] = "A tabela CoworkingRatings já existe no banco de dados.";
                    await connection.CloseAsync();
                    return RedirectToAction("DatabaseManager");
                }

                var createTableSql = @"
CREATE TABLE IF NOT EXISTS CoworkingRatings (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CoworkingId INT NOT NULL,
    ClientId INT NULL,
    Rating INT NOT NULL CHECK (Rating >= 1 AND Rating <= 5),
    Comment VARCHAR(1000) NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    FOREIGN KEY (CoworkingId) REFERENCES Coworkings(Id) ON DELETE CASCADE,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE SET NULL
);";

                await _context.Database.ExecuteSqlRawAsync(createTableSql);
                await connection.CloseAsync();

                TempData["Success"] = "Tabela CoworkingRatings criada com sucesso!";
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabela CoworkingRatings: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData["Error"] = errorMessage;
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCreditsTables()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                var createTablesSql = @"
CREATE TABLE IF NOT EXISTS ClientCredits (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ClientId INT NOT NULL,
    Credits INT NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS CreditPlans (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Price DECIMAL(10,2) NOT NULL,
    Credits INT NOT NULL,
    BonusCredits INT DEFAULT 0,
    Period VARCHAR(50) NOT NULL,
    ValidityMonths INT NOT NULL DEFAULT 6,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL
);

CREATE TABLE IF NOT EXISTS CreditPurchases (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ClientId INT NOT NULL,
    CreditPlanId INT NOT NULL,
    AmountPaid DECIMAL(10,2) NOT NULL,
    CreditsReceived INT NOT NULL,
    BonusCredits INT DEFAULT 0,
    Status VARCHAR(50) DEFAULT 'Pending',
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    PaidAt DATETIME NULL,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE CASCADE,
    FOREIGN KEY (CreditPlanId) REFERENCES CreditPlans(Id)
);

CREATE TABLE IF NOT EXISTS Reservations (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ClientId INT NOT NULL,
    CoworkingAddressId INT NOT NULL,
    ReservationDate DATETIME NOT NULL,
    StartTime TIME NULL,
    EndTime TIME NULL,
    CreditsUsed INT NOT NULL,
    Status VARCHAR(50) DEFAULT 'Confirmed',
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CancelledAt DATETIME NULL,
    FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE CASCADE,
    FOREIGN KEY (CoworkingAddressId) REFERENCES CoworkingAddresses(Id) ON DELETE CASCADE
);";

                await _context.Database.ExecuteSqlRawAsync(createTablesSql);
                await connection.CloseAsync();

                TempData["Success"] = "Tabelas de créditos criadas com sucesso!";
            }
            catch (Exception ex)
            {
                var errorMessage = $"Erro ao criar tabelas de créditos: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                TempData["Error"] = errorMessage;
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitializeCreditPlans()
        {
            try
            {
                // Verificar se já existem planos
                var existingPlans = await _context.CreditPlans.AnyAsync();
                if (existingPlans)
                {
                    TempData["Info"] = "Os planos de créditos já foram inicializados.";
                    return RedirectToAction("DatabaseManager");
                }

                // Criar planos mensais
                var plans = new List<Domain.Entities.CreditPlan>
                {
                    new Domain.Entities.CreditPlan
                    {
                        Name = "Básico",
                        Price = 300m,
                        Credits = 300,
                        BonusCredits = 0,
                        Period = "Mensal",
                        ValidityMonths = 6,
                        IsActive = true
                    },
                    new Domain.Entities.CreditPlan
                    {
                        Name = "Profissional",
                        Price = 500m,
                        Credits = 520,
                        BonusCredits = 20,
                        Period = "Mensal",
                        ValidityMonths = 6,
                        IsActive = true
                    },
                    new Domain.Entities.CreditPlan
                    {
                        Name = "Business",
                        Price = 1000m,
                        Credits = 1080,
                        BonusCredits = 80,
                        Period = "Mensal",
                        ValidityMonths = 6,
                        IsActive = true
                    }
                };

                _context.CreditPlans.AddRange(plans);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Planos de créditos inicializados com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao inicializar planos: {ex.Message}";
            }

            return RedirectToAction("DatabaseManager");
        }

        [HttpPost]
        public async Task<IActionResult> AddRoomConfigurationColumns()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                // Verificar e adicionar cada coluna individualmente
                var columns = new[]
                {
                    ("OffersSharedRoom", "BOOLEAN DEFAULT FALSE"),
                    ("SharedRoomPricePerHour", "DECIMAL(10,2) NULL"),
                    ("SharedRoomMaxSeats", "INT NULL"),
                    ("OffersPrivateRoom", "BOOLEAN DEFAULT FALSE"),
                    ("PrivateRoomPricePerHour", "DECIMAL(10,2) NULL"),
                    ("PrivateRoomMaxSeats", "INT NULL")
                };

                foreach (var (columnName, columnType) in columns)
                {
                    try
                    {
                        // Verificar se a coluna já existe
                        using var checkCommand = connection.CreateCommand();
                        checkCommand.CommandText = $@"
                            SELECT COUNT(*) 
                            FROM information_schema.COLUMNS 
                            WHERE TABLE_SCHEMA = DATABASE() 
                            AND TABLE_NAME = 'CoworkingAddresses' 
                            AND COLUMN_NAME = '{columnName}'";
                        
                        var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
                        
                        if (!exists)
                        {
                            using var addCommand = connection.CreateCommand();
                            addCommand.CommandText = $"ALTER TABLE CoworkingAddresses ADD COLUMN {columnName} {columnType}";
                            await addCommand.ExecuteNonQueryAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Ignorar se a coluna já existe ou outro erro específico
                        if (!ex.Message.Contains("Duplicate column name"))
                        {
                            throw;
                        }
                    }
                }

                await connection.CloseAsync();
                TempData["Success"] = "Colunas de configuração de salas adicionadas com sucesso na tabela CoworkingAddresses!";
                return RedirectToAction("DatabaseManager");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao adicionar colunas: {ex.Message}";
                return RedirectToAction("DatabaseManager");
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddReservationColumns()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                var columns = new[]
                {
                    ("RoomType", "VARCHAR(20) DEFAULT 'Shared'"),
                    ("TotalValue", "DECIMAL(10,2) NOT NULL DEFAULT 0")
                };

                foreach (var (columnName, columnType) in columns)
                {
                    try
                    {
                        // Verificar se a coluna já existe
                        using var checkCommand = connection.CreateCommand();
                        checkCommand.CommandText = $@"
                            SELECT COUNT(*) 
                            FROM information_schema.COLUMNS 
                            WHERE TABLE_SCHEMA = DATABASE() 
                            AND TABLE_NAME = 'Reservations' 
                            AND COLUMN_NAME = '{columnName}'";
                        
                        var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
                        
                        if (!exists)
                        {
                            using var addCommand = connection.CreateCommand();
                            addCommand.CommandText = $"ALTER TABLE Reservations ADD COLUMN {columnName} {columnType}";
                            await addCommand.ExecuteNonQueryAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Ignorar se a coluna já existe
                        if (!ex.Message.Contains("Duplicate column name"))
                        {
                            throw;
                        }
                    }
                }

                await connection.CloseAsync();
                TempData["Success"] = "Colunas RoomType e TotalValue adicionadas com sucesso na tabela Reservations!";
                return RedirectToAction("DatabaseManager");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao adicionar colunas: {ex.Message}";
                return RedirectToAction("DatabaseManager");
            }
        }
    }
}
