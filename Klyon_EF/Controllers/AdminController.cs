using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KlyonEF.Services;
using KlyonEF.Models;
using KlyonEF.Data;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace KlyonEF.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly IUserService _userService;
        private readonly ICoworkingService _coworkingService;
        private readonly IContractService _contractService;
        private readonly ApplicationDbContext _context;

        public AdminController(
            IUserService userService,
            ICoworkingService coworkingService,
            IContractService contractService,
            ApplicationDbContext context)
        {
            _userService = userService;
            _coworkingService = coworkingService;
            _contractService = contractService;
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            // KPIs Principais
            var totalUsers = await _context.Users.CountAsync();
            var totalCoworkings = await _context.Coworkings.CountAsync();
            var activeContracts = await _context.Contracts.CountAsync(c => c.Status == ContractStatus.Active);
            var totalRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            // KPIs Adicionais
            var totalLocations = await _context.Locations.CountAsync();
            var pendingPayments = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Pending)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;
            var totalCommissions = await _context.Commissions
                .Where(c => !c.IsTransferred)
                .SumAsync(c => (decimal?)c.Amount) ?? 0;
            var inactiveUsers = await _context.Users.CountAsync(u => !u.IsActive);
            var unverifiedUsers = await _context.Users.CountAsync(u => !u.EmailVerified);

            // Receita dos últimos 6 meses
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var monthlyRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= sixMonthsAgo)
                .GroupBy(p => new { Year = p.PaidAt.Value.Year, Month = p.PaidAt.Value.Month })
                .Select(g => new
                {
                    Month = g.Key.Month,
                    Year = g.Key.Year,
                    Revenue = g.Sum(p => p.Amount)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            // Usuários por tipo
            var usersByRole = await _context.UserRoles
                .Include(ur => ur.Role)
                .GroupBy(ur => ur.Role.Name)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToListAsync();

            // Contratos por status
            var contractsByStatus = await _context.Contracts
                .GroupBy(c => c.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();

            // Top 5 localidades por receita
            var topLocations = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                            .ThenInclude(a => a.Location)
                .Where(p => p.Status == PaymentStatus.Paid)
                .GroupBy(p => p.Subscription.Contract.CoworkingAddress.Location.City)
                .Select(g => new { City = g.Key, Revenue = g.Sum(p => p.Amount) })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            // Pagamentos dos últimos 30 dias
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var recentPayments = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= thirtyDaysAgo)
                .OrderByDescending(p => p.PaidAt)
                .Take(10)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                .ToListAsync();

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalCoworkings = totalCoworkings;
            ViewBag.ActiveContracts = activeContracts;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalLocations = totalLocations;
            ViewBag.PendingPayments = pendingPayments;
            ViewBag.TotalCommissions = totalCommissions;
            ViewBag.InactiveUsers = inactiveUsers;
            ViewBag.UnverifiedUsers = unverifiedUsers;
            ViewBag.MonthlyRevenue = monthlyRevenue;
            ViewBag.UsersByRole = usersByRole;
            ViewBag.ContractsByStatus = contractsByStatus;
            ViewBag.TopLocations = topLocations;
            ViewBag.RecentPayments = recentPayments;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Users(string? search = null, string? role = null, string? status = null, string? emailVerified = null, int page = 1, int pageSize = 15)
        {
            var query = _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .AsQueryable();

            // Filtro de busca (nome ou email)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
            }

            // Filtro por role
            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == role));
            }

            // Filtro por status (ativo/inativo)
            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status == "active";
                query = query.Where(u => u.IsActive == isActive);
            }

            // Filtro por email verificado
            if (!string.IsNullOrEmpty(emailVerified))
            {
                bool verified = emailVerified == "verified";
                query = query.Where(u => u.EmailVerified == verified);
            }

            // Ordenação
            query = query.OrderByDescending(u => u.CreatedAt);

            // Contagem total para paginação
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Paginação
            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            ViewBag.Search = search;
            ViewBag.SelectedRole = role;
            ViewBag.Status = status;
            ViewBag.EmailVerified = emailVerified;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            // Lista de roles disponíveis para o filtro
            ViewBag.AvailableRoles = await _context.Roles.Select(r => r.Name).ToListAsync();

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
        public async Task<IActionResult> EditUser(int id, string fullName, string email, string? phone, string isActive, int[]? roleIds)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.FullName = fullName;
            user.Email = email;
            user.Phone = phone;
            // Converter string para bool (checkbox envia "true" quando marcado, "false" quando desmarcado via hidden)
            user.IsActive = isActive == "true";

            await _userService.UpdateAsync(user);

            // Atualizar roles
            var currentRoles = user.UserRoles.ToList();
            foreach (var currentRole in currentRoles)
            {
                if (roleIds == null || !roleIds.Contains(currentRole.RoleId))
                {
                    _context.UserRoles.Remove(currentRole);
                }
            }

            if (roleIds != null)
            {
                foreach (var roleId in roleIds)
                {
                    if (!currentRoles.Any(ur => ur.RoleId == roleId))
                    {
                        _context.UserRoles.Add(new Models.UserRole
                        {
                            UserId = id,
                            RoleId = roleId
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> VerifyUserEmail(int id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.EmailVerified = true;
            user.EmailVerificationToken = null;
            await _userService.UpdateAsync(user);

            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> ResetUserPassword(int userId, string newPassword, string confirmNewPassword)
        {
            try
            {
                var user = await _userService.GetByIdAsync(userId);
                if (user == null)
                {
                    TempData["Error"] = "Usuário não encontrado.";
                    return RedirectToAction("Users");
                }

                if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
                {
                    TempData["Error"] = "A senha deve ter no mínimo 6 caracteres.";
                    return RedirectToAction("ViewUser", new { id = userId });
                }

                if (newPassword != confirmNewPassword)
                {
                    TempData["Error"] = "As senhas não coincidem.";
                    return RedirectToAction("ViewUser", new { id = userId });
                }

                // Obter o tipo de usuário para mensagem personalizada
                var userRoles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
                var userType = userRoles.Contains("CoworkingPartner") ? "Coworking Partner" :
                               userRoles.Contains("Administrator") ? "Administrador" :
                               userRoles.Contains("Client") ? "Cliente" : "Usuário";

                // Gerar novo hash da senha usando BCrypt
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
                
                // Atualizar diretamente no contexto para garantir que o hash seja salvo corretamente
                _context.Entry(user).Property(u => u.PasswordHash).IsModified = true;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Senha do {userType} <strong>{user.Email}</strong> resetada com sucesso! A nova senha já está ativa.";
                return RedirectToAction("ViewUser", new { id = userId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao resetar senha: {ex.Message}";
                return RedirectToAction("ViewUser", new { id = userId });
            }
        }

        // Endpoint temporário para corrigir hash corrompido via URL
        // ATENÇÃO: Remover [AllowAnonymous] após corrigir o problema
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> FixPasswordHash(string email, string newPassword = "password")
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    return Content($"<h1>Erro</h1><p>Usuário não encontrado: {email}</p>", "text/html");
                }

                // Gerar hash válido usando BCrypt
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
                await _context.SaveChangesAsync();

                // Verificar se o hash foi salvo corretamente
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
            var query = _context.Coworkings
                .Include(c => c.Addresses)
                .AsQueryable();

            // Filtro de busca (nome, CNPJ ou email)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => 
                    c.Name.Contains(search) || 
                    c.CNPJ.Contains(search) || 
                    c.ContactEmail.Contains(search));
            }

            // Filtro por status (ativo/inativo)
            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status == "active";
                query = query.Where(c => c.IsActive == isActive);
            }

            // Filtro por parceiro
            if (!string.IsNullOrEmpty(isPartner))
            {
                bool partner = isPartner == "true";
                query = query.Where(c => c.IsPartner == partner);
            }

            // Ordenação
            query = query.OrderByDescending(c => c.CreatedAt);

            // Contagem total para paginação
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Paginação
            var coworkings = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
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
        public async Task<IActionResult> Contracts(string? search = null, string? status = null, int? clientId = null, int page = 1, int pageSize = 15)
        {
            var query = _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Location)
                .AsQueryable();

            // Filtro de busca (número do contrato, cliente ou endereço)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => 
                    c.ContractNumber.Contains(search) || 
                    (c.Client != null && (c.Client.RazaoSocial.Contains(search) || c.Client.NomeFantasia.Contains(search))) ||
                    (c.CoworkingAddress != null && (c.CoworkingAddress.Address.Contains(search) || c.CoworkingAddress.City.Contains(search))));
            }

            // Filtro por status
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<ContractStatus>(status, out var contractStatus))
                {
                    query = query.Where(c => c.Status == contractStatus);
                }
            }

            // Filtro por cliente
            if (clientId.HasValue && clientId.Value > 0)
            {
                query = query.Where(c => c.ClientId == clientId.Value);
            }

            // Ordenação
            query = query.OrderByDescending(c => c.CreatedAt);

            // Contagem total para paginação
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Paginação
            var contracts = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.ClientId = clientId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            // Lista de clientes para o filtro
            var clients = await _context.Clients
                .OrderBy(c => c.RazaoSocial ?? c.NomeFantasia)
                .ToListAsync();
            ViewBag.Clients = clients;

            return View(contracts);
        }

        [HttpGet]
        public async Task<IActionResult> Locations(string? search = null, string? state = null, string? region = null, string? status = null, int page = 1, int pageSize = 12)
        {
            var query = _context.Locations.AsQueryable();

            // Filtros
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(l => l.City.Contains(search) || l.State.Contains(search));
            }

            if (!string.IsNullOrEmpty(state))
            {
                query = query.Where(l => l.State == state);
            }

            if (!string.IsNullOrEmpty(region))
            {
                query = query.Where(l => l.Region == region);
            }

            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status == "active";
                query = query.Where(l => l.IsActive == isActive);
            }

            // Ordenação
            query = query.OrderBy(l => l.State).ThenBy(l => l.City);

            // Contagem total para paginação
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Paginação
            var locations = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Buscar comissões ativas
            var commissions = await _context.LocationCommissions
                .Where(lc => lc.IsActive)
                .ToListAsync();

            ViewBag.Commissions = commissions;
            ViewBag.Search = search;
            ViewBag.State = state;
            ViewBag.Region = region;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            // Listas para filtros
            ViewBag.States = await _context.Locations.Select(l => l.State).Distinct().OrderBy(s => s).ToListAsync();
            ViewBag.Regions = await _context.Locations.Where(l => l.Region != null).Select(l => l.Region!).Distinct().OrderBy(r => r).ToListAsync();

            return View(locations);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateLocationCommission(int locationId, decimal commissionPercentage)
        {
            try
            {
                if (locationId <= 0)
                {
                    TempData["Error"] = "Localidade inválida.";
                    return RedirectToAction("Locations");
                }

                if (commissionPercentage < 0 || commissionPercentage > 100)
                {
                    TempData["Error"] = "A comissão deve estar entre 0% e 100%.";
                    return RedirectToAction("Locations");
                }

                // Desativar comissões ativas existentes para esta localidade
                var existing = await _context.LocationCommissions
                    .Where(lc => lc.LocationId == locationId && lc.IsActive)
                    .ToListAsync();

                foreach (var comm in existing)
                {
                    comm.IsActive = false;
                    comm.EndDate = DateTime.UtcNow;
                }

                // Criar nova comissão
                var newCommission = new LocationCommission
                {
                    LocationId = locationId,
                    CommissionPercentage = commissionPercentage,
                    IsActive = true,
                    EffectiveDate = DateTime.UtcNow
                };

                _context.LocationCommissions.Add(newCommission);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Comissão atualizada para {commissionPercentage:F1}% com sucesso!";
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
            ViewBag.BrazilianStates = new List<string>
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
        public async Task<IActionResult> CreateLocation(string city, string state, string? region, string? isActive, decimal commissionPercentage = 15.0m)
        {
            try
            {
                if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(state))
                {
                    TempData["Error"] = "Cidade e Estado são obrigatórios.";
                    return RedirectToAction("CreateLocation");
                }

                // Verificar se já existe localidade com mesma cidade e estado
                var existing = await _context.Locations
                    .FirstOrDefaultAsync(l => l.City.ToLower() == city.ToLower() && l.State.ToUpper() == state.ToUpper());

                if (existing != null)
                {
                    TempData["Error"] = $"Já existe uma localidade cadastrada para {city}/{state}.";
                    return RedirectToAction("CreateLocation");
                }

                // Criar nova localidade
                var location = new Location
                {
                    City = city,
                    State = state.ToUpper(),
                    Region = region,
                    IsActive = !string.IsNullOrEmpty(isActive) && isActive.ToLower() == "true"
                };

                _context.Locations.Add(location);
                await _context.SaveChangesAsync();

                // Criar comissão inicial
                var commission = new LocationCommission
                {
                    LocationId = location.Id,
                    CommissionPercentage = commissionPercentage,
                    IsActive = true,
                    EffectiveDate = DateTime.UtcNow
                };

                _context.LocationCommissions.Add(commission);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Localidade {city}/{state} cadastrada com sucesso!";
                return RedirectToAction("Locations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao cadastrar localidade: {ex.Message}";
                return RedirectToAction("CreateLocation");
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditLocation(int id)
        {
            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                TempData["Error"] = "Localidade não encontrada.";
                return RedirectToAction("Locations");
            }

            // Lista de estados brasileiros
            ViewBag.BrazilianStates = new List<string>
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
        public async Task<IActionResult> EditLocation(int id, string city, string state, string? region, string? isActive)
        {
            try
            {
                var location = await _context.Locations.FindAsync(id);
                if (location == null)
                {
                    TempData["Error"] = "Localidade não encontrada.";
                    return RedirectToAction("Locations");
                }

                // Verificar se já existe outra localidade com mesma cidade e estado
                var existing = await _context.Locations
                    .FirstOrDefaultAsync(l => l.City.ToLower() == city.ToLower() 
                        && l.State.ToUpper() == state.ToUpper() 
                        && l.Id != id);

                if (existing != null)
                {
                    TempData["Error"] = $"Já existe uma localidade cadastrada para {city}/{state}.";
                    return RedirectToAction("EditLocation", new { id });
                }

                location.City = city;
                location.State = state.ToUpper();
                location.Region = region;
                location.IsActive = !string.IsNullOrEmpty(isActive) && isActive.ToLower() == "true";

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Localidade {city}/{state} atualizada com sucesso!";
                return RedirectToAction("Locations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao atualizar localidade: {ex.Message}";
                return RedirectToAction("EditLocation", new { id });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            try
            {
                var location = await _context.Locations
                    .Include(l => l.LocationCommissions)
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (location == null)
                {
                    TempData["Error"] = "Localidade não encontrada.";
                    return RedirectToAction("Locations");
                }

                // Verificar se há endereços vinculados
                var hasAddresses = await _context.CoworkingAddresses
                    .AnyAsync(a => a.LocationId == id);

                if (hasAddresses)
                {
                    TempData["Error"] = "Não é possível excluir localidade com endereços vinculados. Desative a localidade ao invés de excluí-la.";
                    return RedirectToAction("Locations");
                }

                // Remover comissões
                _context.LocationCommissions.RemoveRange(location.LocationCommissions);
                _context.Locations.Remove(location);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Localidade excluída com sucesso!";
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

            var payments = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                            .ThenInclude(a => a.Location)
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= startDate && p.PaidAt <= endDate)
                .ToListAsync();

            ViewBag.Payments = payments;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View();
        }
    }
}
