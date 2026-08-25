namespace KlyonHubPlatform.Api.Constants
{
    public static class UploadRequestLimits
    {
        /// <summary>
        /// Folga acima dos 15 MB da regra de negócio, alinhada ao nginx <c>client_max_body_size 20m</c>.
        /// </summary>
        public const long MaxRequestBodyBytes = 20L * 1024 * 1024;
    }
}
