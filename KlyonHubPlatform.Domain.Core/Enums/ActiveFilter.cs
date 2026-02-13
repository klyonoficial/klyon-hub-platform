namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Filtro por estado ativo/inativo em listagens (ex.: localidades, usuários, parceiros).
    /// </summary>
    public enum ActiveFilter
    {
        /// <summary>Não filtrar por ativo/inativo.</summary>
        All = 0,

        /// <summary>Somente registros ativos.</summary>
        Active = 1,

        /// <summary>Somente registros inativos.</summary>
        Inactive = 2
    }
}
