using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Documents.Api;

/// <summary>
/// A development/test stand-in for real authentication. It turns three request headers into a
/// <see cref="ClaimsPrincipal"/> so you can exercise authorization without standing up an identity
/// provider. In production you replace this with ASP.NET Core authentication
/// (e.g. <c>AddAuthentication().AddJwtBearer(...)</c> + <c>UseAuthentication()</c>); Relay's
/// authorization consumes whatever principal that produces. Credential validation is never Relay's job.
/// </summary>
public static class DevAuthentication
{
    public const string UserHeader = "X-User-Id";        // a GUID — the authenticated user id
    public const string RolesHeader = "X-Roles";          // comma-separated roles
    public const string PermissionsHeader = "X-Permissions"; // comma-separated permissions

    public static void ApplyFromHeaders(HttpContext context)
    {
        var userId = context.Request.Headers[UserHeader].ToString();
        if (string.IsNullOrEmpty(userId))
        {
            return; // anonymous
        }

        var claims = new List<Claim> { new("sub", userId) };
        foreach (var role in Split(context.Request.Headers[RolesHeader].ToString()))
        {
            claims.Add(new Claim("role", role));
        }
        foreach (var permission in Split(context.Request.Headers[PermissionsHeader].ToString()))
        {
            claims.Add(new Claim("permission", permission));
        }

        // A non-null authenticationType makes the identity IsAuthenticated == true.
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "DevHeaders"));
    }

    private static IEnumerable<string> Split(string raw)
        => raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
