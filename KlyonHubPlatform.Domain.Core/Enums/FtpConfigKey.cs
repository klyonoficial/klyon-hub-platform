namespace KlyonHubPlatform.Domain.Core.Enums
{
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
    public static class FtpDefaults
    {
        public const string ProviderName = "FTP";
        public const int Port = 21;
        public const string PortString = "21";
        /// <summary>Usuário FTP padrão (valor inicial em seed/config). Troque em produção.</summary>
        public const string Username = "gestorhub";
        /// <summary>Senha FTP padrão (valor inicial em seed/config). Troque em produção.</summary>
        public const string Password = "F@z3rd@rC3rt0";
        public const string UsePassive = KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.True;
        public const string UseSsl = KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.False;
        public const string RootPath = "/";
        public const string ContractsFolder = "contracts";
        public const string True = KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.True;
        public const string False = KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.False;
    }
    public static class FtpErrorMessages
    {
        public const string CredentialsNotConfigured = "Credenciais FTP não configuradas";
    }
}