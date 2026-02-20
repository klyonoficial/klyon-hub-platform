using KlyonHubPlatform.Domain.Core.Constants;

namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Extensões para obter textos de exibição do enum CoworkingApprovalStatus.
    /// </summary>
    public static class CoworkingApprovalStatusExtensions
    {
        /// <summary>Texto principal de exibição (dashboard, API).</summary>
        public static string ToDisplayString(this CoworkingApprovalStatus status) => status switch
        {
            CoworkingApprovalStatus.Approved => DisplayStrings.Approved,
            CoworkingApprovalStatus.UnderReview => DisplayStrings.UnderReview,
            CoworkingApprovalStatus.AwaitingAdjustment => DisplayStrings.AwaitingAdjustment,
            _ => EnumDisplayConstants.Unknown
        };

        /// <summary>Texto para lista admin (Coworkings.cshtml) - "Pendente aprovação", "Pendente reprovação", "Aprovado".</summary>
        public static string ToAdminListLabel(this CoworkingApprovalStatus status) => status switch
        {
            CoworkingApprovalStatus.UnderReview => DisplayStrings.AdminPendingApproval,
            CoworkingApprovalStatus.AwaitingAdjustment => DisplayStrings.AdminPendingRejection,
            CoworkingApprovalStatus.Approved => DisplayStrings.Approved,
            _ => DisplayStrings.Approved
        };

        /// <summary>Classe CSS do badge Bootstrap (warning, danger, success).</summary>
        public static string ToBadgeCssClass(this CoworkingApprovalStatus status) => status switch
        {
            CoworkingApprovalStatus.UnderReview => BootstrapBadgeCss.Warning,
            CoworkingApprovalStatus.AwaitingAdjustment => BootstrapBadgeCss.Danger,
            CoworkingApprovalStatus.Approved => BootstrapBadgeCss.Success,
            _ => BootstrapBadgeCss.Secondary
        };

        /// <summary>Converte string (enum.ToString do audit log) para display. Ex: "UnderReview" → "Aguardando Aprovação".</summary>
        public static string ToDisplayStringFromEnumName(string? enumName)
        {
            if (string.IsNullOrEmpty(enumName) || !System.Enum.TryParse<CoworkingApprovalStatus>(enumName, out var status))
                return enumName ?? string.Empty;
            return status.ToDisplayString();
        }

        /// <summary>Strings de exibição centralizadas - evita código fixo em toda a aplicação.</summary>
        public static class DisplayStrings
        {
            public const string Approved = "Aprovado";
            public const string UnderReview = "Aguardando Aprovação";
            public const string AwaitingAdjustment = "Aguardando Ajustes";
            public const string AdminPendingApproval = "Pendente aprovação";
            public const string AdminPendingRejection = "Pendente reprovação";
            public const string AwaitingApprovalShort = "Aguardando aprovação";
            public const string AwaitingAdjustmentShort = "Aguardando ajuste";
        }
    }
}
