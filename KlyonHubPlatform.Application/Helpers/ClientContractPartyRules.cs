using KlyonHubPlatform.Application.DTOs.ClientProfile;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Helpers;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Helpers
{
    /// <summary>
    /// Dados essenciais do contratante usados no contrato/declaração.
    /// </summary>
    public static class ClientContractPartyRules
    {
        public const string IncompleteMessage =
            "Não é possível gerar o contrato ou a declaração: faltam dados essenciais do contratante. Preencha nome completo (pessoa física) ou razão social (pessoa jurídica), CPF/CNPJ, endereço completo, telefone e e-mail em Dados para o contrato.";

        public static bool ClientContractPartyIsComplete(Client? client) =>
            Evaluate(client).IsComplete;

        public static ClientContractPartyStatus Evaluate(Client? client)
        {
            var missing = new List<string>();
            if (client == null)
            {
                missing.Add("Cadastro do cliente");
                return Incomplete(missing);
            }

            var email = FirstNonBlank(client.ContactEmail, client.User?.Email);
            if (string.IsNullOrWhiteSpace(email))
                missing.Add("E-mail");

            var isPj = IsPessoaJuridica(client.CNPJ);
            if (isPj)
            {
                if (string.IsNullOrWhiteSpace(client.RazaoSocial))
                    missing.Add("Razão Social");
                if (!BrazilianTaxId.IsValidCnpj(client.CNPJ))
                    missing.Add("CNPJ");
            }
            else
            {
                if (ClientDisplayName.IsPlaceholderName(client.ContactName, email))
                    missing.Add("Nome completo");
                if (!BrazilianTaxId.IsValidCpf(client.CPF))
                    missing.Add("CPF");
            }

            var phone = BrazilianTaxId.Digits(client.ContactPhone);
            if (phone.Length < 10 || phone.Length > 11)
                missing.Add("Telefone");

            if (BrazilianTaxId.Digits(client.ZipCode).Length != 8)
                missing.Add("CEP");
            if (string.IsNullOrWhiteSpace(client.Address))
                missing.Add("Logradouro");
            if (string.IsNullOrWhiteSpace(client.AddressNumber))
                missing.Add("Número");
            if (string.IsNullOrWhiteSpace(client.Neighborhood))
                missing.Add("Bairro");
            if (string.IsNullOrWhiteSpace(client.City))
                missing.Add("Cidade");
            if (!IsValidUf(client.State))
                missing.Add("UF");

            return missing.Count == 0
                ? new ClientContractPartyStatus { IsComplete = true }
                : Incomplete(missing);
        }

        public static bool IsPessoaJuridica(string? cnpj) =>
            BrazilianTaxId.IsValidCnpj(cnpj);

        public static bool IsValidUf(string? state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return false;
            var uf = state.Trim().ToUpperInvariant();
            return BrazilianUfs.All.Contains(uf);
        }

        private static ClientContractPartyStatus Incomplete(List<string> missing) =>
            new()
            {
                IsComplete = false,
                MissingFields = missing,
                ErrorMessage = $"{IncompleteMessage} Campos em falta: {string.Join(", ", missing)}."
            };

        private static string? FirstNonBlank(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return null;
        }
    }
}
