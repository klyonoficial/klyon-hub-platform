using FluentValidation;
using KlyonHubPlatform.Application.DTOs.ClientProfile;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Domain.Core.Helpers;

namespace KlyonHubPlatform.Application.Validators
{
    public class ClientProfileSaveInputValidator : AbstractValidator<ClientProfileSaveInput>
    {
        public ClientProfileSaveInputValidator()
        {
            RuleFor(x => x.UserId).GreaterThan(0);
            RuleFor(x => x.AccountEmail).NotEmpty().EmailAddress();

            RuleFor(x => x.ContactPhone)
                .Must(phone =>
                {
                    var digits = BrazilianTaxId.Digits(phone);
                    return digits.Length is 10 or 11;
                })
                .WithMessage("Informe um telefone válido com DDD.");

            RuleFor(x => x.ZipCode)
                .Must(zip => BrazilianTaxId.Digits(zip).Length == 8)
                .WithMessage("Informe um CEP válido (8 dígitos).");

            RuleFor(x => x.Address).NotEmpty().WithMessage("Informe o logradouro.");
            RuleFor(x => x.AddressNumber).NotEmpty().WithMessage("Informe o número do endereço.");
            RuleFor(x => x.Neighborhood).NotEmpty().WithMessage("Informe o bairro.");
            RuleFor(x => x.City).NotEmpty().WithMessage("Informe a cidade.");
            RuleFor(x => x.State)
                .Must(ClientContractPartyRules.IsValidUf)
                .WithMessage("Informe a UF.");

            RuleFor(x => x).Custom((input, context) =>
            {
                var isPj = ClientContractPartyRules.IsPessoaJuridica(input.CNPJ);
                var cnpjDigits = BrazilianTaxId.Digits(input.CNPJ);
                var cpfDigits = BrazilianTaxId.Digits(input.CPF);

                if (!isPj && cnpjDigits.Length > 0 && !BrazilianTaxId.IsBlank(cnpjDigits))
                    context.AddFailure(nameof(input.CNPJ), "CNPJ inválido. Verifique os dígitos informados.");

                if (isPj)
                {
                    if (string.IsNullOrWhiteSpace(input.RazaoSocial))
                        context.AddFailure(nameof(input.RazaoSocial), "A Razão Social é obrigatória para pessoa jurídica.");
                    if (cpfDigits.Length > 0 && !BrazilianTaxId.IsValidCpf(input.CPF))
                        context.AddFailure(nameof(input.CPF), "CPF inválido. Verifique os dígitos informados.");
                }
                else
                {
                    if (cpfDigits.Length == 0)
                        context.AddFailure(nameof(input.CPF), "Informe o CPF (pessoa física) ou o CNPJ (pessoa jurídica).");
                    else if (!BrazilianTaxId.IsValidCpf(input.CPF))
                        context.AddFailure(nameof(input.CPF), "CPF inválido. Verifique os dígitos informados.");

                    if (ClientDisplayName.IsPlaceholderName(input.ContactName, input.AccountEmail))
                        context.AddFailure(nameof(input.ContactName), "Informe o nome completo do contratante. Não use o login ou o prefixo do e-mail.");
                    else if (!HasFullName(input.ContactName))
                        context.AddFailure(nameof(input.ContactName), "Informe o nome completo (nome e sobrenome).");
                }

                if (!string.IsNullOrWhiteSpace(input.ContactName)
                    && ClientDisplayName.IsPlaceholderName(input.ContactName, input.AccountEmail))
                {
                    context.AddFailure(nameof(input.ContactName), "O nome completo não pode ser o login ou o prefixo do e-mail.");
                }
            });
        }

        private static bool HasFullName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && parts[0].Length >= 2 && parts[1].Length >= 2;
        }
    }
}
