using V086ReleaseCheck2.Api.Authorization;
using V086ReleaseCheck2.Api.Identity;
using V086ReleaseCheck2.Api.Integrations;
using V086ReleaseCheck2.Api.Features.Health;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace V086ReleaseCheck2.Api.Features.Authorization;

public static class AuthorizationEndpoints
{
    public static void MapAuthorizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/authorization/profile", () => Results.Ok(new { permission = Permissions.ProfileView }))
            .RequireAuthorization(Permissions.ProfileView)
            .WithName("AuthorizationProfile")
            .WithTags("Authorization");
        endpoints.MapGet("/api/authorization/users", (UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles) =>
            Results.Ok(new { users = users.Users.Select(user => new { id = user.Id, email = user.Email }).ToArray(), roles = roles.Roles.Select(role => role.Name).ToArray() }))
            .RequireAuthorization(Permissions.AuthorizationManage)
            .WithName("AuthorizationUsers").WithTags("Authorization");
        endpoints.MapPost("/api/authorization/users/{userId}/roles/{roleName}", async (string userId, string roleName, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles) =>
        {
            var user = await users.FindByIdAsync(userId);
            if (user is null || !await roles.RoleExistsAsync(roleName)) return Results.NotFound();
            var result = await users.AddToRoleAsync(user, roleName);
            return result.Succeeded ? Results.NoContent() : Results.ValidationProblem(result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
        }).RequireAuthorization(Permissions.AuthorizationManage).WithName("AuthorizationAssignRole").WithTags("Authorization");
    }
}