using KlyonHubPlatform.Application.Interfaces;
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
            var provider = await _systemConfigService.GetValueAsync("Storage.Provider");
            
            // Se não houver provider configurado, tenta usar FTP primeiro (mais comum em desenvolvimento)
            if (string.IsNullOrEmpty(provider))
            {
                // Verifica se FTP está configurado
                var ftpHost = await _systemConfigService.GetValueAsync("FTP.Host");
                if (!string.IsNullOrEmpty(ftpHost))
                {
                    return _ftpStorageService;
                }
                // Se FTP não estiver configurado, tenta AWS
                return _awsStorageService;
            }
            
            return provider.ToUpper() switch
            {
                "FTP" => _ftpStorageService,
                "AWS" or "S3" => _awsStorageService,
                _ => _awsStorageService // Default para AWS
            };
        }
    }
}
