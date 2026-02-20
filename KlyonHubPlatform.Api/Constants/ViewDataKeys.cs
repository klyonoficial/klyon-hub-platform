namespace KlyonHubPlatform.Api.Constants
{
    /// <summary>
    /// Chaves usadas em ViewData para evitar strings fixas (ex.: "Title", "EnableCoworkingMenus").
    /// </summary>
    public static class ViewDataKeys
    {
        /// <summary>Título da página (exibido no &lt;title&gt; e no layout).</summary>
        public const string Title = "Title";

        /// <summary>Indica se os menus de coworking devem ser exibidos (feature flag).</summary>
        public const string EnableCoworkingMenus = "EnableCoworkingMenus";

        /// <summary>Mensagem de erro para exibir na view.</summary>
        public const string Error = "Error";

        /// <summary>Mensagem de sucesso para exibir na view.</summary>
        public const string Success = "Success";

        /// <summary>URL de retorno após login/ação.</summary>
        public const string ReturnUrl = "ReturnUrl";

        /// <summary>Mensagem informativa genérica.</summary>
        public const string Message = "Message";

        /// <summary>Indica se o endereço possui endereço fiscal.</summary>
        public const string HasFiscalAddress = "HasFiscalAddress";

        /// <summary>Indica se possui aluguel de salas.</summary>
        public const string HasRoomRental = "HasRoomRental";
    }
}
