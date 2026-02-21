using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Application.Services
{
    public class StorageServiceFactory
    {
        private readonly ISystemConfigurationService _systemConfigService;
        private readonly AWSStorageService _awsStorageService;
        private readonly FTPStorageService _ftpStorageService;
        private readonly OracleStorageService _oracleStorageService;
        public StorageServiceFactory(
            ISystemConfigurationService systemConfigService,
            AWSStorageService awsStorageService,
            FTPStorageService ftpStorageService,
            OracleStorageService oracleStorageService)
        {
            _systemConfigService = systemConfigService;
            _awsStorageService = awsStorageService;
            _ftpStorageService = ftpStorageService;
            _oracleStorageService = oracleStorageService;
        }
        public async Task<IStorageService> GetStorageServiceAsync()
        {
            var provider = await _systemConfigService.GetValueAsync(StorageConfigKeys.Provider);
            var normalizedProvider = (provider ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(normalizedProvider))
            {
                var key = normalizedProvider.ToUpperInvariant();
                if (key == OracleStorageDefaults.ProviderName.ToUpperInvariant())
                    return _oracleStorageService;
                if (key == FtpDefaults.ProviderName.ToUpperInvariant())
                    return _ftpStorageService;
                if (key == StorageProviderNames.Aws || key == StorageProviderNames.S3)
                    return _awsStorageService;
                return _oracleStorageService;
            }
            var oracleNamespace = await _systemConfigService.GetValueAsync(OracleStorageConfigKey.Namespace.ToConfigKey());
            var oracleAccessKey = await _systemConfigService.GetValueAsync(OracleStorageConfigKey.AccessKeyId.ToConfigKey());
            var oracleSecret = await _systemConfigService.GetValueAsync(OracleStorageConfigKey.SecretAccessKey.ToConfigKey());
            var oracleBucket = await _systemConfigService.GetValueAsync(OracleStorageConfigKey.BucketName.ToConfigKey());
            if (!string.IsNullOrEmpty(oracleNamespace) && !string.IsNullOrEmpty(oracleAccessKey) && !string.IsNullOrEmpty(oracleSecret) && !string.IsNullOrEmpty(oracleBucket))
                return _oracleStorageService;
            var ftpHost = await _systemConfigService.GetValueAsync(FtpConfigKey.Host.ToConfigKey());
            var ftpUsername = await _systemConfigService.GetValueAsync(FtpConfigKey.Username.ToConfigKey());
            var ftpPassword = await _systemConfigService.GetValueAsync(FtpConfigKey.Password.ToConfigKey());
            if (!string.IsNullOrEmpty(ftpHost) && !string.IsNullOrEmpty(ftpUsername) && !string.IsNullOrEmpty(ftpPassword))
                return _ftpStorageService;
            return _awsStorageService;
        }
    }
}