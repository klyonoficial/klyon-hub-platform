namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IStorageService
    {
        Task<string> SaveFileAsync(byte[] fileContent, string fileName, string folder);
        Task<byte[]?> GetFileAsync(string filePath);
        Task<bool> DeleteFileAsync(string filePath);
        Task<bool> FileExistsAsync(string filePath);
    }
}