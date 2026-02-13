using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;

namespace KlyonHubPlatform.Application.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync()
        {
            // Calcular receitas baseadas em comissões
            // Receita Bruta = soma de todos os TotalPaymentAmount das comissões (ou pagamentos pagos)
            var grossRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            // Receita Parceiro = soma de (TotalPaymentAmount - Amount) de todas as comissões
            var partnerRevenue = await _context.Commissions
                .SumAsync(c => (decimal?)(c.TotalPaymentAmount - c.Amount)) ?? 0;

            // Receita Líquida = Receita Bruta - Receita Parceiro (ou soma dos Amount das comissões)
            var netRevenue = await _context.Commissions
                .SumAsync(c => (decimal?)c.Amount) ?? 0;

            // Comissões Concluídas = soma das comissões transferidas
            var completedCommissions = await _context.Commissions
                .Where(c => c.IsTransferred)
                .SumAsync(c => (decimal?)(c.TotalPaymentAmount - c.Amount)) ?? 0;

            var model = new DashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalCoworkings = await _context.Coworkings.CountAsync(),
                ActiveContracts = await _context.Contracts.CountAsync(c => c.Status == ContractStatus.Active),
                TotalRevenue = grossRevenue, // Mantido para compatibilidade
                GrossRevenue = grossRevenue,
                PartnerRevenue = partnerRevenue,
                NetRevenue = netRevenue,
                TotalLocations = await _context.Locations.CountAsync(),
                PendingPayments = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Pending)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0,
                TotalCommissions = await _context.Commissions
                    .Where(c => !c.IsTransferred)
                    .SumAsync(c => (decimal?)(c.TotalPaymentAmount - c.Amount)) ?? 0, // Valor creditado pendente
                CompletedCommissions = completedCommissions,
                InactiveUsers = await _context.Users.CountAsync(u => !u.IsActive),
                UnverifiedUsers = await _context.Users.CountAsync(u => !u.EmailVerified)
            };

            // Receita dos últimos 6 meses
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            model.MonthlyRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt.HasValue && p.PaidAt >= sixMonthsAgo)
                .GroupBy(p => new { Year = p.PaidAt!.Value.Year, Month = p.PaidAt.Value.Month })
                .Select(g => new MonthlyRevenueDto
                {
                    Month = g.Key.Month,
                    Year = g.Key.Year,
                    Revenue = g.Sum(p => p.Amount)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            // Usuários por tipo (mantido para compatibilidade, mas não mais exibido)
            model.UsersByRole = await _context.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.Role != null && ur.Role.Name != null)
                .GroupBy(ur => ur.Role!.Name)
                .Select(g => new UsersByRoleDto
                {
                    Role = g.Key!,
                    Count = g.Count()
                })
                .ToListAsync();

            // === NOVOS GRÁFICOS ===

            // 1. Novos contratos por mês (últimos 6 meses)
            model.MonthlyContracts = await _context.Contracts
                .Where(c => c.CreatedAt >= sixMonthsAgo)
                .GroupBy(c => new { Year = c.CreatedAt.Year, Month = c.CreatedAt.Month })
                .Select(g => new MonthlyContractsDto
                {
                    Month = g.Key.Month,
                    Year = g.Key.Year,
                    NewContracts = g.Count(),
                    CancelledContracts = g.Count(c => c.Status == ContractStatus.Cancelled)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            // Totais de contratos nos últimos 6 meses
            model.NewContractsLast6Months = await _context.Contracts
                .Where(c => c.CreatedAt >= sixMonthsAgo)
                .CountAsync();
            model.CancelledContractsLast6Months = await _context.Contracts
                .Where(c => c.Status == ContractStatus.Cancelled && c.UpdatedAt >= sixMonthsAgo)
                .CountAsync();

            // 2. Top 5 coworkings por receita
            // Materializar os dados primeiro e agrupar em memória para evitar problema de tradução do EF Core
            var paymentsForTopCoworkings = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.SubscriptionId != null)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                            .ThenInclude(a => a.Coworking)
                .Where(p => p.Subscription != null && 
                           p.Subscription.Contract != null && 
                           p.Subscription.Contract.CoworkingAddress != null &&
                           p.Subscription.Contract.CoworkingAddress.Coworking != null)
                .Select(p => new {
                    CoworkingId = p.Subscription!.Contract!.CoworkingAddress!.CoworkingId,
                    CoworkingName = p.Subscription.Contract.CoworkingAddress.Coworking.Name,
                    Amount = p.Amount,
                    ContractId = p.Subscription.Contract.Id
                })
                .ToListAsync();

            model.TopCoworkingsByRevenue = paymentsForTopCoworkings
                .GroupBy(p => new { p.CoworkingId, p.CoworkingName })
                .Select(g => new TopCoworkingRevenueDto
                {
                    CoworkingId = g.Key.CoworkingId,
                    CoworkingName = g.Key.CoworkingName,
                    Revenue = g.Sum(p => p.Amount),
                    ContractsCount = g.Select(p => p.ContractId).Distinct().Count()
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToList();

            // 3. Pagamentos por método (últimos 6 meses)
            model.PaymentsByMethod = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= sixMonthsAgo)
                .GroupBy(p => p.Method)
                .Select(g => new PaymentsByMethodDto
                {
                    Method = g.Key.ToString(),
                    MethodDisplay = g.Key == PaymentMethod.PIX ? "PIX" :
                                   g.Key == PaymentMethod.CreditCard ? "Cartão de Crédito" :
                                   g.Key == PaymentMethod.BankTransfer ? "Boleto" : g.Key.ToString(),
                    Count = g.Count(),
                    TotalAmount = g.Sum(p => p.Amount)
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToListAsync();

            // 4. Novos clientes por mês (últimos 6 meses)
            model.MonthlyNewClients = await _context.Clients
                .Where(c => c.CreatedAt >= sixMonthsAgo)
                .GroupBy(c => new { Year = c.CreatedAt.Year, Month = c.CreatedAt.Month })
                .Select(g => new MonthlyNewClientsDto
                {
                    Month = g.Key.Month,
                    Year = g.Key.Year,
                    NewClients = g.Count()
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            // 5. Breakdown de receita mensal (bruta, parceiro, líquida) - últimos 6 meses
            var paymentsWithCommissions = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt.HasValue && p.PaidAt >= sixMonthsAgo)
                .Select(p => new { 
                    Year = p.PaidAt!.Value.Year, 
                    Month = p.PaidAt.Value.Month, 
                    Amount = p.Amount 
                })
                .ToListAsync();

            var commissionsData = await _context.Commissions
                .Where(c => c.CreatedAt >= sixMonthsAgo)
                .Select(c => new { 
                    Year = c.CreatedAt.Year, 
                    Month = c.CreatedAt.Month,
                    CommissionAmount = c.Amount,
                    PartnerAmount = c.TotalPaymentAmount - c.Amount
                })
                .ToListAsync();

            var monthlyBreakdown = paymentsWithCommissions
                .GroupBy(p => new { p.Year, p.Month })
                .Select(g => {
                    var commissionForMonth = commissionsData
                        .Where(c => c.Year == g.Key.Year && c.Month == g.Key.Month)
                        .ToList();
                    
                    var gross = g.Sum(p => p.Amount);
                    var partner = commissionForMonth.Sum(c => c.PartnerAmount);
                    var net = commissionForMonth.Sum(c => c.CommissionAmount);
                    
                    // Se não houver comissões, estimar 20% como receita líquida
                    if (net == 0 && gross > 0)
                    {
                        net = gross * 0.20m;
                        partner = gross - net;
                    }
                    
                    return new MonthlyRevenueBreakdownDto
                    {
                        Month = g.Key.Month,
                        Year = g.Key.Year,
                        GrossRevenue = gross,
                        PartnerRevenue = partner,
                        NetRevenue = net
                    };
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            model.MonthlyRevenueBreakdown = monthlyBreakdown;

            // Contratos por status
            model.ContractsByStatus = await _context.Contracts
                .GroupBy(c => c.Status)
                .Select(g => new ContractsByStatusDto
                {
                    Status = g.Key.ToString(),
                    Count = g.Count()
                })
                .ToListAsync();

            // Top 5 localidades por receita
            model.TopLocations = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.CoworkingAddress)
                            .ThenInclude(a => a!.Location)
                .Where(p => p.Status == PaymentStatus.Paid && p.Subscription != null && p.Subscription.Contract != null && p.Subscription.Contract.CoworkingAddress != null && p.Subscription.Contract.CoworkingAddress.Location != null)
                .GroupBy(p => p.Subscription!.Contract!.CoworkingAddress!.Location!.City)
                .Select(g => new TopLocationDto
                {
                    City = g.Key,
                    Revenue = g.Sum(p => p.Amount)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            // Pagamentos recentes
            model.RecentPayments = await GetRecentPaymentsAsync(30, 50);

            return model;
        }

        public async Task<List<Payment>> GetRecentPaymentsAsync(int days = 30, int take = 10)
        {
            var dateFrom = DateTime.UtcNow.AddDays(-days);
            return await _context.Payments
                .Where(p => 
                    (p.Status == PaymentStatus.Paid && p.PaidAt >= dateFrom) ||
                    (p.Status == PaymentStatus.Pending && p.CreatedAt >= dateFrom) ||
                    (p.Status == PaymentStatus.AwaitingValidation && p.CreatedAt >= dateFrom) ||
                    (p.Status == PaymentStatus.Processing && p.CreatedAt >= dateFrom)
                )
                .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                .Take(take)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.Client)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.CoworkingAddress)
                .ToListAsync();
        }

        public async Task<ReportFinancialResult> GetFinancialReportAsync(DateTime dateFrom, DateTime dateTo, string? product = null, string? assistedSaleFilter = null)
        {
            var result = new ReportFinancialResult();
            var rows = new List<ReportFinancialRowDto>();
            var totalReceived = 0m;
            var pending = 0m;

            var productNorm = string.IsNullOrWhiteSpace(product) ? ReportProductFilter.Todos : product.Trim();
            var assistedNorm = string.IsNullOrWhiteSpace(assistedSaleFilter) ? ReportAssistedSaleFilter.All : assistedSaleFilter.Trim();

            // Endereço fiscal (Payment -> Subscription -> Contract)
            if (productNorm == ReportProductFilter.Todos || productNorm == ReportProductFilter.EnderecoFiscal)
            {
                var dateFromUtc = dateFrom.Date;
                var dateToEnd = dateTo.Date.AddDays(1);

                var payments = await _context.Payments
                    .Where(p =>
                        p.SubscriptionId != null &&
                        ((p.PaidAt != null && p.PaidAt >= dateFromUtc && p.PaidAt < dateToEnd) ||
                         (p.PaidAt == null && p.CreatedAt >= dateFromUtc && p.CreatedAt < dateToEnd))
                    )
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s!.Contract)
                            .ThenInclude(c => c!.Client)
                                .ThenInclude(client => client.User)
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s!.Contract)
                            .ThenInclude(c => c!.CoworkingAddress)
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s!.Contract)
                            .ThenInclude(c => c!.AssistedSaleAdmin)
                    .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                    .ToListAsync();

                foreach (var p in payments)
                {
                    var contract = p.Subscription?.Contract;
                    if (contract == null) continue;

                    var isAssisted = contract.IsAssistedSale;
                    if (assistedNorm == ReportAssistedSaleFilter.AssistedOnly && !isAssisted) continue;
                    if (assistedNorm == ReportAssistedSaleFilter.SelfServiceOnly && isAssisted) continue;

                    var isPaid = p.Status == PaymentStatus.Paid;
                    if (isPaid) totalReceived += p.Amount;
                    else if (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.AwaitingValidation || p.Status == PaymentStatus.Processing)
                        pending += p.Amount;

                    var client = contract.Client;
                    var clientName = client != null
                        ? (client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User?.FullName ?? "N/A")
                        : "N/A";

                    var paymentMethod = p.Method.ToDisplayString();

                    var address = contract.CoworkingAddress;
                    var location = "N/A";
                    if (address != null)
                    {
                        var city = address.City ?? "";
                        var state = address.State ?? "";
                        if (!string.IsNullOrEmpty(city) || !string.IsNullOrEmpty(state))
                            location = $"{city}/{state}".Trim('/');
                    }

                    var saleOrigin = isAssisted ? "Venda assistida" : "Autoatendimento";
                    var adminName = isAssisted && contract.AssistedSaleAdmin != null ? contract.AssistedSaleAdmin.FullName : null;

                    rows.Add(new ReportFinancialRowDto
                    {
                        ProductType = "Endereço fiscal",
                        Date = p.PaidAt ?? p.CreatedAt,
                        Amount = p.Amount,
                        Method = paymentMethod,
                        Client = clientName,
                        Location = location,
                        Status = p.Status.ToDisplayString(),
                        IsAssistedSale = isAssisted,
                        SaleOrigin = saleOrigin,
                        AssistedSaleAdminName = adminName
                    });
                }
            }

            // Aluguel de salas (Reservations -> Payments). Filtro por venda assistida não se aplica.
            if ((productNorm == ReportProductFilter.Todos || productNorm == ReportProductFilter.AluguelSalas) && assistedNorm == ReportAssistedSaleFilter.All)
            {
                var dateFromUtc = dateFrom.Date;
                var dateToEnd = dateTo.Date.AddDays(1);

                var reservationPayments = await _context.Payments
                    .Where(p =>
                        p.ReservationId != null &&
                        ((p.PaidAt != null && p.PaidAt >= dateFromUtc && p.PaidAt < dateToEnd) ||
                         (p.PaidAt == null && p.CreatedAt >= dateFromUtc && p.CreatedAt < dateToEnd))
                    )
                    .Include(p => p.Reservation)
                        .ThenInclude(r => r!.Client)
                            .ThenInclude(client => client!.User)
                    .Include(p => p.Reservation)
                        .ThenInclude(r => r!.CoworkingAddress)
                            .ThenInclude(a => a!.Location)
                    .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                    .ToListAsync();

                foreach (var p in reservationPayments)
                {
                    var isPaid = p.Status == PaymentStatus.Paid;
                    if (isPaid) totalReceived += p.Amount;
                    else if (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.AwaitingValidation || p.Status == PaymentStatus.Processing)
                        pending += p.Amount;

                    // Obter nome do cliente
                    var clientName = "N/A";
                    var client = p.Reservation?.Client;
                    if (client != null)
                    {
                        clientName = client.RazaoSocial 
                            ?? client.NomeFantasia 
                            ?? client.ContactName 
                            ?? client.User?.FullName 
                            ?? "N/A";
                    }

                    var paymentMethod = p.Method.ToDisplayString();

                    // Obter localidade
                    var location = "N/A";
                    var address = p.Reservation?.CoworkingAddress;
                    if (address != null)
                    {
                        var city = address.City ?? "";
                        var state = address.State ?? "";
                        if (!string.IsNullOrEmpty(city) || !string.IsNullOrEmpty(state))
                        {
                            location = $"{city}/{state}".Trim('/');
                        }
                    }

                    rows.Add(new ReportFinancialRowDto
                    {
                        ProductType = "Aluguel de salas",
                        Date = p.PaidAt ?? p.CreatedAt,
                        Amount = p.Amount,
                        Method = paymentMethod,
                        Client = clientName,
                        Location = location,
                        Status = p.Status.ToDisplayString(),
                        IsAssistedSale = false,
                        SaleOrigin = "",
                        AssistedSaleAdminName = null
                    });
                }
            }

            // Certificado digitais: ainda não implementado no sistema; nenhum dado a incluir

            // Calcular comissões totais
            var totalCommissions = 0m;
            try
            {
                totalCommissions = await _context.Commissions
                    .Where(c => c.CreatedAt >= dateFrom.Date && c.CreatedAt < dateTo.Date.AddDays(1))
                    .SumAsync(c => c.Amount);
            }
            catch
            {
                // Se a tabela não existir, calcular estimativa (20% padrão)
                totalCommissions = totalReceived * 0.20m;
            }

            // Ordenar todas as linhas por data decrescente
            result.Rows = rows.OrderByDescending(r => r.Date).ToList();
            result.TotalPayments = result.Rows.Count;
            result.TotalReceived = totalReceived;
            result.TotalCommissions = totalCommissions;
            result.Pending = pending;

            return result;
        }

        public async Task<List<ClientCreditsReportDto>> GetClientCreditsReportAsync()
        {
            // Buscar clientes que têm pagamentos ou reservas
            var clientIds = new HashSet<int>();

            // Buscar clientes através de pagamentos (via Subscription ou Reservation)
            var paymentsWithClients = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                .Include(p => p.Reservation)
                .ToListAsync();

            foreach (var p in paymentsWithClients)
            {
                if (p.Subscription?.Contract?.ClientId != null)
                    clientIds.Add(p.Subscription.Contract.ClientId);
                if (p.Reservation?.ClientId != null)
                    clientIds.Add(p.Reservation.ClientId);
            }

            var reserved = await _context.Reservations
                .Where(r => r.Status != ReservationStatus.Cancelled)
                .Select(r => r.ClientId)
                .Distinct()
                .ToListAsync();
            foreach (var id in reserved) clientIds.Add(id);

            var list = new List<ClientCreditsReportDto>();
            foreach (var cid in clientIds.OrderBy(x => x))
            {
                var client = await _context.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == cid);
                var name = client?.RazaoSocial ?? client?.NomeFantasia ?? client?.ContactName ?? client?.User?.FullName ?? $"Cliente #{cid}";

                // Total pago pelo cliente (todos os pagamentos aprovados via Subscription ou Reservation)
                var clientPayments = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Paid)
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s!.Contract)
                    .Include(p => p.Reservation)
                    .ToListAsync();

                var totalPaid = clientPayments
                    .Where(p => (p.SubscriptionId != null && p.Subscription?.Contract?.ClientId == cid) ||
                               (p.ReservationId != null && p.Reservation?.ClientId == cid))
                    .Sum(p => p.Amount);

                // Reservas do cliente
                var resList = await _context.Reservations
                    .Where(r => r.ClientId == cid && r.Status != ReservationStatus.Cancelled)
                    .Include(r => r.CoworkingAddress)
                        .ThenInclude(a => a.Coworking)
                    .ToListAsync();
                
                var totalSpent = resList.Sum(r => r.TotalValue);
                
                var spending = resList
                    .Where(r => r.CoworkingAddress != null)
                    .GroupBy(r => r.CoworkingAddress!.CoworkingId)
                    .Select(g => new ClientCreditsSpendingByCoworkingDto
                    {
                        CoworkingId = g.Key,
                        CoworkingName = g.First().CoworkingAddress?.Coworking?.Name ?? "N/A",
                        CreditsUsed = 0, // Não usamos mais créditos
                        TotalValue = g.Sum(r => r.TotalValue)
                    })
                    .ToList();

                // Saldo atual = Total pago - Total gasto em reservas
                var currentBalance = totalPaid - totalSpent;

                list.Add(new ClientCreditsReportDto
                {
                    ClientId = cid,
                    ClientName = name,
                    TotalCreditsPurchased = 0, // Não usamos mais créditos, manter 0 para compatibilidade
                    TotalAmountPaid = totalPaid,
                    SpendingByCoworking = spending,
                    CurrentBalance = 0 // Não usamos mais créditos, usar TotalAmountPaid - TotalSpent
                });
            }
            return list.OrderByDescending(c => c.TotalAmountPaid).ToList();
        }

        public async Task<ClientCreditStatementDto> GetClientCreditStatementAsync(int clientId, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var client = await _context.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == clientId);
            var name = client != null
                ? (client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User?.FullName ?? $"Cliente #{clientId}")
                : $"Cliente #{clientId}";

            // Calcular saldo atual: Total pago - Total gasto
            var allPayments = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                .Include(p => p.Reservation)
                .ToListAsync();

            var totalPaid = allPayments
                .Where(p => (p.SubscriptionId != null && p.Subscription?.Contract?.ClientId == clientId) ||
                           (p.ReservationId != null && p.Reservation?.ClientId == clientId))
                .Sum(p => p.Amount);

            var totalSpent = await _context.Reservations
                .Where(r => r.ClientId == clientId && r.Status != ReservationStatus.Cancelled)
                .SumAsync(r => r.TotalValue);

            var balance = totalPaid - totalSpent;

            var payments = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.CoworkingAddress)
                            .ThenInclude(a => a.Location)
                .Include(p => p.Reservation)
                .ToListAsync();

            // Filtrar pagamentos do cliente em memória
            payments = payments
                .Where(p => (p.SubscriptionId != null && p.Subscription?.Contract?.ClientId == clientId) ||
                           (p.ReservationId != null && p.Reservation?.ClientId == clientId))
                .ToList();

            var reservations = await _context.Reservations
                .Where(r => r.ClientId == clientId && r.Status != ReservationStatus.Cancelled)
                .Include(r => r.CoworkingAddress)
                    .ThenInclude(a => a!.Coworking)
                .ToListAsync();

            var dateFromUtc = dateFrom?.Date ?? DateTime.MinValue;
            var dateToEnd = dateTo.HasValue ? dateTo.Value.Date.AddDays(1) : DateTime.MaxValue;

            var rows = new List<ClientCreditStatementRowDto>();

            foreach (var p in payments)
            {
                var d = p.PaidAt ?? p.CreatedAt;
                if (d >= dateFromUtc && d < dateToEnd)
                {
                    var description = "Pagamento";
                    if (p.Subscription != null && p.Subscription.Contract != null)
                    {
                        var location = p.Subscription.Contract.CoworkingAddress?.Location;
                        description = $"Pagamento - Endereço Fiscal" + (location != null ? $" ({location.City}/{location.State})" : "");
                    }
                    else if (p.ReservationId != null)
                    {
                        description = "Pagamento - Reserva";
                    }

                    rows.Add(new ClientCreditStatementRowDto
                    {
                        Date = d,
                        Description = description,
                        Type = "Entrada",
                        CreditsDelta = 0, // Não usamos mais créditos
                        Amount = p.Amount,
                        PaymentId = p.Id,
                        ReservationId = null
                    });
                }
            }

            foreach (var r in reservations)
            {
                var d = r.ReservationDate.Add(r.StartTime ?? TimeSpan.Zero);
                if (d >= dateFromUtc && d < dateToEnd)
                    rows.Add(new ClientCreditStatementRowDto
                    {
                        Date = d,
                        Description = "Reserva - " + (r.CoworkingAddress?.Coworking?.Name ?? "Coworking"),
                        Type = "Saída",
                        CreditsDelta = 0, // Não usamos mais créditos
                        Amount = r.TotalValue,
                        PaymentId = null,
                        ReservationId = r.Id
                    });
            }

            rows.Sort((a, b) => a.Date.CompareTo(b.Date));

            return new ClientCreditStatementDto
            {
                ClientId = clientId,
                ClientName = name,
                CurrentBalance = 0, // Não usamos mais créditos, calcular como totalPaid - totalSpent
                DateFrom = dateFrom,
                DateTo = dateTo,
                Movements = rows
            };
        }

        public async Task<List<CoworkingRevenueReportDto>> GetCoworkingRevenueReportAsync()
        {
            var result = new List<CoworkingRevenueReportDto>();
            
            // Buscar todos os coworkings que têm receita
            var coworkingIds = new HashSet<int>();

            // 1. Buscar coworkings através de reservas (Aluguel de Salas)
            var reservationCoworkings = await _context.Reservations
                .Where(r => (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Completed) && r.CoworkingAddress != null)
                .Select(r => r.CoworkingAddress!.CoworkingId)
                .Distinct()
                .ToListAsync();
            
            foreach (var id in reservationCoworkings)
                coworkingIds.Add(id);

            // 2. Buscar coworkings através de contratos (Endereço Fiscal)
            var contractCoworkings = await _context.Contracts
                .Where(c => c.Status == ContractStatus.Active)
                .Select(c => c.CoworkingAddress!.CoworkingId)
                .Distinct()
                .ToListAsync();
            
            foreach (var id in contractCoworkings)
                coworkingIds.Add(id);

            // Buscar serviços para obter porcentagens de comissão
            var reservarSalaService = await _context.Services
                .FirstOrDefaultAsync(s => s.Name == "Reservar Sala" && s.IsActive);
            
            var enderecoFiscalService = await _context.Services
                .FirstOrDefaultAsync(s => s.Name == "Endereço Fiscal" && s.IsActive);

            // Para cada coworking, calcular receita por produto
            foreach (var coworkingId in coworkingIds)
            {
                var coworking = await _context.Coworkings.FindAsync(coworkingId);
                var coworkingName = coworking?.Name ?? $"Coworking #{coworkingId}";
                
                var products = new List<CoworkingProductRevenueDto>();
                decimal totalRevenue = 0m;

                // Produto: Aluguel de Salas
                var reservations = await _context.Reservations
                    .Where(r => r.CoworkingAddress != null && 
                               r.CoworkingAddress.CoworkingId == coworkingId &&
                               (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Completed))
                    .ToListAsync();
                
                if (reservations.Any())
                {
                    var reservationRevenue = reservations.Sum(r => r.TotalValue);
                    var reservationCount = reservations.Count;
                    
                    // Buscar porcentagem de comissão
                    decimal commissionPercentage = 20m; // Padrão
                    if (reservarSalaService != null)
                    {
                        var partnerService = await _context.PartnerServices
                            .FirstOrDefaultAsync(ps => ps.CoworkingId == coworkingId && 
                                                      ps.ServiceId == reservarSalaService.Id);
                        if (partnerService != null && partnerService.Percentage > 0)
                            commissionPercentage = partnerService.Percentage;
                    }
                    
                    products.Add(new CoworkingProductRevenueDto
                    {
                        ProductType = ReportProductFilter.AluguelSalas,
                        ProductDisplayName = "Aluguel de Salas",
                        TotalRevenue = reservationRevenue,
                        TransactionCount = reservationCount,
                        CommissionPercentage = commissionPercentage
                    });
                    
                    totalRevenue += reservationRevenue;
                }

                // Produto: Endereço Fiscal
                var contracts = await _context.Contracts
                    .Where(c => c.CoworkingAddress != null &&
                               c.CoworkingAddress.CoworkingId == coworkingId &&
                               c.Status == ContractStatus.Active)
                    .Include(c => c.Subscription)
                        .ThenInclude(s => s!.Payments)
                    .ToListAsync();
                
                var fiscalPayments = contracts
                    .Where(c => c.Subscription != null)
                    .Select(c => c.Subscription!)
                    .SelectMany(s => s.Payments)
                    .Where(p => p.Status == PaymentStatus.Paid)
                    .ToList();
                
                if (fiscalPayments.Any())
                {
                    var fiscalRevenue = fiscalPayments.Sum(p => p.Amount);
                    var fiscalCount = fiscalPayments.Count;
                    
                    // Buscar porcentagem de comissão
                    decimal commissionPercentage = 20m; // Padrão
                    if (enderecoFiscalService != null)
                    {
                        var partnerService = await _context.PartnerServices
                            .FirstOrDefaultAsync(ps => ps.CoworkingId == coworkingId && 
                                                      ps.ServiceId == enderecoFiscalService.Id);
                        if (partnerService != null && partnerService.Percentage > 0)
                            commissionPercentage = partnerService.Percentage;
                    }
                    
                    products.Add(new CoworkingProductRevenueDto
                    {
                        ProductType = ReportProductFilter.EnderecoFiscal,
                        ProductDisplayName = "Endereço Fiscal",
                        TotalRevenue = fiscalRevenue,
                        TransactionCount = fiscalCount,
                        CommissionPercentage = commissionPercentage
                    });
                    
                    totalRevenue += fiscalRevenue;
                }

                if (products.Any())
                {
                    result.Add(new CoworkingRevenueReportDto
                    {
                        CoworkingId = coworkingId,
                        CoworkingName = coworkingName,
                        TotalRevenue = totalRevenue,
                        Products = products
                    });
                }
            }

            return result.OrderByDescending(x => x.TotalRevenue).ToList();
        }

        public async Task<CoworkingRevenueDetailDto> GetCoworkingRevenueDetailAsync(int coworkingId, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var cow = await _context.Coworkings.FindAsync(coworkingId);
            var name = cow?.Name ?? $"Coworking #{coworkingId}";

            // Buscar o serviço "Reservar Sala" para obter o ServiceId
            var service = await _context.Services
                .FirstOrDefaultAsync(s => s.Name == "Reservar Sala" && s.IsActive);

            // Buscar porcentagem do PartnerService para este coworking e serviço
            decimal defaultCommissionPercentage = 20m; // Fallback padrão
            if (service != null)
            {
                var partnerService = await _context.PartnerServices
                    .FirstOrDefaultAsync(ps => ps.CoworkingId == coworkingId && ps.ServiceId == service.Id);
                
                if (partnerService != null && partnerService.Percentage > 0)
                {
                    defaultCommissionPercentage = partnerService.Percentage;
                }
            }

            IQueryable<Reservation> q = _context.Reservations
                .Where(r => r.CoworkingAddress != null && r.CoworkingAddress.CoworkingId == coworkingId && (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Completed))
                .Include(r => r.Client)
                .Include(r => r.CoworkingAddress);

            if (dateFrom.HasValue)
                q = q.Where(r => r.ReservationDate >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                q = q.Where(r => r.ReservationDate <= dateTo.Value.Date.AddDays(1));

            var reservations = await q
                .OrderByDescending(r => r.ReservationDate)
                .ThenByDescending(r => r.StartTime)
                .ToListAsync();

            var res = reservations.Select(r => new CoworkingReservationDetailRowDto
            {
                ReservationId = r.Id,
                Date = r.ReservationDate,
                RoomType = r.RoomType,
                RoomTypeDisplay = r.RoomType == "Shared" ? "Sala compartilhada" : r.RoomType == "Private" ? "Sala privativa" : r.RoomType == "Auditorium" ? "Auditório" : r.RoomType,
                TotalValue = r.TotalValue,
                CreditsUsed = 0, // Não usamos mais créditos
                ClientName = r.Client == null ? "Cliente não identificado" : (r.Client.RazaoSocial ?? r.Client.NomeFantasia ?? r.Client.ContactName ?? "N/A"),
                Status = r.Status == ReservationStatus.Completed ? "Concluída" : "Confirmada",
                CommissionPercentage = defaultCommissionPercentage
            }).ToList();

            return new CoworkingRevenueDetailDto
            {
                CoworkingId = coworkingId,
                CoworkingName = name,
                Reservations = res,
                TotalRevenue = res.Sum(r => r.TotalValue)
            };
        }

        public async Task<CoworkingProductDetailDto> GetCoworkingProductDetailAsync(int coworkingId, string productType, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var coworking = await _context.Coworkings.FindAsync(coworkingId);
            var coworkingName = coworking?.Name ?? $"Coworking #{coworkingId}";
            
            var productDisplayName = ReportProductFilter.GetDisplayName(productType);

            var transactions = new List<ProductTransactionDetailDto>();
            decimal totalRevenue = 0m;
            decimal commissionPercentage = 20m;

            // Buscar porcentagem de comissão
            Service? service = null;
            if (productType == ReportProductFilter.AluguelSalas)
            {
                service = await _context.Services
                    .FirstOrDefaultAsync(s => s.Name == "Reservar Sala" && s.IsActive);
            }
            else if (productType == ReportProductFilter.EnderecoFiscal)
            {
                service = await _context.Services
                    .FirstOrDefaultAsync(s => s.Name == "Endereço Fiscal" && s.IsActive);
            }

            if (service != null)
            {
                var partnerService = await _context.PartnerServices
                    .FirstOrDefaultAsync(ps => ps.CoworkingId == coworkingId && ps.ServiceId == service.Id);
                if (partnerService != null && partnerService.Percentage > 0)
                    commissionPercentage = partnerService.Percentage;
            }

            if (productType == ReportProductFilter.AluguelSalas)
            {
                // Buscar pagamentos de reservas
                IQueryable<Payment> paymentQuery = _context.Payments
                    .Where(p => p.ReservationId != null &&
                               p.Reservation!.CoworkingAddress != null &&
                               p.Reservation.CoworkingAddress.CoworkingId == coworkingId &&
                               p.Status == PaymentStatus.Paid)
                    .Include(p => p.Reservation)
                        .ThenInclude(r => r!.Client)
                            .ThenInclude(c => c.User)
                    .Include(p => p.Reservation)
                        .ThenInclude(r => r!.CoworkingAddress);

                if (dateFrom.HasValue)
                    paymentQuery = paymentQuery.Where(p => (p.PaidAt ?? p.CreatedAt) >= dateFrom.Value.Date);
                if (dateTo.HasValue)
                    paymentQuery = paymentQuery.Where(p => (p.PaidAt ?? p.CreatedAt) <= dateTo.Value.Date.AddDays(1));

                var payments = await paymentQuery
                    .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                    .ToListAsync();

                foreach (var payment in payments)
                {
                    var reservation = payment.Reservation;
                    if (reservation == null) continue;

                    var clientName = "N/A";
                    if (reservation.Client != null)
                    {
                        clientName = reservation.Client.RazaoSocial 
                            ?? reservation.Client.NomeFantasia 
                            ?? reservation.Client.ContactName 
                            ?? reservation.Client.User?.FullName 
                            ?? "N/A";
                    }

                    var roomType = RoomTypes.GetDisplayName(reservation.RoomType ?? "");

                    var paymentMethod = payment.Method.ToDisplayString();

                    transactions.Add(new ProductTransactionDetailDto
                    {
                        PaymentId = payment.Id,
                        Date = payment.PaidAt ?? payment.CreatedAt,
                        Amount = payment.Amount,
                        ClientName = clientName,
                        ClientId = reservation.ClientId,
                        PaymentMethod = paymentMethod,
                        Status = payment.Status.ToDisplayString(),
                        AdditionalInfo = $"{roomType} - {reservation.ReservationDate:dd/MM/yyyy}",
                        ReservationId = reservation.Id,
                        ContractId = null
                    });

                    totalRevenue += payment.Amount;
                }
            }
            else if (productType == ReportProductFilter.EnderecoFiscal)
            {
                // Buscar pagamentos de contratos (endereço fiscal)
                IQueryable<Payment> paymentQuery = _context.Payments
                    .Where(p => p.SubscriptionId != null &&
                               p.Subscription!.Contract != null &&
                               p.Subscription.Contract.CoworkingAddress != null &&
                               p.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingId &&
                               p.Status == PaymentStatus.Paid)
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s!.Contract)
                            .ThenInclude(c => c!.Client)
                                .ThenInclude(client => client.User)
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s!.Contract)
                            .ThenInclude(c => c!.CoworkingAddress)
                                .ThenInclude(a => a!.Location);

                if (dateFrom.HasValue)
                    paymentQuery = paymentQuery.Where(p => (p.PaidAt ?? p.CreatedAt) >= dateFrom.Value.Date);
                if (dateTo.HasValue)
                    paymentQuery = paymentQuery.Where(p => (p.PaidAt ?? p.CreatedAt) <= dateTo.Value.Date.AddDays(1));

                var payments = await paymentQuery
                    .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                    .ToListAsync();

                foreach (var payment in payments)
                {
                    var contract = payment.Subscription?.Contract;
                    if (contract == null) continue;

                    var clientName = "N/A";
                    if (contract.Client != null)
                    {
                        clientName = contract.Client.RazaoSocial 
                            ?? contract.Client.NomeFantasia 
                            ?? contract.Client.ContactName 
                            ?? contract.Client.User?.FullName 
                            ?? "N/A";
                    }

                    var location = "N/A";
                    if (contract.CoworkingAddress?.Location != null)
                    {
                        var city = contract.CoworkingAddress.Location.City ?? "";
                        var state = contract.CoworkingAddress.Location.State ?? "";
                        if (!string.IsNullOrEmpty(city) || !string.IsNullOrEmpty(state))
                            location = $"{city}/{state}".Trim('/');
                    }

                    var paymentMethod = payment.Method.ToDisplayString();

                    transactions.Add(new ProductTransactionDetailDto
                    {
                        PaymentId = payment.Id,
                        Date = payment.PaidAt ?? payment.CreatedAt,
                        Amount = payment.Amount,
                        ClientName = clientName,
                        ClientId = contract.ClientId,
                        PaymentMethod = paymentMethod,
                        Status = payment.Status.ToDisplayString(),
                        AdditionalInfo = location,
                        ReservationId = null,
                        ContractId = contract.Id
                    });

                    totalRevenue += payment.Amount;
                }
            }

            return new CoworkingProductDetailDto
            {
                CoworkingId = coworkingId,
                CoworkingName = coworkingName,
                ProductType = productType,
                ProductDisplayName = productDisplayName,
                TotalRevenue = totalRevenue,
                CommissionPercentage = commissionPercentage,
                Transactions = transactions
            };
        }
    }
}
