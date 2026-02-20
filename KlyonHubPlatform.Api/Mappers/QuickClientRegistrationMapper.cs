using KlyonHubPlatform.Api.Responses;
using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Api.Mappers
{
    public static class QuickClientRegistrationMapper
    {
        public static QuickClientRegistrationResponse ToResponse(
            bool success,
            string message,
            Contract contract,
            Client client,
            User user,
            Payment? payment,
            string? pixCode,
            bool isNewUser)
        {
            return new QuickClientRegistrationResponse
            {
                Success = success,
                Message = message,
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                ClientId = client.Id,
                PaymentId = payment?.Id,
                PixCode = pixCode,
                UserId = user.Id,
                IsNewUser = isNewUser
            };
        }
        public static QuickClientRegistrationResponse ToError(bool success, string message)
        {
            return new QuickClientRegistrationResponse
            {
                Success = success,
                Message = message
            };
        }
    }
}