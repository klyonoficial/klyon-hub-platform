using KlyonHubPlatform.Domain.Core.Constants;
namespace KlyonHubPlatform.Api.Helpers
{
    public static class RoleHelper
    {
        public static string GetRoleNameInPortuguese(string roleName) => RoleNames.GetDisplayName(roleName);
    }
}