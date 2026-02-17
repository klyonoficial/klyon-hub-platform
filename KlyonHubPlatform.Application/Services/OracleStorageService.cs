using KlyonHubPlatform.Application.Interfaces;
using Amazon.S3;
using Amazon.S3.Model;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.Extensions.Configuration;

namespace KlyonHubPlatform.Application.Services
{
    /// <summary>
    /// Armazenamento em Oracle Cloud Object Storage (via S3 Compatibility API).
    /// </summary>
    public class OracleStorageService : IStorageService
    {
        /// <summary>
        /// Cliente para Oracle Cloud Object Storage (usa S3 Compatibility API internamente; não é cliente AWS).
        /// </summary>
        private sealed class OracleObjectStorageClient
        {
            private readonly AmazonS3Client _s3CompatibilityClient;

            public OracleObjectStorageClient(AmazonS3Client s3CompatibilityClient)
            {
                _s3CompatibilityClient = s3CompatibilityClient;
            }

            public Task PutObjectAsync(PutObjectRequest request) => _s3CompatibilityClient.PutObjectAsync(request);
            public Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request) => _s3CompatibilityClient.GetObjectAsync(request);
            public Task DeleteObjectAsync(string bucketName, string key) => _s3CompatibilityClient.DeleteObjectAsync(bucketName, key);
            public Task GetObjectMetadataAsync(GetObjectMetadataRequest request) => _s3CompatibilityClient.GetObjectMetadataAsync(request);
        }

        private readonly IConfiguration _configuration;
        private readonly ISystemConfigurationService _systemConfigService;
        private OracleObjectStorageClient? _oracleClient;
        private string? _bucketName;
        private string? _serviceUrl;

        public OracleStorageService(IConfiguration configuration, ISystemConfigurationService systemConfigService)
        {
            _configuration = configuration;
            _systemConfigService = systemConfigService;
        }

        /// <summary>
        /// Retorna o cliente para Oracle Cloud Object Storage (não AWS). Usa S3 Compatibility API contra o endpoint Oracle.
        /// </summary>
        private async Task<OracleObjectStorageClient> GetClientAsync()
        {
            if (_oracleClient != null)
                return _oracleClient;

            var namespaceName = await _systemConfigService.GetValueAsync(OracleStorageConfigKey.Namespace.ToConfigKey());
            var region = await _systemConfigService.GetValueAsync(OracleStorageConfigKey.Region.ToConfigKey()) ?? OracleStorageDefaults.Region;
            var accessKeyId = await _systemConfigService.GetValueAsync(OracleStorageConfigKey.AccessKeyId.ToConfigKey());
            var secretAccessKey = await _systemConfigService.GetValueAsync(OracleStorageConfigKey.SecretAccessKey.ToConfigKey());

            if (string.IsNullOrEmpty(namespaceName))
                throw new InvalidOperationException(OracleStorageErrorMessages.NamespaceNotConfigured);
            if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
                throw new InvalidOperationException(OracleStorageErrorMessages.CredentialsNotConfigured);

            // Endpoint Oracle S3 Compatibility API (path-style). Não usar RegionEndpoint para não apontar para AWS.
            _serviceUrl = $"https://{namespaceName.Trim()}.compat.objectstorage.{region.Trim()}.oci.customer-oci.com";

            var config = new AmazonS3Config
            {
                ServiceURL = _serviceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = region.Trim()
            };

            var s3CompatibilityClient = new AmazonS3Client(accessKeyId, secretAccessKey, config);
            _oracleClient = new OracleObjectStorageClient(s3CompatibilityClient);
            return _oracleClient;
        }

        private async Task<string> GetBucketNameAsync()
        {
            if (!string.IsNullOrEmpty(_bucketName))
                return _bucketName;

            _bucketName = await _systemConfigService.GetValueAsync(OracleStorageConfigKey.BucketName.ToConfigKey());
            if (string.IsNullOrEmpty(_bucketName))
                throw new InvalidOperationException(OracleStorageErrorMessages.BucketNameNotConfigured);

            return _bucketName;
        }

        public async Task<string> SaveFileAsync(byte[] fileContent, string fileName, string folder)
        {
            try
            {
                var client = await GetClientAsync();
                var bucketName = await GetBucketNameAsync();

                var folderPath = folder.Trim('/');
                var key = string.IsNullOrEmpty(folderPath) ? fileName : $"{folderPath}/{fileName}";

                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    InputStream = new MemoryStream(fileContent),
                    ContentType = GetContentType(fileName),
                    // Oracle S3 Compatibility API não suporta STREAMING-AWS4-HMAC-SHA256-PAYLOAD; desabilita assinatura de payload em streaming
                    DisablePayloadSigning = true
                };

                await client.PutObjectAsync(request);

                return $"oracle://{bucketName}/{key}";
            }
            catch (AmazonS3Exception ex)
            {
                throw new InvalidOperationException(
                    $"Oracle Object Storage: {ex.ErrorCode} - {ex.Message}. Verifique em Admin > Configurações do Sistema > Armazenamento: Oracle.OS.Namespace, Oracle.OS.Region, Oracle.OS.BucketName e credenciais (AccessKeyId/SecretAccessKey).",
                    ex);
            }
            catch (Amazon.Runtime.AmazonServiceException ex)
            {
                throw new InvalidOperationException(
                    $"Oracle Object Storage: {ex.ErrorCode} - {ex.Message}. Verifique as configurações de armazenamento em Admin > Configurações do Sistema.",
                    ex);
            }
            catch (Exception ex) when (ex.GetType().FullName?.Contains("Amazon.Runtime", StringComparison.OrdinalIgnoreCase) == true)
            {
                var code = ex.GetType().GetProperty("ErrorCode")?.GetValue(ex)?.ToString();
                var msg = string.IsNullOrEmpty(code) ? ex.Message : $"{code} - {ex.Message}";
                throw new InvalidOperationException(
                    $"Oracle Object Storage: {msg}. Verifique namespace, região, bucket e credenciais em Admin > Configurações do Sistema > Armazenamento.",
                    ex);
            }
        }

        public async Task<byte[]?> GetFileAsync(string filePath)
        {
            try
            {
                var client = await GetClientAsync();
                var bucketName = await GetBucketNameAsync();

                var key = ParseKey(filePath, bucketName);
                if (key == null)
                    return null;

                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = key
                };

                using var response = await client.GetObjectAsync(request);
                using var memoryStream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                var client = await GetClientAsync();
                var bucketName = await GetBucketNameAsync();
                var key = ParseKey(filePath, bucketName);
                if (key == null)
                    return false;

                await client.DeleteObjectAsync(bucketName, key);
                return true;
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
                var client = await GetClientAsync();
                var bucketName = await GetBucketNameAsync();
                var key = ParseKey(filePath, bucketName);
                if (key == null)
                    return false;

                var request = new GetObjectMetadataRequest
                {
                    BucketName = bucketName,
                    Key = key
                };
                await client.GetObjectMetadataAsync(request);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        /// <summary>
        /// Extrai a chave do objeto a partir do filePath (oracle://bucket/key ou apenas key).
        /// </summary>
        private static string? ParseKey(string filePath, string bucketName)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;
            if (filePath.StartsWith("oracle://", StringComparison.OrdinalIgnoreCase))
            {
                var withoutScheme = filePath.Substring("oracle://".Length);
                var firstSlash = withoutScheme.IndexOf('/');
                if (firstSlash >= 0 && firstSlash < withoutScheme.Length - 1)
                    return withoutScheme.Substring(firstSlash + 1);
                return null;
            }
            return filePath;
        }

        private static string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".xml" => "application/xml",
                _ => "application/octet-stream"
            };
        }
    }
}
