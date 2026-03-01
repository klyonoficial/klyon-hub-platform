namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Tipos de documento opcionais no cadastro de cliente (quando possui CNPJ).
    /// </summary>
    public enum ClientRegistrationDocumentType
    {
        Cnpj = 0,
        ContratoSocial = 1,
        RgCnhRepresentantes = 2
    }
}
