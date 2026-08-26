using KlyonHubPlatform.Application.DTOs.PromoCode;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class PromoCodeService : IPromoCodeService
    {
        private readonly ApplicationDbContext _context;

        public PromoCodeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PromoCodeApplyResult> ValidateAndApplyAsync(
            string code,
            int coworkingId,
            int addressId,
            PaymentMethod paymentMethod,
            decimal baseAmount,
            CancellationToken cancellationToken = default)
        {
            var normalized = NormalizeCode(code);
            if (string.IsNullOrEmpty(normalized))
                return PromoCodeApplyResult.Invalid("Informe um código de cupom válido.");

            var now = DateTime.UtcNow;
            var promo = await _context.PromoCodes
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.IsActive &&
                    p.Code == normalized &&
                    p.ValidFrom <= now &&
                    (p.ValidTo == null || p.ValidTo >= now), cancellationToken);

            if (promo == null)
                return PromoCodeApplyResult.Invalid("Cupom inválido, expirado ou inativo.");

            if (promo.MaxRedemptions.HasValue && promo.RedemptionCount >= promo.MaxRedemptions.Value)
                return PromoCodeApplyResult.Invalid("Este cupom atingiu o limite de utilizações.");

            if (promo.Scope == PromoScope.Coworking)
            {
                if (!promo.CoworkingId.HasValue || promo.CoworkingId.Value != coworkingId)
                    return PromoCodeApplyResult.Invalid("Este cupom não é válido para o coworking selecionado.");
            }

            if (!IsPaymentMethodAllowed(promo.AllowedPaymentMethods, paymentMethod))
                return PromoCodeApplyResult.Invalid("Este cupom não é válido para a forma de pagamento selecionada.");

            var discount = promo.Type == PromoDiscountType.Percent
                ? Math.Round(baseAmount * promo.Value / 100m, 2, MidpointRounding.AwayFromZero)
                : Math.Min(promo.Value, baseAmount);

            if (discount <= 0)
                return PromoCodeApplyResult.Invalid("O cupom não gera desconto para este valor.");

            return PromoCodeApplyResult.Success(promo.Id, promo.Code, discount, baseAmount);
        }

        public async Task<PromoCode> CreateAsync(PromoCodeCreateInput input, CancellationToken cancellationToken = default)
        {
            var normalized = NormalizeCode(input.Code);
            if (string.IsNullOrEmpty(normalized))
                throw new InvalidOperationException("Informe um código de cupom válido.");

            if (input.Value <= 0)
                throw new InvalidOperationException("O valor do desconto deve ser maior que zero.");

            if (input.Type == PromoDiscountType.Percent && input.Value > 100)
                throw new InvalidOperationException("Desconto percentual não pode ser maior que 100%.");

            if (!input.IsAdmin)
            {
                if (input.Scope != PromoScope.Coworking)
                    throw new InvalidOperationException("Parceiros só podem criar cupons do próprio coworking.");
                if (!input.CoworkingId.HasValue || input.CoworkingId.Value <= 0)
                    throw new InvalidOperationException("Coworking é obrigatório para cupom de parceiro.");
            }
            else if (input.Scope == PromoScope.Coworking && (!input.CoworkingId.HasValue || input.CoworkingId.Value <= 0))
            {
                throw new InvalidOperationException("Informe o coworking para cupom com escopo de parceiro.");
            }

            await EnsureUniqueActiveCodeAsync(normalized, null, cancellationToken);

            var promo = new PromoCode
            {
                Code = normalized,
                Type = input.Type,
                Value = input.Value,
                Scope = input.Scope,
                ValidFrom = input.ValidFrom,
                ValidTo = input.ValidTo,
                MaxRedemptions = input.MaxRedemptions,
                CoworkingId = input.Scope == PromoScope.Global ? null : input.CoworkingId,
                CreatedByUserId = input.CreatedByUserId,
                IsActive = true,
                RedemptionCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.PromoCodes.Add(promo);
            await _context.SaveChangesAsync(cancellationToken);
            return promo;
        }

        public async Task<PromoCodeDto?> GetByIdAsync(int id, int? coworkingIdFilter, CancellationToken cancellationToken = default)
        {
            var query = _context.PromoCodes.AsNoTracking().Include(p => p.Coworking).AsQueryable();
            if (coworkingIdFilter.HasValue)
                query = query.Where(p => p.CoworkingId == coworkingIdFilter.Value);

            var promo = await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            return promo == null ? null : MapToDto(promo);
        }

        public async Task<List<PromoCodeDto>> ListAsync(int? coworkingIdFilter, bool includeGlobal, CancellationToken cancellationToken = default)
        {
            var query = _context.PromoCodes.AsNoTracking().Include(p => p.Coworking).AsQueryable();

            if (coworkingIdFilter.HasValue)
            {
                query = includeGlobal
                    ? query.Where(p => p.CoworkingId == coworkingIdFilter.Value || p.Scope == PromoScope.Global)
                    : query.Where(p => p.CoworkingId == coworkingIdFilter.Value);
            }

            var list = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
            return list.Select(MapToDto).ToList();
        }

        public async Task DeactivateAsync(int id, int? coworkingIdFilter, CancellationToken cancellationToken = default)
        {
            var query = _context.PromoCodes.AsQueryable();
            if (coworkingIdFilter.HasValue)
                query = query.Where(p => p.CoworkingId == coworkingIdFilter.Value);

            var promo = await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (promo == null)
                throw new KeyNotFoundException("Cupom não encontrado.");

            if (!promo.IsActive)
                return;

            promo.IsActive = false;
            promo.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task IncrementRedemptionAsync(int promoCodeId, CancellationToken cancellationToken = default)
        {
            var promo = await _context.PromoCodes.FirstOrDefaultAsync(p => p.Id == promoCodeId, cancellationToken);
            if (promo == null) return;

            promo.RedemptionCount++;
            promo.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task TryRecordRedemptionForPaymentAsync(int paymentId, int promoCodeId, CancellationToken cancellationToken = default)
        {
            var alreadyRedeemed = await _context.PaymentHistories
                .AnyAsync(h => h.PaymentId == paymentId && h.Action == PaymentHistoryActions.PromoRedeemed, cancellationToken);
            if (alreadyRedeemed)
                return;

            var promo = await _context.PromoCodes.FirstOrDefaultAsync(p => p.Id == promoCodeId, cancellationToken);
            if (promo == null)
                return;

            promo.RedemptionCount++;
            promo.UpdatedAt = DateTime.UtcNow;
            _context.PaymentHistories.Add(new PaymentHistory
            {
                PaymentId = paymentId,
                Action = PaymentHistoryActions.PromoRedeemed,
                ActionAt = DateTime.UtcNow
            });
        }

        private static bool IsPaymentMethodAllowed(string? allowedPaymentMethods, PaymentMethod paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(allowedPaymentMethods))
                return true;

            var allowed = allowedPaymentMethods
                .Split(',', ';')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return allowed.Contains(paymentMethod.ToString());
        }

        private async Task EnsureUniqueActiveCodeAsync(string normalizedCode, int? excludeId, CancellationToken cancellationToken)
        {
            var exists = await _context.PromoCodes.AnyAsync(p =>
                p.IsActive &&
                p.Code == normalizedCode &&
                (!excludeId.HasValue || p.Id != excludeId.Value), cancellationToken);

            if (exists)
                throw new InvalidOperationException("Já existe um cupom ativo com este código no sistema.");
        }

        private static string NormalizeCode(string code) =>
            (code ?? string.Empty).Trim().ToUpperInvariant();

        private static PromoCodeDto MapToDto(PromoCode promo) => new()
        {
            Id = promo.Id,
            Code = promo.Code,
            Type = promo.Type,
            Value = promo.Value,
            Scope = promo.Scope,
            ValidFrom = promo.ValidFrom,
            ValidTo = promo.ValidTo,
            IsActive = promo.IsActive,
            MaxRedemptions = promo.MaxRedemptions,
            RedemptionCount = promo.RedemptionCount,
            CoworkingId = promo.CoworkingId,
            CoworkingName = promo.Coworking?.Name,
            CreatedByUserId = promo.CreatedByUserId,
            CreatedAt = promo.CreatedAt
        };
    }
}
