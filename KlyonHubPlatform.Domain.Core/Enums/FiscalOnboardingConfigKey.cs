using System.Collections.Generic;

namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>Chaves de configuração para onboarding fiscal (CNPJ primeiro).</summary>
    public enum FiscalOnboardingConfigKey
    {
        /// <summary>Valor fixo da cobrança de adesão (PIX). Se vazio, usa o preço base mensal do endereço.</summary>
        AdhesionAmount
    }

    public static class FiscalOnboardingConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<FiscalOnboardingConfigKey, string> Keys = new Dictionary<FiscalOnboardingConfigKey, string>
        {
            { FiscalOnboardingConfigKey.AdhesionAmount, "FiscalOnboarding.AdhesionAmount" }
        };

        public static string ToConfigKey(this FiscalOnboardingConfigKey key) => Keys[key];
    }
}
