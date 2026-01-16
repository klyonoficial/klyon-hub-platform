using KlyonHubPlatform.Application.Interfaces;
using FluentFTP;
using Microsoft.Extensions.Configuration;

namespace KlyonHubPlatform.Application.Services
{
    public class FTPStorageService : IStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly ISystemConfigurationService _systemConfigService;
        private FtpClient? _ftpClient;

        public FTPStorageService(IConfiguration configuration, ISystemConfigurationService systemConfigService)
        {
            _configuration = configuration;
            _systemConfigService = systemConfigService;
        }

        private async Task<FtpClient> GetFtpClientAsync()
        {
            if (_ftpClient != null && _ftpClient.IsConnected)
                return _ftpClient;

            var host = await _systemConfigService.GetValueAsync("FTP.Host");
            var portStr = await _systemConfigService.GetValueAsync("FTP.Port") ?? "21";
            var username = await _systemConfigService.GetValueAsync("FTP.Username");
            var password = await _systemConfigService.GetValueAsync("FTP.Password");
            var usePassiveStr = await _systemConfigService.GetValueAsync("FTP.UsePassiveMode") ?? "true";
            var useSslStr = await _systemConfigService.GetValueAsync("FTP.UseSSL") ?? "false";

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Credenciais FTP não configuradas");

            if (!int.TryParse(portStr, out var port))
                port = 21;

            var usePassive = usePassiveStr.ToLower() == "true";
            var useSsl = useSslStr.ToLower() == "true";

            _ftpClient = new FtpClient(host, username, password, port);

            if (useSsl)
            {
                _ftpClient.Config.EncryptionMode = FtpEncryptionMode.Explicit;
                _ftpClient.Config.ValidateAnyCertificate = true; // Em produção, use certificados válidos
            }

            _ftpClient.Config.DataConnectionType = usePassive ? FtpDataConnectionType.PASV : FtpDataConnectionType.PORT;

            if (!_ftpClient.IsConnected)
            {
                await Task.Run(() => _ftpClient.Connect());
            }

            return _ftpClient;
        }

        private async Task<string> GetRootPathAsync()
        {
            var rootPath = await _systemConfigService.GetValueAsync("FTP.RootPath") ?? "/";
            return rootPath.TrimEnd('/');
        }

        public async Task<string> SaveFileAsync(byte[] fileContent, string fileName, string folder)
        {
            var client = await GetFtpClientAsync();
            var rootPath = await GetRootPathAsync();

            var folderPath = folder.Trim('/');
            var remoteDirectory = string.IsNullOrEmpty(folderPath) 
                ? rootPath 
                : $"{rootPath}/{folderPath}";

            // Garante que o diretório existe
            if (!await Task.Run(() => client.DirectoryExists(remoteDirectory)))
            {
                await Task.Run(() => client.CreateDirectory(remoteDirectory));
            }

            var remotePath = $"{remoteDirectory}/{fileName}";

            using var stream = new MemoryStream(fileContent);
            await Task.Run(() => client.UploadStream(stream, remotePath));

            return remotePath;
        }

        public async Task<byte[]?> GetFileAsync(string filePath)
        {
            try
            {
                var client = await GetFtpClientAsync();

                if (!await Task.Run(() => client.FileExists(filePath)))
                    return null;

                using var stream = new MemoryStream();
                await Task.Run(() => client.DownloadStream(stream, filePath));
                return stream.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                var client = await GetFtpClientAsync();

                if (await Task.Run(() => client.FileExists(filePath)))
                {
                    await Task.Run(() => client.DeleteFile(filePath));
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> FileExistsAsync(string filePath)
        {
            try
            {
                var client = await GetFtpClientAsync();
                return await Task.Run(() => client.FileExists(filePath));
            }
            catch
            {
                return false;
            }
        }
    }
}
