namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IClicksignService
    {
        Task<bool> IsConfiguredAsync();
        Task<string?> SendContractForSignatureAsync(
            byte[] pdfBytes,
            string fileName,
            string signerEmail,
            string? signerName = null,
            string? message = null);
    }
}