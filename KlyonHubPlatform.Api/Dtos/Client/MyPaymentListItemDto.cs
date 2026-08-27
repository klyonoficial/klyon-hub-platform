using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Api.Dtos.Client
{
    public class MyPaymentListItemDto
    {
        public bool IsPredicted { get; set; }
        public int? PaymentId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentMethod Method { get; set; }
        public PaymentStatus? Status { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? InstallmentCount { get; set; }
        public decimal? InstallmentValue { get; set; }
        public string? PredictedStatusLabel { get; set; }
        public string? PredictedExplanation { get; set; }
        public PaymentClientDestination Destination { get; set; } = PaymentClientDestination.None;
        public bool ShowActionLink { get; set; }
        public string ActionLabel { get; set; } = string.Empty;
        public string ActionIconCss { get; set; } = "bi-eye";

        public static MyPaymentListItemDto FromPayment(Payment payment)
        {
            var navigation = PaymentNavigationHelper.Resolve(payment);
            return new()
            {
                IsPredicted = false,
                PaymentId = payment.Id,
                ContractNumber = payment.Subscription?.Contract?.ContractNumber ?? string.Empty,
                Address = payment.Subscription?.Contract?.CoworkingAddress?.Address
                    ?? payment.Subscription?.Contract?.SnapshotAddress
                    ?? string.Empty,
                Amount = payment.Amount,
                Method = payment.Method,
                Status = payment.Status,
                DueDate = payment.DueDate,
                PaidAt = payment.PaidAt,
                CreatedAt = payment.CreatedAt,
                InstallmentCount = payment.InstallmentCount,
                InstallmentValue = payment.InstallmentValue,
                Destination = navigation.Destination,
                ShowActionLink = navigation.ShowActionLink,
                ActionLabel = navigation.ActionLabel,
                ActionIconCss = navigation.ActionIconCss
            };
        }

        /// <summary>
        /// PO: Aguardando Pagamento → vencido/falha → Previsto → processamento → Pago → cancelado.
        /// </summary>
        public static List<MyPaymentListItemDto> OrderForList(IEnumerable<MyPaymentListItemDto> items)
        {
            return items.OrderBy(i => i, Comparer<MyPaymentListItemDto>.Create(CompareForList)).ToList();
        }

        /// <summary>
        /// 0 Aguardando | 1 Vencido/Falhou | 2 Previsto | 3 Processando | 4 Pago | 5 Cancelado/Estornado
        /// </summary>
        private static int GetListSortGroup(MyPaymentListItemDto item)
        {
            if (item.IsPredicted)
                return 2;

            return item.Status switch
            {
                PaymentStatus.Pending => BrazilDateTime.IsCalendarDueDateOverdue(item.DueDate) ? 1 : 0,
                PaymentStatus.Failed => 1,
                PaymentStatus.Processing => 3,
                PaymentStatus.AwaitingValidation => 3,
                PaymentStatus.Paid => 4,
                PaymentStatus.Cancelled => 5,
                PaymentStatus.Refunded => 5,
                _ => 6
            };
        }

        private static int CompareForList(MyPaymentListItemDto a, MyPaymentListItemDto b)
        {
            var groupCompare = GetListSortGroup(a).CompareTo(GetListSortGroup(b));
            if (groupCompare != 0)
                return groupCompare;

            var group = GetListSortGroup(a);
            var withinGroup = group switch
            {
                0 or 1 or 2 or 3 => a.DueDate.CompareTo(b.DueDate),
                4 => (b.PaidAt ?? DateTime.MinValue).CompareTo(a.PaidAt ?? DateTime.MinValue),
                _ => b.DueDate.CompareTo(a.DueDate)
            };
            if (withinGroup != 0)
                return withinGroup;

            return b.CreatedAt.CompareTo(a.CreatedAt);
        }
    }
}
