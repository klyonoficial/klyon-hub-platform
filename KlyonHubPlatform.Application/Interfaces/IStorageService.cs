namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IStorageService
    {
        /// <summary>
        /// Salva um arquivo no storage
        /// </summary>
        /// <param name="fileContent">Conteúdo do arquivo em bytes</param>
        /// <param name="fileName">Nome do arquivo</param>
        /// <param name="folder">Pasta onde o arquivo será salvo</param>
        /// <returns>URL ou caminho do arquivo salvo</returns>
        Task<string> SaveFileAsync(byte[] fileContent, string fileName, string folder);

        /// <summary>
        /// Obtém o conteúdo de um arquivo do storage
        /// </summary>
        /// <param name="filePath">Caminho ou URL do arquivo</param>
        /// <returns>Conteúdo do arquivo em bytes</returns>
        Task<byte[]?> GetFileAsync(string filePath);

        /// <summary>
        /// Deleta um arquivo do storage
        /// </summary>
        /// <param name="filePath">Caminho ou URL do arquivo</param>
        Task<bool> DeleteFileAsync(string filePath);

        /// <summary>
        /// Verifica se um arquivo existe no storage
        /// </summary>
        /// <param name="filePath">Caminho ou URL do arquivo</param>
        Task<bool> FileExistsAsync(string filePath);
    }
}
