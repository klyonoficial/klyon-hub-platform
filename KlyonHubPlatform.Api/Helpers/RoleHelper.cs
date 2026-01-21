namespace KlyonHubPlatform.Api.Helpers
{
    public static class RoleHelper
    {
        public static string GetRoleNameInPortuguese(string roleName)
        {
            return roleName switch
            {
                "Administrator" => "Administrador",
                "CoworkingPartner" => "Parceiro Coworking",
                "Client" => "Cliente",
                _ => roleName
            };
        }
    }
}
