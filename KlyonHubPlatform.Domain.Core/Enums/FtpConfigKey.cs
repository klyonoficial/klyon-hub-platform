namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Chaves de configuração do servidor FTP.
    /// </summary>
    public enum FtpConfigKey
    {
        Host,
        Port,
        Username,
        Password,
        RootPath,
        ContractsFolder,
        PhotosFolder,
        UsePassiveMode,
        UseSSL
    }

    /// <summary>
    /// Extensões para obter a chave de configuração do enum.
    /// </summary>
    public static class FtpConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<FtpConfigKey, string> Keys = new Dictionary<FtpConfigKey, string>
        {
            { FtpConfigKey.Host, "FTP.Host" },
            { FtpConfigKey.Port, "FTP.Port" },
            { FtpConfigKey.Username, "FTP.Username" },
            { FtpConfigKey.Password, "FTP.Password" },
            { FtpConfigKey.RootPath, "FTP.RootPath" },
            { FtpConfigKey.ContractsFolder, "FTP.ContractsFolder" },
            { FtpConfigKey.PhotosFolder, "FTP.PhotosFolder" },
            { FtpConfigKey.UsePassiveMode, "FTP.UsePassiveMode" },
            { FtpConfigKey.UseSSL, "FTP.UseSSL" }
        };

        public static string ToConfigKey(this FtpConfigKey key) => Keys[key];

        private static readonly IReadOnlyDictionary<FtpConfigKey, string> Descriptions = new Dictionary<FtpConfigKey, string>
        {
            { FtpConfigKey.Host, "Endereço do servidor FTP" },
            { FtpConfigKey.Port, "Porta do servidor FTP (padrão: 21)" },
            { FtpConfigKey.Username, "Usuário para acesso ao servidor FTP" },
            { FtpConfigKey.Password, "Senha para acesso ao servidor FTP" },
            { FtpConfigKey.RootPath, "Caminho raiz no servidor FTP para armazenamento" },
            { FtpConfigKey.ContractsFolder, "Pasta no servidor FTP para armazenar contratos" },
            { FtpConfigKey.PhotosFolder, "Pasta no servidor FTP para armazenar fotos dos coworkings" },
            { FtpConfigKey.UsePassiveMode, "Usar modo passivo no FTP (recomendado para firewalls)" },
            { FtpConfigKey.UseSSL, "Usar SSL/TLS para conexão FTP (FTPS)" }
        };

        public static string GetDescription(this FtpConfigKey key) => Descriptions[key];
    }

    /// <summary>
    /// Valores padrão para configurações FTP.
    /// </summary>
    public static class FtpDefaults
    {
        /// <summary>Nome do provider de storage FTP.</summary>
        public const string ProviderName = "FTP";

        public const int Port = 21;
        public const string PortString = "21";
        public const string UsePassive = KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.True;
        public const string UseSsl = KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.False;
        public const string RootPath = "/";
        public const string ContractsFolder = "contracts";

        /// <summary>String que indica valor verdadeiro em config (true/True/TRUE).</summary>
        public const string True = KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.True;

        /// <summary>String que indica valor falso em config (false/False/FALSE).</summary>
        public const string False = KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.False;
    }

    /// <summary>
    /// Mensagens de erro relacionadas ao FTP.
    /// </summary>
    public static class FtpErrorMessages
    {
        public const string CredentialsNotConfigured = "Credenciais FTP não configuradas";
    }
}
