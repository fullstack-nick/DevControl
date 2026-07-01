using System.Text.Json;
using DevControl.Api.Security;
using DevControl.Application.Apps;
using DevControl.Application.Security;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Endpoints;

public static class OperatorEndpoints
{
    private const string RegisterScope = "apps:register";
    private const int DefaultApiKeyRateLimitPerMinute = 10;
    private const string BootstrapTokenName = "Operator bootstrap registration token";
    private const string BootstrapApiKeyName = "Operator bootstrap API key";
    private const string BootstrapFeatureFlagKey = "checkout.enabled";
    private const string BootstrapKillSwitchKey = "checkout.kill";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapOperatorEndpoints(this WebApplication app)
    {
        app.MapPost("/api/operator/bootstrap-live-proof", BootstrapLiveProofAsync);
    }

    private static async Task<IResult> BootstrapLiveProofAsync(
        OperatorBootstrapRequest request,
        HttpContext httpContext,
        IConfiguration configuration,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        RegistrationTokenService registrationTokenService,
        ApiKeySecretService apiKeySecretService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var configuredSecret = configuration["OPERATOR_BOOTSTRAP_SECRET"];
        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            return Results.NotFound();
        }

        var providedSecret = httpContext.Request.Headers[OperatorSecretValidator.HeaderName].ToString();
        if (!OperatorSecretValidator.IsValid(configuredSecret, providedSecret))
        {
            return Results.Unauthorized();
        }

        var normalized = Normalize(request);
        if (normalized.Failure is not null)
        {
            return normalized.Failure;
        }

        var input = normalized.Input!;
        var now = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == input.NormalizedOwnerEmail, cancellationToken);
        if (user is null)
        {
            user = new User(
                EmailAddressNormalizer.Display(input.OwnerEmail),
                input.NormalizedOwnerEmail,
                input.OwnerName,
                "operator-bootstrap",
                input.NormalizedOwnerEmail,
                now);
            dbContext.Users.Add(user);
        }
        else
        {
            user.MarkSeen(now);
        }

        var actor = new CurrentUser(user.Id, user.Email, user.NormalizedEmail, user.DisplayName);
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(candidate => candidate.Slug == input.OrganizationSlug, cancellationToken);
        if (organization is null)
        {
            organization = new Organization(input.OrganizationName, input.OrganizationSlug, user.Id, now);
            dbContext.Organizations.Add(organization);
            auditLogWriter.Add(
                organization.Id,
                actor,
                "organization.create",
                "Succeeded",
                "organization",
                organization.Id.ToString(),
                "Organization created by operator bootstrap.",
                new { bootstrap = true, organization.Name, organization.Slug });
        }

        var member = await dbContext.OrganizationMembers
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organization.Id && candidate.UserId == user.Id, cancellationToken);
        if (member is null)
        {
            member = new OrganizationMember(organization.Id, user.Id, OrganizationRole.Owner, now);
            dbContext.OrganizationMembers.Add(member);
        }
        else if (!member.IsActive)
        {
            member.Reactivate(OrganizationRole.Owner, now);
        }
        else if (member.Role != OrganizationRole.Owner)
        {
            member.ChangeRole(OrganizationRole.Owner, now);
        }

        var project = await dbContext.Projects
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organization.Id && candidate.Slug == input.ProjectSlug, cancellationToken);
        if (project is null)
        {
            project = new Project(organization.Id, input.ProjectName, input.ProjectSlug, input.ProjectDescription, user.Id, now);
            dbContext.Projects.Add(project);
            auditLogWriter.Add(
                organization.Id,
                actor,
                "project.create",
                "Succeeded",
                "project",
                project.Id.ToString(),
                "Project created by operator bootstrap.",
                new { bootstrap = true, project.Name, project.Slug });
        }

        var environment = await dbContext.ProjectEnvironments
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organization.Id &&
                    candidate.ProjectId == project.Id &&
                    candidate.Slug == input.EnvironmentSlug,
                cancellationToken);
        if (environment is null)
        {
            environment = new ProjectEnvironment(organization.Id, project.Id, input.EnvironmentName, input.EnvironmentSlug, user.Id, now);
            dbContext.ProjectEnvironments.Add(environment);
            auditLogWriter.Add(
                organization.Id,
                actor,
                "environment.create",
                "Succeeded",
                "environment",
                environment.Id.ToString(),
                "Environment created by operator bootstrap.",
                new { bootstrap = true, environment.Name, environment.Slug },
                project.Id,
                environment.Id);
        }

        var revokedRegistrationTokenIds = await RevokePriorRegistrationTokensAsync(dbContext, auditLogWriter, actor, organization.Id, project.Id, environment.Id, user.Id, now, cancellationToken);
        var revokedApiKeyIds = await RevokePriorApiKeysAsync(dbContext, auditLogWriter, actor, organization.Id, project.Id, environment.Id, user.Id, now, cancellationToken);

        var registrationSecret = registrationTokenService.CreateToken();
        var registrationToken = new RegistrationToken(
            organization.Id,
            project.Id,
            environment.Id,
            BootstrapTokenName,
            registrationSecret.Prefix,
            registrationSecret.Hash,
            RegisterScope,
            user.Id,
            now);
        dbContext.RegistrationTokens.Add(registrationToken);
        auditLogWriter.Add(
            organization.Id,
            actor,
            "registration_token.create",
            "Succeeded",
            "registration_token",
            registrationToken.Id.ToString(),
            "Registration token created by operator bootstrap.",
            new { bootstrap = true, registrationToken.Name, registrationToken.TokenPrefix, registrationToken.Scope },
            project.Id,
            environment.Id);

        _ = ApiKeyScopes.TryNormalize([ApiKeyScopes.SampleRead, ApiKeyScopes.FlagsRead], out var scopes, out var scopesJson, out _);
        var apiKeySecret = apiKeySecretService.CreateKey();
        var apiKey = new ApiKey(
            organization.Id,
            project.Id,
            environment.Id,
            BootstrapApiKeyName,
            apiKeySecret.Prefix,
            apiKeySecret.Hash,
            scopesJson,
            DefaultApiKeyRateLimitPerMinute,
            user.Id,
            now);
        dbContext.ApiKeys.Add(apiKey);
        auditLogWriter.Add(
            organization.Id,
            actor,
            "api_key.create",
            "Succeeded",
            "api_key",
            apiKey.Id.ToString(),
            "API key created by operator bootstrap.",
            new { bootstrap = true, apiKey.Name, apiKey.KeyPrefix, scopes, apiKey.RateLimitPerMinute },
            project.Id,
            environment.Id);

        await UpsertStage5ProofFlagsAsync(dbContext, auditLogWriter, actor, organization.Id, project.Id, environment.Id, user.Id, now, cancellationToken);

        var controlAction = new ControlAction(
            organization.Id,
            project.Id,
            environment.Id,
            "operator.bootstrap.live_proof",
            user.Id,
            "operator_bootstrap",
            organization.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                input.OwnerEmail,
                input.OrganizationSlug,
                input.ProjectSlug,
                input.EnvironmentSlug,
                bootstrap = true
            }, JsonOptions),
            now);
        controlAction.MarkStarted(now);
        controlAction.MarkCompleted(
            ControlActionStatus.Succeeded,
            JsonSerializer.Serialize(new
            {
                organizationId = organization.Id,
                projectId = project.Id,
                environmentId = environment.Id,
                registrationTokenId = registrationToken.Id,
                registrationTokenPrefix = registrationToken.TokenPrefix,
                apiKeyId = apiKey.Id,
                apiKeyPrefix = apiKey.KeyPrefix,
                revokedRegistrationTokenIds,
                revokedApiKeyIds
            }, JsonOptions),
            null,
            now);
        dbContext.ControlActions.Add(controlAction);

        auditLogWriter.Add(
            organization.Id,
            actor,
            "operator.bootstrap.live_proof",
            "Succeeded",
            "operator_bootstrap",
            organization.Id.ToString(),
            "Live proof tenant and show-once secrets bootstrapped.",
            new
            {
                bootstrap = true,
                registrationTokenPrefix = registrationToken.TokenPrefix,
                apiKeyPrefix = apiKey.KeyPrefix,
                revokedRegistrationTokenIds,
                revokedApiKeyIds
            },
            project.Id,
            environment.Id);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new OperatorBootstrapResponse(
            new OperatorBootstrapUserResponse(user.Id, user.Email, user.DisplayName),
            new OperatorBootstrapOrganizationResponse(organization.Id, organization.Name, organization.Slug),
            new OperatorBootstrapProjectResponse(project.Id, project.Name, project.Slug),
            new OperatorBootstrapEnvironmentResponse(environment.Id, environment.Name, environment.Slug),
            new OperatorBootstrapRegistrationTokenResponse(registrationToken.Id, registrationToken.Name, registrationToken.TokenPrefix, registrationToken.Scope, registrationSecret.Secret),
            new OperatorBootstrapApiKeyResponse(apiKey.Id, apiKey.Name, apiKey.KeyPrefix, ApiKeyScopes.FromJson(apiKey.ScopesJson), apiKey.RateLimitPerMinute, apiKeySecret.Secret),
            revokedRegistrationTokenIds,
            revokedApiKeyIds));
    }

    private static async Task<IReadOnlyList<Guid>> RevokePriorRegistrationTokensAsync(
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        CurrentUser actor,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RegistrationTokens
            .Where(token =>
                token.OrganizationId == organizationId &&
                token.ProjectId == projectId &&
                token.EnvironmentId == environmentId &&
                token.Name == BootstrapTokenName &&
                token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(userId, now);
            auditLogWriter.Add(
                organizationId,
                actor,
                "registration_token.revoke",
                "Succeeded",
                "registration_token",
                token.Id.ToString(),
                "Prior operator bootstrap registration token revoked.",
                new { bootstrap = true, token.Name, token.TokenPrefix },
                projectId,
                environmentId);
        }

        return tokens.Select(token => token.Id).ToArray();
    }

    private static async Task UpsertStage5ProofFlagsAsync(
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        CurrentUser actor,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await UpsertStage5ProofFlagAsync(
            dbContext,
            auditLogWriter,
            actor,
            organizationId,
            projectId,
            environmentId,
            userId,
            BootstrapFeatureFlagKey,
            "Checkout enabled",
            "Stage 5 proof feature flag consumed by the sample app SDK.",
            FeatureFlagKind.FeatureFlag,
            isEnabled: true,
            "Stage 5 live proof enables the checkout feature flag.",
            now,
            cancellationToken);

        await UpsertStage5ProofFlagAsync(
            dbContext,
            auditLogWriter,
            actor,
            organizationId,
            projectId,
            environmentId,
            userId,
            BootstrapKillSwitchKey,
            "Checkout kill switch",
            "Stage 5 proof kill switch consumed by the sample app SDK.",
            FeatureFlagKind.KillSwitch,
            isEnabled: false,
            "Stage 5 live proof keeps the checkout kill switch inactive.",
            now,
            cancellationToken);
    }

    private static async Task UpsertStage5ProofFlagAsync(
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        CurrentUser actor,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        Guid userId,
        string key,
        string name,
        string description,
        FeatureFlagKind kind,
        bool isEnabled,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var flag = await dbContext.FeatureFlags
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.ProjectId == projectId &&
                    candidate.EnvironmentId == environmentId &&
                    candidate.Key == key,
                cancellationToken);

        var action = flag is null ? "feature_flag.create" : "feature_flag.update";
        var oldValue = flag?.IsEnabled ?? false;
        if (flag is null)
        {
            flag = new FeatureFlag(
                organizationId,
                projectId,
                environmentId,
                key,
                name,
                description,
                kind,
                isEnabled,
                userId,
                now);
            dbContext.FeatureFlags.Add(flag);
        }
        else
        {
            flag.Update(name, description, isEnabled, userId, now);
        }

        dbContext.FeatureFlagChanges.Add(new FeatureFlagChange(
            flag.Id,
            organizationId,
            projectId,
            environmentId,
            oldValue,
            isEnabled,
            reason,
            userId,
            now));

        var controlAction = new ControlAction(
            organizationId,
            projectId,
            environmentId,
            action,
            userId,
            "feature_flag",
            flag.Id.ToString(),
            JsonSerializer.Serialize(new { flag.Id, flag.Key, flag.Kind, bootstrap = true }, JsonOptions),
            now);
        controlAction.MarkStarted(now);
        controlAction.MarkCompleted(
            ControlActionStatus.Succeeded,
            JsonSerializer.Serialize(new { flag.Id, flag.Key, flag.Kind, oldValue, newValue = isEnabled, reason, bootstrap = true }, JsonOptions),
            null,
            now);
        dbContext.ControlActions.Add(controlAction);

        auditLogWriter.Add(
            organizationId,
            actor,
            action,
            "Succeeded",
            "feature_flag",
            flag.Id.ToString(),
            "Stage 5 proof flag changed by operator bootstrap.",
            new { bootstrap = true, flag.Key, flag.Name, flag.Kind, oldValue, newValue = isEnabled, reason },
            projectId,
            environmentId);
    }

    private static async Task<IReadOnlyList<Guid>> RevokePriorApiKeysAsync(
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        CurrentUser actor,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var apiKeys = await dbContext.ApiKeys
            .Where(apiKey =>
                apiKey.OrganizationId == organizationId &&
                apiKey.ProjectId == projectId &&
                apiKey.EnvironmentId == environmentId &&
                apiKey.Name == BootstrapApiKeyName &&
                apiKey.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var apiKey in apiKeys)
        {
            apiKey.Revoke(userId, now);
            auditLogWriter.Add(
                organizationId,
                actor,
                "api_key.revoke",
                "Succeeded",
                "api_key",
                apiKey.Id.ToString(),
                "Prior operator bootstrap API key revoked.",
                new { bootstrap = true, apiKey.Name, apiKey.KeyPrefix },
                projectId,
                environmentId);
        }

        return apiKeys.Select(apiKey => apiKey.Id).ToArray();
    }

    private static NormalizedBootstrapResult Normalize(OperatorBootstrapRequest request)
    {
        string ownerEmail;
        string normalizedOwnerEmail;
        try
        {
            ownerEmail = EmailAddressNormalizer.Display(request.OwnerEmail ?? string.Empty);
            normalizedOwnerEmail = EmailAddressNormalizer.Normalize(request.OwnerEmail ?? string.Empty);
        }
        catch (ArgumentException exception)
        {
            return new NormalizedBootstrapResult(null, Results.BadRequest(new ProblemDetailsResponse(exception.Message)));
        }

        try
        {
            var organization = NormalizeNameAndSlug(request.OrganizationName, request.OrganizationSlug, "Acme Platform", "acme-platform");
            var project = NormalizeNameAndSlug(request.ProjectName, request.ProjectSlug, "Sample App", "sample-app");
            var environment = NormalizeNameAndSlug(request.EnvironmentName, request.EnvironmentSlug, "Production", "production");
            return new NormalizedBootstrapResult(
                new NormalizedBootstrapInput(
                    ownerEmail,
                    normalizedOwnerEmail,
                    string.IsNullOrWhiteSpace(request.OwnerName) ? ownerEmail : request.OwnerName.Trim(),
                    organization.Name,
                    organization.Slug,
                    project.Name,
                    project.Slug,
                    string.IsNullOrWhiteSpace(request.ProjectDescription) ? "Live proof sample app" : request.ProjectDescription.Trim(),
                    environment.Name,
                    environment.Slug),
                null);
        }
        catch (ArgumentException exception)
        {
            return new NormalizedBootstrapResult(null, Results.BadRequest(new ProblemDetailsResponse(exception.Message)));
        }
    }

    private static NameSlug NormalizeNameAndSlug(string? requestedName, string? requestedSlug, string defaultName, string defaultSlug)
    {
        var name = string.IsNullOrWhiteSpace(requestedName) ? defaultName : requestedName.Trim();
        if (name.Length > 160)
        {
            throw new ArgumentException("Name cannot exceed 160 characters.");
        }

        var slugSource = string.IsNullOrWhiteSpace(requestedSlug) ? defaultSlug : requestedSlug;
        var slug = SlugNormalizer.Normalize(slugSource);
        return new NameSlug(name, slug);
    }

    private sealed record NameSlug(string Name, string Slug);

    private sealed record NormalizedBootstrapResult(NormalizedBootstrapInput? Input, IResult? Failure);

    private sealed record NormalizedBootstrapInput(
        string OwnerEmail,
        string NormalizedOwnerEmail,
        string OwnerName,
        string OrganizationName,
        string OrganizationSlug,
        string ProjectName,
        string ProjectSlug,
        string ProjectDescription,
        string EnvironmentName,
        string EnvironmentSlug);
}

public sealed record OperatorBootstrapRequest(
    string? OwnerEmail,
    string? OwnerName,
    string? OrganizationName,
    string? OrganizationSlug,
    string? ProjectName,
    string? ProjectSlug,
    string? ProjectDescription,
    string? EnvironmentName,
    string? EnvironmentSlug);

public sealed record OperatorBootstrapResponse(
    OperatorBootstrapUserResponse Owner,
    OperatorBootstrapOrganizationResponse Organization,
    OperatorBootstrapProjectResponse Project,
    OperatorBootstrapEnvironmentResponse Environment,
    OperatorBootstrapRegistrationTokenResponse RegistrationToken,
    OperatorBootstrapApiKeyResponse ApiKey,
    IReadOnlyList<Guid> RevokedRegistrationTokenIds,
    IReadOnlyList<Guid> RevokedApiKeyIds);

public sealed record OperatorBootstrapUserResponse(Guid Id, string Email, string DisplayName);

public sealed record OperatorBootstrapOrganizationResponse(Guid Id, string Name, string Slug);

public sealed record OperatorBootstrapProjectResponse(Guid Id, string Name, string Slug);

public sealed record OperatorBootstrapEnvironmentResponse(Guid Id, string Name, string Slug);

public sealed record OperatorBootstrapRegistrationTokenResponse(Guid Id, string Name, string TokenPrefix, string Scope, string Secret);

public sealed record OperatorBootstrapApiKeyResponse(Guid Id, string Name, string KeyPrefix, IReadOnlyList<string> Scopes, int RateLimitPerMinute, string Secret);
