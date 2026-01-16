using KlyonHubPlatform.Application.Interfaces;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon;
using Microsoft.Extensions.Configuration;

namespace KlyonHubPlatform.Application.Services
{
    public class AWSStorageService : IStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly ISystemConfigurationService _systemConfigService;
        private AmazonS3Client? _s3Client;
        private string? _bucketName;
        private string? _region;

        public AWSStorageService(IConfiguration configuration, ISystemConfigurationService systemConfigService)
        {
            _configuration = configuration;
            _systemConfigService = systemConfigService;
        }

        private async Task<AmazonS3Client> GetS3ClientAsync()
        {
            if (_s3Client != null)
                return _s3Client;

            var accessKeyId = await _systemConfigService.GetValueAsync("AWS.S3.AccessKeyId");
            var secretAccessKey = await _systemConfigService.GetValueAsync("AWS.S3.SecretAccessKey");
            _region = await _systemConfigService.GetValueAsync("AWS.S3.Region") ?? "us-east-1";

            if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
                throw new InvalidOperationException("Credenciais AWS S3 não configuradas. Configure as credenciais AWS S3 nas Configurações do Sistema ou altere o Storage.Provider para FTP.");

            var regionEndpoint = RegionEndpoint.GetBySystemName(_region);
            _s3Client = new AmazonS3Client(accessKeyId, secretAccessKey, regionEndpoint);

            return _s3Client;
        }

        private async Task<string> GetBucketNameAsync()
        {
            if (!string.IsNullOrEmpty(_bucketName))
                return _bucketName;

            _bucketName = await _systemConfigService.GetValueAsync("AWS.S3.BucketName");
            if (string.IsNullOrEmpty(_bucketName))
                throw new InvalidOperationException("Nome do bucket S3 não configurado");

            return _bucketName;
        }

        public async Task<string> SaveFileAsync(byte[] fileContent, string fileName, string folder)
        {
            var client = await GetS3ClientAsync();
            var bucketName = await GetBucketNameAsync();

            var folderPath = folder.Trim('/');
            var key = string.IsNullOrEmpty(folderPath) ? fileName : $"{folderPath}/{fileName}";

            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = new MemoryStream(fileContent),
                ContentType = GetContentType(fileName)
            };

            await client.PutObjectAsync(request);

            // Retorna a URL do arquivo
            return $"s3://{bucketName}/{key}";
        }

        public async Task<byte[]?> GetFileAsync(string filePath)
        {
            try
            {
                var client = await GetS3ClientAsync();
                var bucketName = await GetBucketNameAsync();

                // Extrai a chave do caminho s3://bucket/key ou apenas key
                var key = filePath.StartsWith("s3://") 
                    ? filePath.Substring(filePath.IndexOf('/', 5) + 1)
                    : filePath;

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
                var client = await GetS3ClientAsync();
                var bucketName = await GetBucketNameAsync();

                var key = filePath.StartsWith("s3://")
                    ? filePath.Substring(filePath.IndexOf('/', 5) + 1)
                    : filePath;

                var request = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = key
                };

                await client.DeleteObjectAsync(request);
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
                var client = await GetS3ClientAsync();
                var bucketName = await GetBucketNameAsync();

                var key = filePath.StartsWith("s3://")
                    ? filePath.Substring(filePath.IndexOf('/', 5) + 1)
                    : filePath;

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

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }
    }
}
