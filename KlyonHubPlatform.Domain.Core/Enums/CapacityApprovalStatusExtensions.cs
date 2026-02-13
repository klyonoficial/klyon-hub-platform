using KlyonHubPlatform.Domain.Core.Constants;

namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Extensões para obter textos de exibição do enum CapacityApprovalStatus.
    /// </summary>
    public static class CapacityApprovalStatusExtensions
    {
        public static string ToDisplayString(this CapacityApprovalStatus status) => status switch
        {
            CapacityApprovalStatus.None => DisplayStrings.NoneLabel,
            CapacityApprovalStatus.Pending => DisplayStrings.Pending,
            CapacityApprovalStatus.Approved => DisplayStrings.Approved,
            CapacityApprovalStatus.Rejected => DisplayStrings.Rejected,
            _ => EnumDisplayConstants.None
        };

        public static class DisplayStrings
        {
            public const string NoneLabel = "Nenhum";
            public const string Pending = "Pendente";
            public const string Approved = "Aprovado";
            public const string Rejected = "Rejeitado";
        }
    }
}
