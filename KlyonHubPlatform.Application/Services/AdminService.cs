using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Security.Claims;
namespace KlyonHubPlatform.Application.Services
{
    public class AdminService : IAdminService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService? _auditService;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        public AdminService(ApplicationDbContext context, IUserService userService, IAuditService? auditService = null, IHttpContextAccessor? httpContextAccessor = null)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
            _httpContextAccessor = httpContextAccessor;
        }
        private async Task LogAuditAsync(int? userId, string action, string entityType, int? entityId, object? oldValues = null, object? newValues = null)
        {
            if (_auditService == null) return;
            string? ipAddress = null;
            string? userAgent = null;
            if (_httpContextAccessor?.HttpContext != null)
            {
                ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();
                userAgent = _httpContextAccessor.HttpContext.Request.Headers[HttpHeaderNames.UserAgent].ToString();
            }
            string? oldValuesJson = oldValues != null ? JsonSerializer.Serialize(oldValues) : null;
            string? newValuesJson = newValues != null ? JsonSerializer.Serialize(newValues) : null;
            await _auditService.LogActionAsync(userId, action, entityType, entityId, oldValuesJson, newValuesJson, ipAddress, userAgent);
        }
        private int? GetCurrentUserId()
        {
            if (_httpContextAccessor?.HttpContext?.User != null)
            {
                var userIdClaim = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    return userId;
                }
            }
            return null;
        }
        public async Task ResetUserPasswordAsync(int userId, string newPassword, string confirmNewPassword)
        {
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                throw new ArgumentException("A senha deve ter no mínimo 6 caracteres.");
            }
            if (newPassword != confirmNewPassword)
            {
                throw new ArgumentException("As senhas não coincidem.");
            }
            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Usuário não encontrado.");
            }
            var oldPasswordHash = user.PasswordHash;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
            _context.Entry(user).Property(u => u.PasswordHash).IsModified = true;
            await _context.SaveChangesAsync();
            await LogAuditAsync(
                GetCurrentUserId(),
                "Senha resetada (ação administrativa)",
                "User",
                userId,
                new { PasswordHashChanged = true },
                new { PasswordHashChanged = true, UserEmail = user.Email, UserFullName = user.FullName }
            );
        }
        public async Task VerifyUserEmailAsync(int userId)
        {
            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Usuário não encontrado.");
            }
            var oldEmailVerified = user.EmailVerified;
            user.EmailVerified = true;
            user.EmailVerificationToken = null;
            await _userService.UpdateAsync(user);
            await LogAuditAsync(
                GetCurrentUserId(),
                "Email verificado (ação administrativa)",
                "User",
                userId,
                new { EmailVerified = oldEmailVerified },
                new { EmailVerified = user.EmailVerified, UserEmail = user.Email, UserFullName = user.FullName }
            );
        }
        public async Task<List<User>> GetUsersFilteredAsync(string? search = null, string? role = null, string? status = null, string? emailVerified = null, int page = 1, int pageSize = 15)
        {
            var query = BuildUsersFilterQuery(search, role, status, emailVerified);
            query = query.OrderByDescending(u => u.CreatedAt);
            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        public async Task<int> GetUsersFilteredCountAsync(string? search = null, string? role = null, string? status = null, string? emailVerified = null)
        {
            var query = BuildUsersFilterQuery(search, role, status, emailVerified);
            return await query.CountAsync();
        }
        private IQueryable<User> BuildUsersFilterQuery(string? search, string? role, string? status, string? emailVerified)
        {
            IQueryable<User> query = _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role);
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
            }
            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == role));
            }
            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status == StatusFilterValues.Active;
                query = query.Where(u => u.IsActive == isActive);
            }
            if (!string.IsNullOrEmpty(emailVerified))
            {
                bool verified = emailVerified == "verified";
                query = query.Where(u => u.EmailVerified == verified);
            }
            return query;
        }
        public async Task<List<string>> GetAvailableRolesAsync()
        {
            return await _context.Roles.AsNoTracking().Select(r => r.Name).ToListAsync();
        }
        public async Task<List<Coworking>> GetCoworkingsFilteredAsync(string? search = null, string? status = null, string? isPartner = null, string? approvalStatus = null, int page = 1, int pageSize = 15)
        {
            var query = BuildCoworkingsFilterQuery(search, status, isPartner, approvalStatus);
            query = query.OrderByDescending(c => c.SubmittedAt ?? c.CreatedAt);
            return await query
                .Include(c => c.Addresses)
                .Include(c => c.User)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        public async Task<int> GetCoworkingsFilteredCountAsync(string? search = null, string? status = null, string? isPartner = null, string? approvalStatus = null)
        {
            var query = BuildCoworkingsFilterQuery(search, status, isPartner, approvalStatus);
            return await query.CountAsync();
        }
        private IQueryable<Coworking> BuildCoworkingsFilterQuery(string? search, string? status, string? isPartner, string? approvalStatus)
        {
            var query = _context.Coworkings.AsNoTracking();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => 
                    (c.Name != null && c.Name.Contains(search)) || 
                    (c.CNPJ != null && c.CNPJ.Contains(search)) || 
                    (c.ContactEmail != null && c.ContactEmail.Contains(search)));
            }
            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status == StatusFilterValues.Active;
                query = query.Where(c => c.IsActive == isActive);
            }
            if (!string.IsNullOrEmpty(isPartner))
            {
                bool partner = KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.IsTrue(isPartner);
                query = query.Where(c => c.IsPartner == partner);
            }
            if (!string.IsNullOrEmpty(approvalStatus) && Enum.TryParse<CoworkingApprovalStatus>(approvalStatus, out var appStatus))
            {
                query = query.Where(c => c.ApprovalStatus == appStatus);
            }
            return query;
        }
        public async Task<List<Contract>> GetContractsFilteredAsync(string? search = null, string? status = null, int? clientId = null, int page = 1, int pageSize = 15)
        {
            var query = BuildContractsFilterQuery(search, status, clientId);
            query = query.OrderByDescending(c => c.CreatedAt);
            return await query
                .Include(c => c.Client!)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a!.Location)
                .Include(c => c.Subscription)
                    .ThenInclude(s => s!.Payments)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        public async Task<int> GetContractsFilteredCountAsync(string? search = null, string? status = null, int? clientId = null)
        {
            var query = BuildContractsFilterQuery(search, status, clientId);
            return await query.CountAsync();
        }
        private IQueryable<Contract> BuildContractsFilterQuery(string? search, string? status, int? clientId)
        {
            var query = _context.Contracts.AsNoTracking();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => 
                    (c.ContractNumber != null && c.ContractNumber.Contains(search)) || 
                    (c.Client != null && (c.Client.RazaoSocial != null && c.Client.RazaoSocial.Contains(search) || c.Client.NomeFantasia != null && c.Client.NomeFantasia.Contains(search))) ||
                    (c.CoworkingAddress != null && (c.CoworkingAddress.Address != null && c.CoworkingAddress.Address.Contains(search) || c.CoworkingAddress.City != null && c.CoworkingAddress.City.Contains(search))));
            }
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<ContractStatus>(status, out var contractStatus))
                {
                    query = query.Where(c => c.Status == contractStatus);
                }
            }
            if (clientId.HasValue && clientId.Value > 0)
            {
                query = query.Where(c => c.ClientId == clientId.Value);
            }
            return query;
        }
        public async Task<List<Client>> GetAllClientsAsync()
        {
            return await _context.Clients
                .AsNoTracking()
                .OrderBy(c => c.RazaoSocial ?? c.NomeFantasia)
                .ToListAsync();
        }

        public async Task<List<CardPriceRequestListItemDto>> GetPendingCardPriceRequestsAsync(CancellationToken cancellationToken = default)
        {
            var list = await _context.CardPriceChangeRequests
                .AsNoTracking()
                .Include(r => r.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                .Where(r => r.Status == CapacityApprovalStatus.Pending)
                .OrderBy(r => r.CreatedAt)
                .Select(r => new CardPriceRequestListItemDto
                {
                    Id = r.Id,
                    CoworkingName = r.CoworkingAddress.Coworking.Name ?? "",
                    CoworkingId = r.CoworkingAddress.CoworkingId,
                    AddressLabel = r.CoworkingAddress.Address + ", " + r.CoworkingAddress.Number + ", " + r.CoworkingAddress.City + "/" + r.CoworkingAddress.State,
                    RequestedCardPrice1x = r.RequestedCardPrice1x,
                    RequestedCardPriceMonthly = r.RequestedCardPriceMonthly,
                    RequestedCardPrice12x = r.RequestedCardPrice12x,
                    PreviousCardPrice1x = r.PreviousCardPrice1x,
                    PreviousCardPriceMonthly = r.PreviousCardPriceMonthly,
                    PreviousCardPrice12x = r.PreviousCardPrice12x,
                    RequestedBasePrice = r.RequestedBasePrice,
                    PreviousBasePrice = r.PreviousBasePrice,
                    RequestedCardMonthlySurchargePercent = r.RequestedCardMonthlySurchargePercent,
                    PreviousCardMonthlySurchargePercent = r.PreviousCardMonthlySurchargePercent,
                    CreatedAtFormatted = r.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync(cancellationToken);
            return list;
        }

        public async Task<CardPriceRequestDetailDto?> GetCardPriceRequestDetailAsync(int id, CancellationToken cancellationToken = default)
        {
            var r = await _context.CardPriceChangeRequests
                .AsNoTracking()
                .Include(req => req.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                .FirstOrDefaultAsync(req => req.Id == id, cancellationToken);
            if (r == null) return null;
            var addr = r.CoworkingAddress;
            return new CardPriceRequestDetailDto
            {
                Id = r.Id,
                Status = r.Status,
                CoworkingName = addr.Coworking?.Name ?? "",
                CoworkingId = addr.CoworkingId,
                AddressLabel = addr.Address + ", " + addr.Number + ", " + addr.City + "/" + addr.State,
                RequestedCardPrice1x = r.RequestedCardPrice1x,
                RequestedCardPriceMonthly = r.RequestedCardPriceMonthly,
                RequestedCardPrice12x = r.RequestedCardPrice12x,
                PreviousCardPrice1x = r.PreviousCardPrice1x,
                PreviousCardPriceMonthly = r.PreviousCardPriceMonthly,
                PreviousCardPrice12x = r.PreviousCardPrice12x,
                RequestedBasePrice = r.RequestedBasePrice,
                PreviousBasePrice = r.PreviousBasePrice,
                RequestedCardMonthlySurchargePercent = r.RequestedCardMonthlySurchargePercent,
                PreviousCardMonthlySurchargePercent = r.PreviousCardMonthlySurchargePercent,
                CreatedAtFormatted = r.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            };
        }

        public async Task FixUserPasswordByEmailAsync(string email, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) throw new KeyNotFoundException("Usuário não encontrado.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            _context.Entry(user).Property(u => u.PasswordHash).IsModified = true;
            await _context.SaveChangesAsync();
        }

        public async Task<(Dictionary<int, int> ServicesCount, Dictionary<int, bool> HasPendingPercentages, Dictionary<int, bool> HasMaxCapacity)> GetCoworkingListStatsAsync(IEnumerable<int> coworkingIds, bool filterCapacityAlert, CancellationToken cancellationToken = default)
        {
            var ids = coworkingIds.ToList();
            var servicesCount = new Dictionary<int, int>();
            var hasPendingPercentages = new Dictionary<int, bool>();
            var hasMaxCapacity = new Dictionary<int, bool>();
            if (ids.Count == 0)
                return (servicesCount, hasPendingPercentages, hasMaxCapacity);

            servicesCount = await _context.PartnerServices
                .AsNoTracking()
                .Where(ps => ids.Contains(ps.CoworkingId))
                .GroupBy(ps => ps.CoworkingId)
                .Select(g => new { CoworkingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CoworkingId, x => x.Count, cancellationToken);

            var coworkingsWithPendingPercentages = await _context.PartnerServices
                .AsNoTracking()
                .Where(ps => ids.Contains(ps.CoworkingId) && ps.Percentage <= 0)
                .Select(ps => ps.CoworkingId)
                .Distinct()
                .ToListAsync(cancellationToken);
            hasPendingPercentages = ids.ToDictionary(id => id, id => coworkingsWithPendingPercentages.Contains(id));
            hasMaxCapacity = ids.ToDictionary(id => id, _ => false);

            return (servicesCount, hasPendingPercentages, hasMaxCapacity);
        }

        public async Task<Coworking?> GetCoworkingWithDetailsForViewAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Coworkings
                .Include(c => c.User)
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.Location)
                .Include(c => c.Addresses)
                    .ThenInclude(a => a.CardPriceChangeRequests)
                .Include(c => c.Photos)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<List<Service>> GetServicesForAdminCoworkingViewAsync(bool showCoworkingMenus, CancellationToken cancellationToken = default)
        {
            IQueryable<Service> query = _context.Services.AsNoTracking().Where(s => s.IsActive);
            if (!showCoworkingMenus)
            {
                var coworkingServiceNames = await _context.Services
                    .AsNoTracking()
                    .Where(x => !x.Code.Contains(ServiceCodes.FiscalAddressCodePrefix))
                    .Select(x => x.Name)
                    .ToListAsync(cancellationToken);
                query = query.Where(s => !coworkingServiceNames.Contains(s.Name));
            }
            return await query.OrderBy(s => s.DisplayOrder).ToListAsync(cancellationToken);
        }

        public async Task<List<PartnerService>> GetPartnerServicesForCoworkingAsync(int coworkingId, bool showCoworkingMenus, CancellationToken cancellationToken = default)
        {
            var partnerServicesQuery = _context.PartnerServices
                .AsNoTracking()
                .Include(ps => ps.Service)
                .Where(ps => ps.CoworkingId == coworkingId && ps.Service != null && ps.Service.IsActive);
            if (!showCoworkingMenus)
            {
                var coworkingServiceNames = await _context.Services
                    .AsNoTracking()
                    .Where(x => !x.Code.Contains(ServiceCodes.FiscalAddressCodePrefix))
                    .Select(x => x.Name)
                    .ToListAsync(cancellationToken);
                partnerServicesQuery = partnerServicesQuery.Where(ps => ps.Service != null && !coworkingServiceNames.Contains(ps.Service.Name));
            }
            return await partnerServicesQuery.ToListAsync(cancellationToken);
        }
    }
}