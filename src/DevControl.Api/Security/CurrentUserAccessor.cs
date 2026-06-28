using System.Security.Claims;
using DevControl.Application.Security;
using DevControl.Domain.Entities;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Security;

public sealed record CurrentUser(Guid Id, string Email, string NormalizedEmail, string DisplayName);

public sealed class CurrentUserAccessor(
    DevControlDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider)
{
    public async Task<CurrentUser> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        var email = GetClaim(principal, ClaimTypes.Email, "email");
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Authenticated principal does not include an email claim.");
        }

        var normalizedEmail = EmailAddressNormalizer.Normalize(email);
        var displayName = GetClaim(principal, ClaimTypes.Name, "name") ?? EmailAddressNormalizer.Display(email);
        var provider = GetClaim(principal, DevControlClaimTypes.Provider) ?? "google";
        var subject = GetClaim(principal, DevControlClaimTypes.Subject, ClaimTypes.NameIdentifier, "sub") ?? normalizedEmail;
        var now = timeProvider.GetUtcNow();

        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = new User(
                EmailAddressNormalizer.Display(email),
                normalizedEmail,
                displayName,
                provider,
                subject,
                now);
            dbContext.Users.Add(user);
        }
        else
        {
            user.SetIdentity(
                EmailAddressNormalizer.Display(email),
                normalizedEmail,
                displayName,
                provider,
                subject,
                now);
            user.MarkSeen(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CurrentUser(user.Id, user.Email, user.NormalizedEmail, user.DisplayName);
    }

    private static string? GetClaim(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
