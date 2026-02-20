namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class RoleNames
    {
        public const string Administrator = "Administrator";
        public const string CoworkingPartner = "CoworkingPartner";
        public const string Client = "Client";
        public static string GetDisplayName(string roleName) => roleName switch
        {
            Administrator => DisplayNames.Administrator,
            CoworkingPartner => DisplayNames.CoworkingPartner,
            Client => DisplayNames.Client,
            _ => roleName
        };
        public static class DisplayNames
        {
            public const string Administrator = "Administrador";
            public const string CoworkingPartner = "Parceiro Coworking";
            public const string Client = "Cliente";
        }
    }
}