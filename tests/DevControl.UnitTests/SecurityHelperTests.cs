using DevControl.Application.Security;
using DevControl.Domain.Enums;
using Xunit;

namespace DevControl.UnitTests;

public sealed class SecurityHelperTests
{
    [Theory]
    [InlineData("  Nick@example.com ", "NICK@EXAMPLE.COM")]
    [InlineData("devcontrol.user+test@Example.co", "DEVCONTROL.USER+TEST@EXAMPLE.CO")]
    public void EmailNormalizer_ProducesStableTenantKey(string input, string expected)
    {
        Assert.Equal(expected, EmailAddressNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("Production API", "production-api")]
    [InlineData("dev_control.api", "dev-control-api")]
    [InlineData("-----Live-----", "live")]
    public void SlugNormalizer_ProducesUrlSafeSlug(string input, string expected)
    {
        Assert.Equal(expected, SlugNormalizer.Normalize(input));
    }

    [Fact]
    public void InvitationTokenHash_IsDeterministic_AndDoesNotExposeRawToken()
    {
        var service = new InvitationTokenService();
        var token = service.CreateToken();

        var firstHash = service.HashToken(token);
        var secondHash = service.HashToken(token);

        Assert.NotEqual(token, firstHash);
        Assert.Equal(64, firstHash.Length);
        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void RolePermissions_EnforceExpectedHierarchy()
    {
        Assert.True(RolePermissions.CanManageProjects(OrganizationRole.Developer));
        Assert.False(RolePermissions.CanManageMembers(OrganizationRole.Developer));
        Assert.True(RolePermissions.CanManageMembers(OrganizationRole.Admin));
        Assert.False(RolePermissions.CanManageOwnerRole(OrganizationRole.Admin));
        Assert.True(RolePermissions.CanManageOwnerRole(OrganizationRole.Owner));
    }
}
