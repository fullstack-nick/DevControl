using System.Security.Claims;
using DevControl.Api.Security;
using DevControl.Application.Security;
using DevControl.Infrastructure.Database;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/login", SignInAsync);
        app.MapGet("/auth/denied", () => Results.Problem("Access denied.", statusCode: StatusCodes.Status403Forbidden));

        app.MapGet("/api/auth/csrf", (HttpContext httpContext, IAntiforgery antiforgery, IWebHostEnvironment environment) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            httpContext.Response.Cookies.Append(
                "XSRF-TOKEN",
                tokens.RequestToken ?? string.Empty,
                new CookieOptions
                {
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = !environment.IsDevelopment() && !environment.IsEnvironment("Test")
                });

            return Results.Ok(new CsrfResponse(tokens.RequestToken ?? string.Empty));
        });

        var auth = app.MapGroup("/api/auth").RequireAuthorization();

        auth.MapGet("/me", async (
            CurrentUserAccessor currentUserAccessor,
            DevControlDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var currentUser = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
            var organizations = await dbContext.OrganizationMembers
                .Where(member => member.UserId == currentUser.Id && member.IsActive)
                .Join(
                    dbContext.Organizations,
                    member => member.OrganizationId,
                    organization => organization.Id,
                    (member, organization) => new { member, organization })
                .OrderBy(candidate => candidate.organization.Name)
                .Select(candidate => new OrganizationMembershipResponse(
                        candidate.organization.Id,
                        candidate.organization.Name,
                        candidate.organization.Slug,
                        candidate.member.Role.ToString()))
                .ToListAsync(cancellationToken);

            return Results.Ok(new MeResponse(
                new UserResponse(currentUser.Id, currentUser.Email, currentUser.DisplayName),
                organizations));
        });

        auth.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireCsrf();
    }

    private static async Task<IResult> SignInAsync(
        HttpContext httpContext,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        string? email,
        string? name,
        string? returnUrl)
    {
        var redirectUri = SafeReturnUrl(returnUrl);
        if (environment.IsDevelopment() || environment.IsEnvironment("Test"))
        {
            var devEmail = string.IsNullOrWhiteSpace(email) ? "developer@devcontrol.local" : email;
            var normalizedEmail = EmailAddressNormalizer.Normalize(devEmail);
            var displayName = string.IsNullOrWhiteSpace(name) ? devEmail : name.Trim();

            var claims = new List<Claim>
            {
                new(ClaimTypes.Email, EmailAddressNormalizer.Display(devEmail)),
                new(ClaimTypes.Name, displayName),
                new(ClaimTypes.NameIdentifier, normalizedEmail),
                new(DevControlClaimTypes.Provider, "development"),
                new(DevControlClaimTypes.Subject, normalizedEmail)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    IssuedUtc = DateTimeOffset.UtcNow
                });

            return Results.Redirect(redirectUri);
        }

        if (!DevControlSecurityExtensions.IsGoogleAuthConfigured(configuration))
        {
            return Results.Problem(
                "Google authentication is not configured. Set DEVCONTROL_AUTH_GOOGLE_CLIENT_ID and DEVCONTROL_AUTH_GOOGLE_CLIENT_SECRET.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Challenge(
            new AuthenticationProperties { RedirectUri = redirectUri },
            [DevControlSecurityExtensions.GoogleScheme]);
    }

    private static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            !returnUrl.StartsWith("/", StringComparison.Ordinal) ||
            returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }
}

public sealed record CsrfResponse(string Token);

public sealed record UserResponse(Guid Id, string Email, string DisplayName);

public sealed record OrganizationMembershipResponse(Guid Id, string Name, string Slug, string Role);

public sealed record MeResponse(UserResponse User, IReadOnlyList<OrganizationMembershipResponse> Organizations);
