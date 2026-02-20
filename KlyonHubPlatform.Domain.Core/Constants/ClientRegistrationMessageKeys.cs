namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Chaves de mensagem (TempDataMessageKey) retornadas pelo ClientMenuService para o fluxo de registro de cliente.
    /// O controller usa essas chaves para mapear para InfoMessages / redirecionamento.
    /// </summary>
    public static class ClientRegistrationMessageKeys
    {
        public const string CompleteRegistration = "CompleteRegistration";
        public const string CompleteRegistrationWithFields = "CompleteRegistrationWithFields";
    }
}
