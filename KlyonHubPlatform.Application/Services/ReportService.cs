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
            var model = new DashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalCoworkings = await _context.Coworkings.CountAsync(),
                ActiveContracts = await _context.Contracts.CountAsync(c => c.Status == ContractStatus.Active),
                TotalRevenue = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Paid)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0,
                TotalLocations = await _context.Locations.CountAsync(),
                PendingPayments = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Pending)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0,
                TotalCommissions = await _context.Commissions
                    .Where(c => !c.IsTransferred)
                    .SumAsync(c => (decimal?)c.Amount) ?? 0,
                InactiveUsers = await _context.Users.CountAsync(u => !u.IsActive),
                UnverifiedUsers = await _context.Users.CountAsync(u => !u.EmailVerified)
            };

            // Receita dos últimos 6 meses
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            model.MonthlyRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= sixMonthsAgo)
                .GroupBy(p => new { Year = p.PaidAt.Value.Year, Month = p.PaidAt.Value.Month })
                .Select(g => new MonthlyRevenueDto
                {
                    Month = g.Key.Month,
                    Year = g.Key.Year,
                    Revenue = g.Sum(p => p.Amount)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            // Usuários por tipo
            model.UsersByRole = await _context.UserRoles
                .Include(ur => ur.Role)
                .GroupBy(ur => ur.Role.Name)
                .Select(g => new UsersByRoleDto
                {
                    Role = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

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
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                            .ThenInclude(a => a.Location)
                .Where(p => p.Status == PaymentStatus.Paid)
                .GroupBy(p => p.Subscription.Contract.CoworkingAddress.Location.City)
                .Select(g => new TopLocationDto
                {
                    City = g.Key,
                    Revenue = g.Sum(p => p.Amount)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            // Pagamentos recentes
            model.RecentPayments = await GetRecentPaymentsAsync(30, 10);

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
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.Client)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                .ToListAsync();
        }

        public async Task<ReportFinancialResult> GetFinancialReportAsync(DateTime dateFrom, DateTime dateTo, string? product = null)
        {
            var result = new ReportFinancialResult();
            var rows = new List<ReportFinancialRowDto>();
            var totalReceived = 0m;
            var pending = 0m;

            var productNorm = string.IsNullOrWhiteSpace(product) ? ReportProductFilter.Todos : product.Trim();

            // Endereço fiscal (Payment -> Subscription -> Contract)
            if (productNorm == ReportProductFilter.Todos || productNorm == ReportProductFilter.EnderecoFiscal)
            {
                var dateFromUtc = dateFrom.Date;
                var dateToEnd = dateTo.Date.AddDays(1);

                var payments = await _context.Payments
                    .Where(p =>
                        ((p.PaidAt != null && p.PaidAt >= dateFromUtc && p.PaidAt < dateToEnd) ||
                         (p.PaidAt == null && p.CreatedAt >= dateFromUtc && p.CreatedAt < dateToEnd))
                    )
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                            .ThenInclude(c => c.Client)
                    .Include(p => p.Subscription)
                        .ThenInclude(s => s.Contract)
                            .ThenInclude(c => c.CoworkingAddress)
                    .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                    .ToListAsync();

                foreach (var p in payments)
                {
                    var isPaid = p.Status == PaymentStatus.Paid;
                    if (isPaid) totalReceived += p.Amount;
                    else if (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.AwaitingValidation || p.Status == PaymentStatus.Processing)
                        pending += p.Amount;

                    rows.Add(new ReportFinancialRowDto
                    {
                        ProductType = "Endereço fiscal",
                        Date = p.PaidAt ?? p.CreatedAt,
                        Amount = p.Amount,
                        Method = p.Method == PaymentMethod.PIX ? "PIX" : p.Method == PaymentMethod.CreditCard ? "Cartão" : p.Method == PaymentMethod.BankTransfer ? "Boleto" : p.Method.ToString() ?? "-",
                        Client = p.Subscription?.Contract?.Client?.RazaoSocial ?? p.Subscription?.Contract?.Client?.NomeFantasia ?? "N/A",
                        Location = p.Subscription?.Contract?.CoworkingAddress != null
                            ? $"{p.Subscription.Contract.CoworkingAddress.City ?? ""}/{p.Subscription.Contract.CoworkingAddress.State ?? ""}".Trim('/')
                            : "N/A",
                        Status = p.Status == PaymentStatus.Paid ? "Pago" : p.Status == PaymentStatus.Pending ? "Pendente" : p.Status == PaymentStatus.Processing ? "Processando"
                            : p.Status == PaymentStatus.AwaitingValidation ? "Aguardando Validação" : p.Status == PaymentStatus.Failed ? "Falhou" : p.Status == PaymentStatus.Refunded ? "Reembolsado" : p.Status == PaymentStatus.Cancelled ? "Cancelado" : p.Status.ToString()
                    });
                }
            }

            // Aluguel de salas (CreditPurchase - créditos para reservas)
            if (productNorm == ReportProductFilter.Todos || productNorm == ReportProductFilter.AluguelSalas)
            {
                var dateFromUtc = dateFrom.Date;
                var dateToEnd = dateTo.Date.AddDays(1);

                var purchases = await _context.CreditPurchases
                    .Where(c =>
                        ((c.PaidAt != null && c.PaidAt >= dateFromUtc && c.PaidAt < dateToEnd) ||
                         (c.PaidAt == null && c.CreatedAt >= dateFromUtc && c.CreatedAt < dateToEnd))
                    )
                    .Include(c => c.Client)
                    .OrderByDescending(c => c.PaidAt ?? c.CreatedAt)
                    .ToListAsync();

                foreach (var c in purchases)
                {
                    var isPaid = string.Equals(c.Status, "Paid", StringComparison.OrdinalIgnoreCase);
                    if (isPaid) totalReceived += c.AmountPaid;
                    else if (string.Equals(c.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(c.Status, "Processing", StringComparison.OrdinalIgnoreCase))
                        pending += c.AmountPaid;

                    rows.Add(new ReportFinancialRowDto
                    {
                        ProductType = "Aluguel de salas",
                        Date = c.PaidAt ?? c.CreatedAt,
                        Amount = c.AmountPaid,
                        Method = c.PaymentMethod == PaymentMethod.PIX ? "PIX" : c.PaymentMethod == PaymentMethod.CreditCard ? "Cartão" : c.PaymentMethod == PaymentMethod.BankTransfer ? "Boleto" : c.PaymentMethod?.ToString() ?? "-",
                        Client = c.Client?.RazaoSocial ?? c.Client?.NomeFantasia ?? "N/A",
                        Location = "-",
                        Status = string.Equals(c.Status, "Paid", StringComparison.OrdinalIgnoreCase) ? "Pago" : string.Equals(c.Status, "Pending", StringComparison.OrdinalIgnoreCase) ? "Pendente"
                            : string.Equals(c.Status, "Processing", StringComparison.OrdinalIgnoreCase) ? "Processando" : string.Equals(c.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ? "Cancelado" : c.Status ?? "-"
                    });
                }
            }

            // Certificado digitais: ainda não implementado no sistema; nenhum dado a incluir

            // Ordenar todas as linhas por data decrescente
            result.Rows = rows.OrderByDescending(r => r.Date).ToList();
            result.TotalPayments = result.Rows.Count;
            result.TotalReceived = totalReceived;
            result.Pending = pending;

            return result;
        }

        public async Task<List<ClientCreditsReportDto>> GetClientCreditsReportAsync()
        {
            var clientIds = new HashSet<int>();

            var purchased = await _context.CreditPurchases
                .Where(c => c.Status != null && c.Status.ToLower() == "paid")
                .Select(c => c.ClientId)
                .Distinct()
                .ToListAsync();
            foreach (var id in purchased) clientIds.Add(id);

            var reserved = await _context.Reservations
                .Where(r => r.Status != "Cancelled")
                .Select(r => r.ClientId)
                .Distinct()
                .ToListAsync();
            foreach (var id in reserved) clientIds.Add(id);

            var list = new List<ClientCreditsReportDto>();
            foreach (var cid in clientIds.OrderBy(x => x))
            {
                var client = await _context.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == cid);
                var name = client?.RazaoSocial ?? client?.NomeFantasia ?? client?.ContactName ?? client?.User?.FullName ?? $"Cliente #{cid}";

                var totalCredits = await _context.CreditPurchases
                    .Where(c => c.ClientId == cid && c.Status != null && c.Status.ToLower() == "paid")
                    .SumAsync(c => c.CreditsReceived + c.BonusCredits);
                var totalPaid = await _context.CreditPurchases
                    .Where(c => c.ClientId == cid && c.Status != null && c.Status.ToLower() == "paid")
                    .SumAsync(c => c.AmountPaid);

                var resList = await _context.Reservations
                    .Where(r => r.ClientId == cid && r.Status != "Cancelled")
                    .Include(r => r.CoworkingAddress)
                        .ThenInclude(a => a.Coworking)
                    .ToListAsync();
                var spending = resList
                    .Where(r => r.CoworkingAddress != null)
                    .GroupBy(r => r.CoworkingAddress!.CoworkingId)
                    .Select(g => new ClientCreditsSpendingByCoworkingDto
                    {
                        CoworkingId = g.Key,
                        CoworkingName = g.First().CoworkingAddress?.Coworking?.Name ?? "N/A",
                        CreditsUsed = g.Sum(r => r.CreditsUsed),
                        TotalValue = g.Sum(r => r.TotalValue)
                    })
                    .ToList();

                var balance = await _context.ClientCredits
                    .Where(cc => cc.ClientId == cid && cc.ExpiresAt > DateTime.UtcNow)
                    .SumAsync(cc => cc.Credits);

                list.Add(new ClientCreditsReportDto
                {
                    ClientId = cid,
                    ClientName = name,
                    TotalCreditsPurchased = totalCredits,
                    TotalAmountPaid = totalPaid,
                    SpendingByCoworking = spending,
                    CurrentBalance = balance
                });
            }
            return list.OrderByDescending(c => c.TotalCreditsPurchased + c.SpendingByCoworking.Sum(s => s.CreditsUsed)).ToList();
        }

        public async Task<ClientCreditStatementDto> GetClientCreditStatementAsync(int clientId, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var client = await _context.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == clientId);
            var name = client != null
                ? (client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User?.FullName ?? $"Cliente #{clientId}")
                : $"Cliente #{clientId}";

            var balance = await _context.ClientCredits
                .Where(cc => cc.ClientId == clientId && cc.ExpiresAt > DateTime.UtcNow)
                .SumAsync(cc => cc.Credits);

            var purchases = await _context.CreditPurchases
                .Where(c => c.ClientId == clientId && c.Status != null && c.Status.ToLower() == "paid")
                .ToListAsync();

            var reservations = await _context.Reservations
                .Where(r => r.ClientId == clientId && r.Status != "Cancelled")
                .Include(r => r.CoworkingAddress)
                    .ThenInclude(a => a!.Coworking)
                .ToListAsync();

            var dateFromUtc = dateFrom?.Date ?? DateTime.MinValue;
            var dateToEnd = dateTo.HasValue ? dateTo.Value.Date.AddDays(1) : DateTime.MaxValue;

            var rows = new List<ClientCreditStatementRowDto>();

            foreach (var c in purchases)
            {
                var d = c.PaidAt ?? c.CreatedAt;
                if (d >= dateFromUtc && d < dateToEnd)
                    rows.Add(new ClientCreditStatementRowDto
                    {
                        Date = d,
                        Description = "Compra de créditos",
                        Type = "Entrada",
                        CreditsDelta = c.CreditsReceived + c.BonusCredits,
                        Amount = c.AmountPaid
                    });
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
                        CreditsDelta = -r.CreditsUsed,
                        Amount = r.TotalValue
                    });
            }

            rows.Sort((a, b) => a.Date.CompareTo(b.Date));

            return new ClientCreditStatementDto
            {
                ClientId = clientId,
                ClientName = name,
                CurrentBalance = balance,
                DateFrom = dateFrom,
                DateTo = dateTo,
                Movements = rows
            };
        }

        public async Task<List<CoworkingRevenueReportDto>> GetCoworkingRevenueReportAsync()
        {
            var data = await _context.Reservations
                .Where(r => (r.Status == "Confirmed" || r.Status == "Completed"))
                .Include(r => r.CoworkingAddress)
                    .ThenInclude(a => a!.Coworking)
                .GroupBy(r => new { r.CoworkingAddress!.CoworkingId, r.CoworkingAddress.Coworking!.Name })
                .Select(g => new CoworkingRevenueReportDto
                {
                    CoworkingId = g.Key.CoworkingId,
                    CoworkingName = g.Key.Name ?? "",
                    TotalRevenue = g.Sum(r => r.TotalValue),
                    ReservationCount = g.Count()
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ToListAsync();
            return data;
        }

        public async Task<CoworkingRevenueDetailDto> GetCoworkingRevenueDetailAsync(int coworkingId, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var cow = await _context.Coworkings.FindAsync(coworkingId);
            var name = cow?.Name ?? $"Coworking #{coworkingId}";

            IQueryable<Reservation> q = _context.Reservations
                .Where(r => r.CoworkingAddress != null && r.CoworkingAddress.CoworkingId == coworkingId && (r.Status == "Confirmed" || r.Status == "Completed"))
                .Include(r => r.Client)
                .Include(r => r.CoworkingAddress);

            if (dateFrom.HasValue)
                q = q.Where(r => r.ReservationDate >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                q = q.Where(r => r.ReservationDate <= dateTo.Value.Date.AddDays(1));

            var res = await q
                .OrderByDescending(r => r.ReservationDate)
                .ThenByDescending(r => r.StartTime)
                .Select(r => new CoworkingReservationDetailRowDto
                {
                    ReservationId = r.Id,
                    Date = r.ReservationDate,
                    RoomType = r.RoomType,
                    RoomTypeDisplay = r.RoomType == "Shared" ? "Sala compartilhada" : r.RoomType == "Private" ? "Sala privativa" : r.RoomType == "Auditorium" ? "Auditório" : r.RoomType,
                    TotalValue = r.TotalValue,
                    CreditsUsed = r.CreditsUsed,
                    ClientName = r.Client == null ? "Cliente não identificado" : (r.Client.RazaoSocial ?? r.Client.NomeFantasia ?? r.Client.ContactName ?? "N/A"),
                    Status = r.Status == "Completed" ? "Concluída" : "Confirmada"
                })
                .ToListAsync();

            return new CoworkingRevenueDetailDto
            {
                CoworkingId = coworkingId,
                CoworkingName = name,
                Reservations = res,
                TotalRevenue = res.Sum(r => r.TotalValue)
            };
        }
    }
}
