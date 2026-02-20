namespace KlyonHubPlatform.Api.Constants
{
    /// <summary>
    /// Chaves de ação dos alertas do dashboard administrativo (ActionKey).
    /// Usado para identificar o tipo de alerta e renderizar botão/link específico na view.
    /// </summary>
    public static class AdminAlertActionKeys
    {
        /// <summary>Alerta de preços no cartão pendentes de aprovação; abre modal em vez de link.</summary>
        public const string CardPricePending = "cardPricePending";
    }
}
