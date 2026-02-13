using System.Text.RegularExpressions;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Infra.CrossCutting.Helpers
{
    public static class DocumentValidator
    {
        /// <summary>
        /// Valida CPF
        /// </summary>
        /// <param name="cpf">CPF com ou sem formatação</param>
        /// <returns>true se válido, false caso contrário</returns>
        public static bool IsValidCPF(string? cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf))
                return false;

            // Remove formatação
            cpf = Regex.Replace(cpf, @"[^\d]", "");

            // Verifica se tem 11 dígitos
            if (cpf.Length != 11)
                return false;

            // Verifica se todos os dígitos são iguais (ex: 111.111.111-11)
            if (cpf.All(c => c == cpf[0]))
                return false;

            // Validação dos dígitos verificadores
            int[] multiplicador1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] multiplicador2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

            string tempCpf = cpf.Substring(0, 9);
            int sum = 0;

            for (int i = 0; i < 9; i++)
                sum += int.Parse(tempCpf[i].ToString()) * multiplicador1[i];

            int remainder = sum % 11;
            if (remainder < 2)
                remainder = 0;
            else
                remainder = 11 - remainder;

            string digit = remainder.ToString();
            tempCpf = tempCpf + digit;
            sum = 0;

            for (int i = 0; i < 10; i++)
                sum += int.Parse(tempCpf[i].ToString()) * multiplicador2[i];

            remainder = sum % 11;
            if (remainder < 2)
                remainder = 0;
            else
                remainder = 11 - remainder;

            digit = digit + remainder.ToString();

            return cpf.EndsWith(digit);
        }

        /// <summary>
        /// Valida CNPJ
        /// </summary>
        /// <param name="cnpj">CNPJ com ou sem formatação</param>
        /// <returns>true se válido, false caso contrário</returns>
        public static bool IsValidCNPJ(string? cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj))
                return false;

            // Remove formatação
            cnpj = Regex.Replace(cnpj, @"[^\d]", "");

            // Verifica se tem 14 dígitos
            if (cnpj.Length != 14)
                return false;

            // Verifica se todos os dígitos são iguais (ex: 00.000.000/0000-00)
            if (cnpj.All(c => c == cnpj[0]))
                return false;

            // Validação dos dígitos verificadores
            int[] multiplicador1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] multiplicador2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

            string tempCnpj = cnpj.Substring(0, 12);
            int sum = 0;

            for (int i = 0; i < 12; i++)
                sum += int.Parse(tempCnpj[i].ToString()) * multiplicador1[i];

            int remainder = (sum % 11);
            if (remainder < 2)
                remainder = 0;
            else
                remainder = 11 - remainder;

            string digit = remainder.ToString();
            tempCnpj = tempCnpj + digit;
            sum = 0;

            for (int i = 0; i < 13; i++)
                sum += int.Parse(tempCnpj[i].ToString()) * multiplicador2[i];

            remainder = (sum % 11);
            if (remainder < 2)
                remainder = 0;
            else
                remainder = 11 - remainder;

            digit = digit + remainder.ToString();

            return cnpj.EndsWith(digit);
        }

        /// <summary>
        /// Valida CPF ou CNPJ (verifica qual é e valida)
        /// </summary>
        /// <param name="value">CPF ou CNPJ com ou sem formatação</param>
        /// <returns>Tupla com (isValid, type, message)</returns>
        public static (bool isValid, TipoDocumentoCliente? type, string message) ValidateCPForCNPJ(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (false, null, "CPF ou CNPJ não informado.");
            }

            // Remove formatação
            string clean = Regex.Replace(value, @"[^\d]", "");

            if (clean.Length == 11)
            {
                bool isValid = IsValidCPF(value);
                return (isValid, TipoDocumentoCliente.CPF, isValid ? "CPF válido." : "CPF inválido. Verifique os dígitos informados.");
            }
            else if (clean.Length == 14)
            {
                bool isValid = IsValidCNPJ(value);
                return (isValid, TipoDocumentoCliente.CNPJ, isValid ? "CNPJ válido." : "CNPJ inválido. Verifique os dígitos informados.");
            }
            else
            {
                return (false, null, "CPF deve ter 11 dígitos ou CNPJ deve ter 14 dígitos.");
            }
        }
    }
}
