namespace KlyonHubPlatform.Api.Constants
{
    /// <summary>
    /// Caminhos de layout usados nas views (Layout = "...").
    /// Use estas constantes em vez de literais "~/Views/Shared/_Layout.cshtml".
    /// </summary>
    public static class LayoutPaths
    {
        /// <summary>Layout principal da aplicação.</summary>
        public const string Layout = "~/Views/Shared/_Layout.cshtml";

        /// <summary>Layout minimal (ex.: termos, páginas embarcadas).</summary>
        public const string LayoutMinimal = "~/Views/Shared/_LayoutMinimal.cshtml";

        /// <summary>Layout da landing (página inicial, marketplace).</summary>
        public const string LandingLayout = "~/Views/Shared/_LandingLayout.cshtml";
    }
}
