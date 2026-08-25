using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Helpers
{
    public static class PaymentHistoryDisplayHelper
    {
        public static (string Title, string Description) GetApprovedTimelineCopy(
            PaymentHistory history,
            PaymentMethod paymentMethod)
        {
            if (IsAutomaticGatewayConfirmation(history, paymentMethod))
            {
                return (
                    "Pagamento Confirmado",
                    GetAutomaticConfirmationDescription(paymentMethod));
            }

            var approver = FormatManualApprover(history);
            return (
                "Pagamento Aprovado",
                $"Aprovado por {approver}.");
        }

        public static string FormatActionByLabel(PaymentHistory history, PaymentMethod paymentMethod)
        {
            if (IsAutomaticGatewayConfirmation(history, paymentMethod))
                return GetAutomaticConfirmationShortLabel(paymentMethod);

            return FormatManualApprover(history);
        }

        public static bool IsAutomaticGatewayConfirmation(PaymentHistory history, PaymentMethod paymentMethod)
        {
            if (history.ActionBy.HasValue)
                return false;

            return paymentMethod is PaymentMethod.CreditCard or PaymentMethod.PIX;
        }

        private static string GetAutomaticConfirmationDescription(PaymentMethod paymentMethod) =>
            paymentMethod switch
            {
                PaymentMethod.CreditCard => "Confirmado automaticamente via cartão de crédito (Asaas).",
                PaymentMethod.PIX => "Confirmado automaticamente via PIX (Asaas).",
                _ => "Confirmado automaticamente via gateway de pagamento."
            };

        private static string GetAutomaticConfirmationShortLabel(PaymentMethod paymentMethod) =>
            paymentMethod switch
            {
                PaymentMethod.CreditCard => "Sistema — cartão de crédito (Asaas)",
                PaymentMethod.PIX => "Sistema — PIX (Asaas)",
                _ => "Sistema — gateway de pagamento"
            };

        private static string FormatManualApprover(PaymentHistory history)
        {
            var name = string.IsNullOrWhiteSpace(history.ActionByUser?.FullName)
                ? DefaultDisplayStrings.SystemAdminLabel
                : history.ActionByUser!.FullName.Trim();
            var role = ResolveApproverRoleDisplay(history);
            return string.IsNullOrWhiteSpace(role) ? name : $"{name} ({role})";
        }

        private static string? ResolveApproverRoleDisplay(PaymentHistory history)
        {
            var roleNames = history.ActionByUser?.UserRoles?
                .Select(ur => ur.Role?.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .ToList();

            if (roleNames == null || roleNames.Count == 0)
                return history.ActionBy.HasValue ? RoleNames.DisplayNames.Administrator : null;

            if (roleNames.Contains(RoleNames.Administrator))
                return RoleNames.DisplayNames.Administrator;
            if (roleNames.Contains(RoleNames.CoworkingPartner))
                return RoleNames.DisplayNames.CoworkingPartner;
            if (roleNames.Contains(RoleNames.Client))
                return RoleNames.DisplayNames.Client;

            return RoleNames.GetDisplayName(roleNames[0]);
        }
    }
}
