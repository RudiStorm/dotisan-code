namespace CiPnpmCheck4.Api.Authorization;

public static class Permissions
{
    public const string ClaimType = "permission";
    public const string ProfileView = "profile.view";
    public const string AuthorizationManage = "authorization.manage";
    // DOTISAN:RESOURCE_PERMISSIONS
    public static IReadOnlyList<string> All { get; } = [ProfileView, AuthorizationManage];
}