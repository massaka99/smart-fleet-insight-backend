using System.Linq;
using SmartFleet.Models;

namespace SmartFleet.Authorization;

public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<UserRole, string[]> Permissions =
        new Dictionary<UserRole, string[]>
        {
            [UserRole.Driver] = new[]
            {
                "Chat",
                "Notifications",
                "Maps"
            },
            [UserRole.Coordinator] = new[]
            {
                "Chat",
                "Dashboard"
            },
            [UserRole.Admin] = new[]
            {
                "Chat",
                "Dashboard",
                "RoleManagement"
            }
        };

    public static bool HasPermission(UserRole role, string permission) =>
        Permissions.TryGetValue(role, out var permissionSet) && permissionSet.Contains(permission, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> GetPermissions(UserRole role) =>
        Permissions.TryGetValue(role, out var permissionSet)
            ? permissionSet
            : Array.Empty<string>();
}
