namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum OracleStorageConfigKey
    {
        Namespace,
        Region,
        BucketName,
        AccessKeyId,
        SecretAccessKey,
        ContractsFolder,
        PhotosFolder
    }
    public static class OracleStorageConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<OracleStorageConfigKey, string> Keys = new Dictionary<OracleStorageConfigKey, string>
        {
            { OracleStorageConfigKey.Namespace, "Oracle.OS.Namespace" },
            { OracleStorageConfigKey.Region, "Oracle.OS.Region" },
            { OracleStorageConfigKey.BucketName, "Oracle.OS.BucketName" },
            { OracleStorageConfigKey.AccessKeyId, "Oracle.OS.AccessKeyId" },
            { OracleStorageConfigKey.SecretAccessKey, "Oracle.OS.SecretAccessKey" },
            { OracleStorageConfigKey.ContractsFolder, "Oracle.OS.ContractsFolder" },
            { OracleStorageConfigKey.PhotosFolder, "Oracle.OS.PhotosFolder" }
        };
        private static readonly IReadOnlyDictionary<OracleStorageConfigKey, string> Descriptions = new Dictionary<OracleStorageConfigKey, string>
        {
            { OracleStorageConfigKey.Namespace, "Namespace do tenancy no Oracle Cloud (ex.: axabcdefghij). Encontre em: Object Storage > Buckets > Namespace" },
            { OracleStorageConfigKey.Region, "Identificador da região OCI (ex.: sa-saopaulo-1, us-ashburn-1). Lista em: https://docs.oracle.com/en-us/iaas/Content/General/Concepts/regions.htm" },
            { OracleStorageConfigKey.BucketName, "Nome do bucket no Object Storage" },
            { OracleStorageConfigKey.AccessKeyId, "Access Key do Customer Secret Key (Identity > Users > Customer secret keys)" },
            { OracleStorageConfigKey.SecretAccessKey, "Secret Key do Customer Secret Key (copiada apenas na criação)" },
            { OracleStorageConfigKey.ContractsFolder, "Pasta (prefixo) no bucket para contratos e documentos" },
            { OracleStorageConfigKey.PhotosFolder, "Pasta (prefixo) no bucket para fotos dos coworkings" }
        };
        public static string ToConfigKey(this OracleStorageConfigKey key) => Keys[key];
        public static string GetDescription(this OracleStorageConfigKey key) => Descriptions[key];
    }
    public static class OracleStorageDefaults
    {
        public const string ProviderName = "Oracle";
        public const string Region = "sa-saopaulo-1";
        public const string ContractsFolder = "contracts";
        public const string PhotosFolder = "coworking-photos";
    }
    public static class OracleStorageErrorMessages
    {
        public const string CredentialsNotConfigured = "Credenciais Oracle Object Storage não configuradas. Configure em Configurações do Sistema (Armazenamento) ou altere Storage.Provider para FTP.";
        public const string BucketNameNotConfigured = "Nome do bucket Oracle Object Storage não configurado";
        public const string NamespaceNotConfigured = "Namespace Oracle não configurado";
    }
}