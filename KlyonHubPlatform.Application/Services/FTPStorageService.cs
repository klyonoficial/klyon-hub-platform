using KlyonHubPlatform.Application.Interfaces;
using FluentFTP;
using KlyonHubPlatform.Domain.Core.Enums;
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
            var host = await _systemConfigService.GetValueAsync(FtpConfigKey.Host.ToConfigKey());
            var portStr = await _systemConfigService.GetValueAsync(FtpConfigKey.Port.ToConfigKey()) ?? FtpDefaults.PortString;
            var username = await _systemConfigService.GetValueAsync(FtpConfigKey.Username.ToConfigKey());
            var password = await _systemConfigService.GetValueAsync(FtpConfigKey.Password.ToConfigKey());
            var usePassiveStr = await _systemConfigService.GetValueAsync(FtpConfigKey.UsePassiveMode.ToConfigKey()) ?? FtpDefaults.UsePassive;
            var useSslStr = await _systemConfigService.GetValueAsync(FtpConfigKey.UseSSL.ToConfigKey()) ?? FtpDefaults.UseSsl;
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                throw new InvalidOperationException(FtpErrorMessages.CredentialsNotConfigured);
            if (!int.TryParse(portStr, out var port))
                port = FtpDefaults.Port;
            var usePassive = usePassiveStr.Trim().Equals(FtpDefaults.True, StringComparison.OrdinalIgnoreCase);
            var useSsl = useSslStr.Trim().Equals(FtpDefaults.True, StringComparison.OrdinalIgnoreCase);
            _ftpClient = new FtpClient(host, username, password, port);
            if (useSsl)
            {
                _ftpClient.Config.EncryptionMode = FtpEncryptionMode.Explicit;
                _ftpClient.Config.ValidateAnyCertificate = true;
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
            var rootPath = await _systemConfigService.GetValueAsync(FtpConfigKey.RootPath.ToConfigKey()) ?? FtpDefaults.RootPath;
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