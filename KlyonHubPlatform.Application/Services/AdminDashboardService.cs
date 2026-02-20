using AutoMapper;
using KlyonHubPlatform.Application.DTOs.Dashboard;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.ViewModels;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IReportService _reportService;
        private readonly ISystemConfigurationService _systemConfigurationService;
        private readonly ILogger<AdminDashboardService> _logger;
        private readonly IMapper _mapper;

        public AdminDashboardService(
            ApplicationDbContext context,
            IReportService reportService,
            ISystemConfigurationService systemConfigurationService,
            ILogger<AdminDashboardService> logger,
            IMapper mapper)
        {
            _context = context;
            _reportService = reportService;
            _systemConfigurationService = systemConfigurationService;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task<DashboardListResult?> GetDashboardListAsync(string type, string? filter, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var data = new List<object>();
            int? totalCount = null;
            switch (type.ToLower())
            {
                case "users":
                    var usersQuery = _context.Users.AsNoTracking().AsQueryable();
                    if (filter == "inactive") usersQuery = usersQuery.Where(u => !u.IsActive);
                    else if (filter == "unverified") usersQuery = usersQuery.Where(u => !u.EmailVerified);
                    totalCount = await usersQuery.CountAsync(cancellationToken);
                    pageSize = Math.Clamp(pageSize, 1, 100);
                    page = Math.Max(1, page);
                    var users = await usersQuery.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
                    data = _mapper.Map<List<UserDashboardItemDto>>(users).Cast<object>().ToList();
                    break;
                case "coworkings":
                    var coworkings = await _context.Coworkings.AsNoTracking().OrderByDescending(c => c.CreatedAt).Take(50).ToListAsync(cancellationToken);
                    data = _mapper.Map<List<CoworkingDashboardItemDto>>(coworkings).Cast<object>().ToList();
                    break;
                case "contracts":
                    var contractsQuery = _context.Contracts.AsNoTracking().Include(c => c.Client).Include(c => c.CoworkingAddress).ThenInclude(a => a!.Location).AsQueryable();
                    if (filter == "Active") contractsQuery = contractsQuery.Where(c => c.Status == ContractStatus.Active);
                    var contracts = await contractsQuery.OrderByDescending(c => c.CreatedAt).Take(50).ToListAsync(cancellationToken);
                    data = _mapper.Map<List<ContractDashboardItemDto>>(contracts).Cast<object>().ToList();
                    break;
                case "locations":
                    var locations = await _context.Locations.AsNoTracking().OrderBy(l => l.State).ThenBy(l => l.City).Take(50).ToListAsync(cancellationToken);
                    data = _mapper.Map<List<LocationDashboardItemDto>>(locations).Cast<object>().ToList();
                    break;
                case "payments":
                    var paymentsQuery = _context.Payments.AsNoTracking().Include(p => p.Subscription!).ThenInclude(s => s.Contract!).ThenInclude(c => c!.Client).AsQueryable();
                    if (filter == "Pending") paymentsQuery = paymentsQuery.Where(p => p.Status == PaymentStatus.Pending);
                    var payments = await paymentsQuery.OrderByDescending(p => p.CreatedAt).Take(50).ToListAsync(cancellationToken);
                    data = _mapper.Map<List<PaymentDashboardItemDto>>(payments).Cast<object>().ToList();
                    break;
                case "reservations":
                    var reservations = await _context.Reservations.AsNoTracking().Include(r => r.Client).Include(r => r.CoworkingAddress!).ThenInclude(a => a.Coworking).OrderByDescending(r => r.CreatedAt).Take(50).ToListAsync(cancellationToken);
                    data = _mapper.Map<List<ReservationDashboardItemDto>>(reservations).Cast<object>().ToList();
                    break;
                default:
                    return null;
            }
            return new DashboardListResult { Data = data, TotalCount = totalCount };
        }

        public async Task<AdminDashboardViewModel> GetDashboardViewModelAsync(CancellationToken cancellationToken = default)
        {
            var dashboardData = await _reportService.GetDashboardDataAsync();
            var model = new AdminDashboardViewModel { DashboardData = dashboardData };
            await FillPendingMigrationsAsync(model, cancellationToken).ConfigureAwait(false);
            await FillProductsAndReservationsAsync(model, cancellationToken).ConfigureAwait(false);
            await FillPartnerServiceStatsAsync(model, cancellationToken).ConfigureAwait(false);
            await FillPendingRemovalRequestsAsync(model, cancellationToken).ConfigureAwait(false);
            await FillAdminAlertsAsync(model, cancellationToken).ConfigureAwait(false);
            return model;
        }
        private async Task FillPendingMigrationsAsync(AdminDashboardViewModel model, CancellationToken cancellationToken)
        {
            try
            {
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
                model.PendingMigrationsCount = pendingMigrations.Count();
                model.PendingMigrations = pendingMigrations.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar migrações pendentes do dashboard");
                model.PendingMigrationsCount = 0;
                model.PendingMigrations = new List<string>();
            }
        }
        private async Task FillProductsAndReservationsAsync(AdminDashboardViewModel model, CancellationToken cancellationToken)
        {
            try
            {
                var productsStats = await _context.CoworkingProducts
                    .AsNoTracking()
                    .Where(p => p.IsActive)
                    .GroupBy(p => p.ProductType)
                    .Select(g => new ProductTypeCountDto { ProductType = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                model.ProductsByType = productsStats;
                var enableCoworkingMenus = await _systemConfigurationService.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey(), cancellationToken).ConfigureAwait(false);
                var showCoworkingMenus = StringBooleanValue.IsTrue(enableCoworkingMenus);
                if (showCoworkingMenus)
                {
                    model.TotalReservations = await _context.Reservations.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false);
                    model.ReservationsByStatus = await _context.Reservations
                        .AsNoTracking()
                        .GroupBy(r => r.Status)
                        .Select(g => new ReservationStatusCountDto { Status = g.Key.ToString(), Count = g.Count() })
                        .ToListAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    model.TotalReservations = 0;
                    model.ReservationsByStatus = new List<ReservationStatusCountDto>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar produtos e reservas do dashboard");
                model.ProductsByType = new List<ProductTypeCountDto>();
                model.TotalReservations = 0;
                model.ReservationsByStatus = new List<ReservationStatusCountDto>();
            }
        }
        private async Task FillPartnerServiceStatsAsync(AdminDashboardViewModel model, CancellationToken cancellationToken)
        {
            try
            {
                var partnersWithoutPercentage = await _context.PartnerServices
                    .AsNoTracking()
                    .Where(ps => ps.Percentage == 0)
                    .Select(ps => ps.CoworkingId)
                    .Distinct()
                    .CountAsync(cancellationToken).ConfigureAwait(false);
                var servicesAwaitingPercentage = await _context.PartnerServices
                    .AsNoTracking()
                    .Where(ps => ps.Percentage == 0)
                    .CountAsync(cancellationToken).ConfigureAwait(false);
                model.PartnersWithoutPercentage = partnersWithoutPercentage;
                model.ServicesAwaitingPercentage = servicesAwaitingPercentage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar estatísticas de parceiros do dashboard");
                model.PartnersWithoutPercentage = 0;
                model.ServicesAwaitingPercentage = 0;
            }
        }
        private async Task FillPendingRemovalRequestsAsync(AdminDashboardViewModel model, CancellationToken cancellationToken)
        {
            try
            {
                model.PendingRemovalRequests = await _context.PartnerServices
                    .AsNoTracking()
                    .Where(ps => ps.RemovalRequestStatus == RemovalRequestStatus.Pending)
                    .CountAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar solicitações de remoção pendentes do dashboard");
                model.PendingRemovalRequests = 0;
            }
        }
        private async Task FillAdminAlertsAsync(AdminDashboardViewModel model, CancellationToken cancellationToken)
        {
            var alerts = new List<AdminAlertDto>();
            await AddPendingCommissionsAlertAsync(alerts, cancellationToken).ConfigureAwait(false);
            await AddOverduePaymentsAlertAsync(alerts, cancellationToken).ConfigureAwait(false);
            await AddCapacityAlertAsync(alerts, cancellationToken).ConfigureAwait(false);
            await AddCoworkingsUnderReviewAlertAsync(alerts, cancellationToken).ConfigureAwait(false);
            await AddPaymentsAwaitingValidationAlertAsync(alerts, cancellationToken).ConfigureAwait(false);
            await AddPendingCardPriceAlertAsync(alerts, cancellationToken).ConfigureAwait(false);
            model.AdminAlerts = alerts;
        }
        private async Task AddPendingCommissionsAlertAsync(List<AdminAlertDto> alerts, CancellationToken cancellationToken)
        {
            try
            {
                var commissionAlertDays = await _systemConfigurationService.GetValueAsync(AlertsConfigKey.CommissionPendingDays.ToConfigKey(), cancellationToken).ConfigureAwait(false);
                var daysThreshold = int.TryParse(commissionAlertDays, out var parsedDays) ? parsedDays : 30;
                var thresholdDate = DateTime.UtcNow.AddDays(-daysThreshold);
                var pendingCommissionsCount = await _context.Commissions
                    .AsNoTracking()
                    .Where(c => !c.IsTransferred && c.CreatedAt < thresholdDate)
                    .CountAsync(cancellationToken).ConfigureAwait(false);
                if (pendingCommissionsCount > 0)
                {
                    var pendingCommissionsAmount = await _context.Commissions
                        .AsNoTracking()
                        .Where(c => !c.IsTransferred && c.CreatedAt < thresholdDate)
                        .SumAsync(c => c.Amount, cancellationToken).ConfigureAwait(false);
                    alerts.Add(new AdminAlertDto
                    {
                        Type = BootstrapBadgeCss.Warning,
                        Icon = "bi-exclamation-triangle-fill",
                        Title = $"Comissões Pendentes há mais de {daysThreshold} dias",
                        Message = $"{pendingCommissionsCount} comissão(ões) pendente(s) há mais de {daysThreshold} dias. Valor total: R$ {pendingCommissionsAmount:N2}",
                        Count = pendingCommissionsCount,
                        ActionKey = null,
                        ActionController = AdminDashboardRouteNames.Controller,
                        ActionName = AdminDashboardRouteNames.CommissionFiscalNotes,
                        ActionRouteValues = null,
                        ActionText = "Ver Comissões"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar alerta de comissões pendentes");
            }
        }
        private async Task AddOverduePaymentsAlertAsync(List<AdminAlertDto> alerts, CancellationToken cancellationToken)
        {
            try
            {
                var overduePayments = await _context.Payments
                    .AsNoTracking()
                    .Include(p => p.Subscription!)
                        .ThenInclude(s => s.Contract!)
                            .ThenInclude(c => c!.Client)
                    .Where(p => p.Status == PaymentStatus.Pending && p.DueDate < DateTime.UtcNow)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                if (overduePayments.Count > 0)
                {
                    var overdueAmount = overduePayments.Sum(p => p.Amount);
                    var oldestOverdue = overduePayments.MinBy(p => p.DueDate)!;
                    var daysOverdue = (DateTime.UtcNow - oldestOverdue.DueDate).Days;
                    alerts.Add(new AdminAlertDto
                    {
                        Type = BootstrapBadgeCss.Danger,
                        Icon = "bi-exclamation-circle-fill",
                        Title = "Pagamentos Atrasados",
                        Message = $"{overduePayments.Count} pagamento(s) atrasado(s). Valor total: R$ {overdueAmount:N2}. Mais antigo há {daysOverdue} dia(s).",
                        Count = overduePayments.Count,
                        ActionKey = null,
                        ActionController = AdminDashboardRouteNames.Controller,
                        ActionName = AdminDashboardRouteNames.Payments,
                        ActionRouteValues = new { status = "Pending" },
                        ActionText = "Ver Pagamentos Atrasados"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar alerta de pagamentos atrasados");
            }
        }
        private async Task AddCapacityAlertAsync(List<AdminAlertDto> alerts, CancellationToken cancellationToken)
        {
            try
            {
                const decimal capacityThreshold = 0.8m;
                var addressesNearCapacity = await _context.CoworkingAddresses
                    .AsNoTracking()
                    .Include(a => a.Coworking)
                    .Include(a => a.Location)
                    .Where(a => a.AddressStatus == AddressStatus.Active &&
                               ((a.MaxContracts > 0 && (decimal)a.CurrentContracts / a.MaxContracts >= capacityThreshold) ||
                                (a.ApprovedFiscalCapacity > 0 && (decimal)a.CurrentContracts / a.ApprovedFiscalCapacity >= capacityThreshold)))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                if (addressesNearCapacity.Count > 0)
                {
                    var atCapacityCount = addressesNearCapacity.Count(a =>
                        (a.MaxContracts > 0 && a.CurrentContracts >= a.MaxContracts) ||
                        (a.ApprovedFiscalCapacity > 0 && a.CurrentContracts >= a.ApprovedFiscalCapacity));
                    alerts.Add(new AdminAlertDto
                    {
                        Type = BootstrapBadgeCss.Info,
                        Icon = "bi-info-circle-fill",
                        Title = "Capacidade Próxima do Limite",
                        Message = $"{addressesNearCapacity.Count} endereço(s) com capacidade próxima ou no limite (≥80%). {atCapacityCount} endereço(s) já atingiram a capacidade máxima.",
                        Count = addressesNearCapacity.Count,
                        ActionKey = null,
                        ActionController = AdminDashboardRouteNames.Controller,
                        ActionName = AdminDashboardRouteNames.Coworkings,
                        ActionRouteValues = new { capacityAlert = StringBooleanValue.True },
                        ActionText = "Ver Coworkings"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar alerta de capacidade");
            }
        }
        private async Task AddCoworkingsUnderReviewAlertAsync(List<AdminAlertDto> alerts, CancellationToken cancellationToken)
        {
            try
            {
                var recentUpdateThreshold = DateTime.UtcNow.AddHours(-24);
                var coworkingsWithRecentUpdates = await _context.Coworkings
                    .AsNoTracking()
                    .Where(c => c.ApprovalStatus == CoworkingApprovalStatus.UnderReview &&
                               c.SubmittedAt.HasValue &&
                               c.SubmittedAt.Value >= recentUpdateThreshold)
                    .OrderByDescending(c => c.SubmittedAt)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                if (coworkingsWithRecentUpdates.Count > 0)
                {
                    var coworkingNames = coworkingsWithRecentUpdates
                        .Take(3)
                        .Select(c => c.Name)
                        .ToList();
                    var moreCount = coworkingsWithRecentUpdates.Count - coworkingNames.Count;
                    var message = $"{coworkingsWithRecentUpdates.Count} parceiro(s) fizeram alterações recentes e estão aguardando análise: {string.Join(", ", coworkingNames)}";
                    if (moreCount > 0)
                        message += $" e mais {moreCount} outro(s)";
                    alerts.Add(new AdminAlertDto
                    {
                        Type = BootstrapBadgeCss.Warning,
                        Icon = "bi-bell-fill",
                        Title = "Alterações de Parceiros Aguardando Análise",
                        Message = message,
                        Count = coworkingsWithRecentUpdates.Count,
                        ActionKey = null,
                        ActionController = AdminDashboardRouteNames.Controller,
                        ActionName = AdminDashboardRouteNames.Coworkings,
                        ActionRouteValues = new { approvalStatus = "UnderReview" },
                        ActionText = "Ver Parceiros em Análise"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar alerta de parceiros em análise");
            }
        }
        private async Task AddPaymentsAwaitingValidationAlertAsync(List<AdminAlertDto> alerts, CancellationToken cancellationToken)
        {
            try
            {
                var paymentsAwaitingValidation = await _context.Payments
                    .AsNoTracking()
                    .Include(p => p.Subscription!)
                        .ThenInclude(s => s!.Contract)
                    .Where(p => p.Status == PaymentStatus.AwaitingValidation)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                if (paymentsAwaitingValidation.Count > 0)
                {
                    var distinctClientsCount = paymentsAwaitingValidation
                        .Where(p => p.Subscription?.Contract?.ClientId != null)
                        .Select(p => p.Subscription!.Contract!.ClientId)
                        .Distinct()
                        .Count();
                    var paymentCount = paymentsAwaitingValidation.Count;
                    var count = distinctClientsCount > 0 ? distinctClientsCount : paymentCount;
                    var message = distinctClientsCount == 0
                        ? $"{paymentCount} pagamento(s) aguardando validação do comprovante."
                        : (distinctClientsCount == 1
                            ? "1 cliente aguardando validação do comprovante de pagamento."
                            : $"{distinctClientsCount} clientes aguardando validação do comprovante de pagamento ({paymentCount} pagamento(s)).");
                    alerts.Add(new AdminAlertDto
                    {
                        Type = BootstrapBadgeCss.Danger,
                        Icon = "bi-cash-coin",
                        Title = "Comprovantes de Pagamento Aguardando Validação",
                        Message = message,
                        Count = count,
                        ActionKey = null,
                        ActionController = AdminDashboardRouteNames.Controller,
                        ActionName = AdminDashboardRouteNames.Payments,
                        ActionRouteValues = new { status = "AwaitingValidation" },
                        ActionText = "Ver Pagamentos"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar alerta de pagamentos aguardando validação");
            }
        }
        private async Task AddPendingCardPriceAlertAsync(List<AdminAlertDto> alerts, CancellationToken cancellationToken)
        {
            try
            {
                    var pendingCardPriceCount = await _context.CardPriceChangeRequests
                    .AsNoTracking()
                    .CountAsync(r => r.Status == CapacityApprovalStatus.Pending, cancellationToken).ConfigureAwait(false);
                if (pendingCardPriceCount > 0)
                {
                    alerts.Add(new AdminAlertDto
                    {
                        Type = BootstrapBadgeCss.Warning,
                        Icon = "bi-credit-card",
                        Title = "Preços no Cartão Aguardando Aprovação",
                        Message = $"{pendingCardPriceCount} solicitação(ões) de alteração de preços no cartão aguardando aprovação.",
                        Count = pendingCardPriceCount,
                        ActionKey = AdminDashboardAlertKeys.CardPricePending,
                        ActionController = AdminDashboardRouteNames.Controller,
                        ActionName = AdminDashboardRouteNames.Coworkings,
                        ActionRouteValues = (object?)null,
                        ActionText = "Ver solicitações"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar alerta de preços no cartão pendentes");
            }
        }
    }
}