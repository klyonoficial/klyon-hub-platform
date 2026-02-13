namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Chaves de configuração do AWS S3.
    /// </summary>
    public enum AwsS3ConfigKey
    {
        AccessKeyId,
        SecretAccessKey,
        Region,
        BucketName,
        ContractsFolder,
        PhotosFolder
    }

    /// <summary>
    /// Extensões para obter a chave de configuração do enum.
    /// </summary>
    public static class AwsS3ConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<AwsS3ConfigKey, string> Keys = new Dictionary<AwsS3ConfigKey, string>
        {
            { AwsS3ConfigKey.AccessKeyId, "AWS.S3.AccessKeyId" },
            { AwsS3ConfigKey.SecretAccessKey, "AWS.S3.SecretAccessKey" },
            { AwsS3ConfigKey.Region, "AWS.S3.Region" },
            { AwsS3ConfigKey.BucketName, "AWS.S3.BucketName" },
            { AwsS3ConfigKey.ContractsFolder, "AWS.S3.ContractsFolder" },
            { AwsS3ConfigKey.PhotosFolder, "AWS.S3.PhotosFolder" }
        };

        public static string ToConfigKey(this AwsS3ConfigKey key) => Keys[key];
    }

    /// <summary>
    /// Valores padrão para configurações AWS S3.
    /// </summary>
    public static class AwsS3Defaults
    {
        public const string Region = "us-east-1";
        public const string ContractsFolder = "contracts";
        public const string PhotosFolder = "coworking-photos";
    }

    /// <summary>
    /// Mensagens de erro relacionadas ao AWS S3.
    /// </summary>
    public static class AwsS3ErrorMessages
    {
        public const string CredentialsNotConfigured = "Credenciais AWS S3 não configuradas. Configure as credenciais AWS S3 nas Configurações do Sistema ou altere o Storage.Provider para FTP.";
        public const string BucketNameNotConfigured = "Nome do bucket S3 não configurado";
    }
}
