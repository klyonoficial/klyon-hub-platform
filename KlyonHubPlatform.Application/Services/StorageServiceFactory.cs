using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.Extensions.Configuration;

namespace KlyonHubPlatform.Application.Services
{
    public class StorageServiceFactory
    {
        private readonly ISystemConfigurationService _systemConfigService;
        private readonly AWSStorageService _awsStorageService;
        private readonly FTPStorageService _ftpStorageService;

        public StorageServiceFactory(
            ISystemConfigurationService systemConfigService,
            AWSStorageService awsStorageService,
            FTPStorageService ftpStorageService)
        {
            _systemConfigService = systemConfigService;
            _awsStorageService = awsStorageService;
            _ftpStorageService = ftpStorageService;
        }

        public async Task<IStorageService> GetStorageServiceAsync()
        {
            var provider = await _systemConfigService.GetValueAsync(StorageConfigKeys.Provider);
            
            // Se não houver provider configurado ou estiver vazio, verifica FTP primeiro
            if (string.IsNullOrEmpty(provider) || string.IsNullOrWhiteSpace(provider))
            {
                // Verifica se FTP está configurado
                var ftpHost = await _systemConfigService.GetValueAsync(FtpConfigKey.Host.ToConfigKey());
                var ftpUsername = await _systemConfigService.GetValueAsync(FtpConfigKey.Username.ToConfigKey());
                var ftpPassword = await _systemConfigService.GetValueAsync(FtpConfigKey.Password.ToConfigKey());
                
                if (!string.IsNullOrEmpty(ftpHost) && !string.IsNullOrEmpty(ftpUsername) && !string.IsNullOrEmpty(ftpPassword))
                {
                    return _ftpStorageService;
                }
                // Se FTP não estiver configurado, tenta AWS
                return _awsStorageService;
            }
            
            // Normalizar o provider (remover espaços e converter para maiúsculas)
            var normalizedProvider = provider.Trim().ToUpper();
            
            return normalizedProvider switch
            {
                _ when normalizedProvider == FtpDefaults.ProviderName => _ftpStorageService,
                "AWS" or "S3" => _awsStorageService,
                _ => _ftpStorageService // Default para FTP se não reconhecer (mais seguro)
            };
        }
    }
}
