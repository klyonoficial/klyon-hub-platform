namespace KlyonHubPlatform.Domain.Core.Enums
{
    public static class D4SignApiFlags
    {
        public const string No = "0";
        public const string Yes = "1";
    }
    public enum D4SignSignerAct
    {
        None = 0,
        Sign = 1
    }
    public enum D4SignSignerForeign
    {
        National = 0,
        Foreign = 1
    }
    public enum D4SignCertificadoIcpBr
    {
        No = 0,
        Yes = 1
    }
    public enum D4SignAssinaturaPresencial
    {
        No = 0,
        Yes = 1
    }
    public enum D4SignDocAuth
    {
        No = 0,
        Yes = 1
    }
    public enum D4SignDocAuthAndSelfie
    {
        No = 0,
        Yes = 1
    }
    public enum D4SignUploadAllow
    {
        No = 0,
        Yes = 1
    }
    public enum D4SignSkipEmail
    {
        SendEmail = 0,
        SkipEmail = 1
    }
    public enum D4SignWorkflow
    {
        Sequential = 0,
        Parallel = 1
    }
}