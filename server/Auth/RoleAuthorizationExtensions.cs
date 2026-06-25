using Microsoft.AspNetCore.Authorization;

namespace Poseidon.Server.Auth;

public static class RoleAuthorizationExtensions
{
    public static RouteHandlerBuilder RequireRole(this RouteHandlerBuilder builder, params string[] roles)
    {
        return builder.RequireAuthorization(CreateRolePolicy(roles));
    }

    public static RouteGroupBuilder RequireRole(this RouteGroupBuilder builder, params string[] roles)
    {
        return builder.RequireAuthorization(CreateRolePolicy(roles));
    }

    private static Action<AuthorizationPolicyBuilder> CreateRolePolicy(string[] roles)
    {
        if (roles.Length == 0)
        {
            throw new ArgumentException("At least one role is required.", nameof(roles));
        }

        return policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(roles);
    }
}
