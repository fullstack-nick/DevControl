using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Infrastructure.Database;

public sealed class DevControlDbContext(DbContextOptions<DevControlDbContext> options) :
    DbContext(options),
    IDataProtectionKeyContext
{
    public DbSet<SchemaVersion> SchemaVersions => Set<SchemaVersion>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectEnvironment> ProjectEnvironments => Set<ProjectEnvironment>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<ControlAction> ControlActions => Set<ControlAction>();

    public DbSet<LiveApp> LiveApps => Set<LiveApp>();

    public DbSet<LiveAppDeployment> LiveAppDeployments => Set<LiveAppDeployment>();

    public DbSet<RegistrationToken> RegistrationTokens => Set<RegistrationToken>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<ApiKeyUsageDaily> ApiKeyUsageDaily => Set<ApiKeyUsageDaily>();

    public DbSet<ApiKeyRateLimitWindow> ApiKeyRateLimitWindows => Set<ApiKeyRateLimitWindow>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureSchemaVersion(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureOrganization(modelBuilder);
        ConfigureOrganizationMember(modelBuilder);
        ConfigureOrganizationInvitation(modelBuilder);
        ConfigureProject(modelBuilder);
        ConfigureProjectEnvironment(modelBuilder);
        ConfigureAuditLog(modelBuilder);
        ConfigureControlAction(modelBuilder);
        ConfigureLiveApp(modelBuilder);
        ConfigureLiveAppDeployment(modelBuilder);
        ConfigureRegistrationToken(modelBuilder);
        ConfigureApiKey(modelBuilder);
        ConfigureApiKeyUsageDaily(modelBuilder);
        ConfigureApiKeyRateLimitWindow(modelBuilder);
        ConfigureDataProtectionKey(modelBuilder);
    }

    private static void ConfigureSchemaVersion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchemaVersion>(entity =>
        {
            entity.ToTable("schema_versions");
            entity.HasKey(schemaVersion => schemaVersion.Id);

            entity.Property(schemaVersion => schemaVersion.Id)
                .HasColumnName("id");

            entity.Property(schemaVersion => schemaVersion.Name)
                .HasColumnName("name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(schemaVersion => schemaVersion.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
        });
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);

            entity.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(user => user.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
            entity.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            entity.Property(user => user.ExternalProvider).HasColumnName("external_provider").HasMaxLength(64).IsRequired();
            entity.Property(user => user.ExternalSubject).HasColumnName("external_subject").HasMaxLength(200).IsRequired();
            entity.Property(user => user.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(user => user.LastSeenAt).HasColumnName("last_seen_at").IsRequired();

            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
            entity.HasIndex(user => new { user.ExternalProvider, user.ExternalSubject });
        });
    }

    private static void ConfigureOrganization(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(organization => organization.Id);

            entity.Property(organization => organization.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(organization => organization.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(organization => organization.Slug).HasColumnName("slug").HasMaxLength(80).IsRequired();
            entity.Property(organization => organization.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(organization => organization.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(organization => organization.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(organization => organization.Slug).IsUnique();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(organization => organization.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOrganizationMember(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationMember>(entity =>
        {
            entity.ToTable("organization_members");
            entity.HasKey(member => member.Id);

            entity.Property(member => member.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(member => member.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(member => member.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(member => member.Role)
                .HasColumnName("role")
                .HasConversion<string>()
                .HasMaxLength(24)
                .IsRequired();
            entity.Property(member => member.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(member => member.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(member => member.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(member => member.RemovedAt).HasColumnName("removed_at");

            entity.HasIndex(member => member.OrganizationId);
            entity.HasIndex(member => member.UserId);
            entity.HasIndex(member => new { member.OrganizationId, member.UserId })
                .IsUnique()
                .HasFilter("is_active");
            entity.HasIndex(member => new { member.OrganizationId, member.Role, member.IsActive });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(member => member.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOrganizationInvitation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationInvitation>(entity =>
        {
            entity.ToTable("organization_invitations");
            entity.HasKey(invitation => invitation.Id);

            entity.Property(invitation => invitation.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(invitation => invitation.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(invitation => invitation.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(invitation => invitation.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
            entity.Property(invitation => invitation.Role)
                .HasColumnName("role")
                .HasConversion<string>()
                .HasMaxLength(24)
                .IsRequired();
            entity.Property(invitation => invitation.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
            entity.Property(invitation => invitation.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(24)
                .IsRequired();
            entity.Property(invitation => invitation.InvitedByUserId).HasColumnName("invited_by_user_id").IsRequired();
            entity.Property(invitation => invitation.AcceptedByUserId).HasColumnName("accepted_by_user_id");
            entity.Property(invitation => invitation.ExpiresAt).HasColumnName("expires_at").IsRequired();
            entity.Property(invitation => invitation.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(invitation => invitation.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(invitation => invitation.LastSentAt).HasColumnName("last_sent_at").IsRequired();
            entity.Property(invitation => invitation.AcceptedAt).HasColumnName("accepted_at");
            entity.Property(invitation => invitation.RevokedAt).HasColumnName("revoked_at");

            entity.HasIndex(invitation => invitation.OrganizationId);
            entity.HasIndex(invitation => invitation.TokenHash).IsUnique();
            entity.HasIndex(invitation => new { invitation.OrganizationId, invitation.NormalizedEmail, invitation.Status });
            entity.HasIndex(invitation => new { invitation.OrganizationId, invitation.NormalizedEmail })
                .IsUnique()
                .HasFilter("status = 'Pending'");

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(invitation => invitation.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(invitation => invitation.InvitedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(invitation => invitation.AcceptedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProject(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(project => project.Id);

            entity.Property(project => project.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(project => project.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(project => project.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(project => project.Slug).HasColumnName("slug").HasMaxLength(80).IsRequired();
            entity.Property(project => project.Description).HasColumnName("description").HasMaxLength(1000).IsRequired();
            entity.Property(project => project.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(project => project.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(project => project.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(project => project.OrganizationId);
            entity.HasIndex(project => new { project.OrganizationId, project.Slug }).IsUnique();

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(project => project.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(project => project.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProjectEnvironment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectEnvironment>(entity =>
        {
            entity.ToTable("environments");
            entity.HasKey(environment => environment.Id);

            entity.Property(environment => environment.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(environment => environment.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(environment => environment.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(environment => environment.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(environment => environment.Slug).HasColumnName("slug").HasMaxLength(80).IsRequired();
            entity.Property(environment => environment.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(environment => environment.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(environment => environment.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(environment => environment.OrganizationId);
            entity.HasIndex(environment => environment.ProjectId);
            entity.HasIndex(environment => new { environment.ProjectId, environment.Slug }).IsUnique();

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(environment => environment.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(environment => environment.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(environment => environment.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(auditLog => auditLog.Id);

            entity.Property(auditLog => auditLog.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(auditLog => auditLog.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(auditLog => auditLog.ProjectId).HasColumnName("project_id");
            entity.Property(auditLog => auditLog.EnvironmentId).HasColumnName("environment_id");
            entity.Property(auditLog => auditLog.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(auditLog => auditLog.ActorEmail).HasColumnName("actor_email").HasMaxLength(320).IsRequired();
            entity.Property(auditLog => auditLog.Action).HasColumnName("action").HasMaxLength(120).IsRequired();
            entity.Property(auditLog => auditLog.Outcome).HasColumnName("outcome").HasMaxLength(24).IsRequired();
            entity.Property(auditLog => auditLog.TargetType).HasColumnName("target_type").HasMaxLength(80).IsRequired();
            entity.Property(auditLog => auditLog.TargetId).HasColumnName("target_id").HasMaxLength(120);
            entity.Property(auditLog => auditLog.Message).HasColumnName("message").HasMaxLength(1000).IsRequired();
            entity.Property(auditLog => auditLog.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb").IsRequired();
            entity.Property(auditLog => auditLog.IpAddress).HasColumnName("ip_address").HasMaxLength(80).IsRequired();
            entity.Property(auditLog => auditLog.UserAgent).HasColumnName("user_agent").HasMaxLength(300).IsRequired();
            entity.Property(auditLog => auditLog.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(auditLog => new { auditLog.OrganizationId, auditLog.CreatedAt });
            entity.HasIndex(auditLog => auditLog.ActorUserId);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(auditLog => auditLog.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(auditLog => auditLog.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(auditLog => auditLog.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(auditLog => auditLog.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureControlAction(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ControlAction>(entity =>
        {
            entity.ToTable("control_actions");
            entity.HasKey(controlAction => controlAction.Id);

            entity.Property(controlAction => controlAction.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(controlAction => controlAction.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(controlAction => controlAction.ProjectId).HasColumnName("project_id");
            entity.Property(controlAction => controlAction.EnvironmentId).HasColumnName("environment_id");
            entity.Property(controlAction => controlAction.ActionType).HasColumnName("action_type").HasMaxLength(120).IsRequired();
            entity.Property(controlAction => controlAction.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(controlAction => controlAction.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
            entity.Property(controlAction => controlAction.TargetType).HasColumnName("target_type").HasMaxLength(80).IsRequired();
            entity.Property(controlAction => controlAction.TargetId).HasColumnName("target_id").HasMaxLength(120);
            entity.Property(controlAction => controlAction.RequestJson).HasColumnName("request_json").HasColumnType("jsonb").IsRequired();
            entity.Property(controlAction => controlAction.ResultJson).HasColumnName("result_json").HasColumnType("jsonb").IsRequired();
            entity.Property(controlAction => controlAction.CorrelationId).HasColumnName("correlation_id").HasMaxLength(120);
            entity.Property(controlAction => controlAction.RequestedAt).HasColumnName("requested_at").IsRequired();
            entity.Property(controlAction => controlAction.StartedAt).HasColumnName("started_at");
            entity.Property(controlAction => controlAction.CompletedAt).HasColumnName("completed_at");

            entity.HasIndex(controlAction => new { controlAction.OrganizationId, controlAction.RequestedAt });
            entity.HasIndex(controlAction => controlAction.ProjectId);
            entity.HasIndex(controlAction => controlAction.EnvironmentId);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(controlAction => controlAction.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(controlAction => controlAction.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(controlAction => controlAction.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(controlAction => controlAction.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLiveApp(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LiveApp>(entity =>
        {
            entity.ToTable("live_apps");
            entity.HasKey(liveApp => liveApp.Id);

            entity.Property(liveApp => liveApp.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(liveApp => liveApp.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(liveApp => liveApp.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(liveApp => liveApp.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(liveApp => liveApp.Repo).HasColumnName("repo").HasMaxLength(220).IsRequired();
            entity.Property(liveApp => liveApp.NormalizedRepo).HasColumnName("normalized_repo").HasMaxLength(220).IsRequired();
            entity.Property(liveApp => liveApp.ServiceUrl).HasColumnName("service_url").HasMaxLength(1000).IsRequired();
            entity.Property(liveApp => liveApp.HealthUrl).HasColumnName("health_url").HasMaxLength(1000).IsRequired();
            entity.Property(liveApp => liveApp.CurrentCommitSha).HasColumnName("current_commit_sha").HasMaxLength(64).IsRequired();
            entity.Property(liveApp => liveApp.Version).HasColumnName("version").HasMaxLength(120).IsRequired();
            entity.Property(liveApp => liveApp.ImageDigest).HasColumnName("image_digest").HasMaxLength(400).IsRequired();
            entity.Property(liveApp => liveApp.CapabilitiesJson).HasColumnName("capabilities_json").HasColumnType("jsonb").IsRequired();
            entity.Property(liveApp => liveApp.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(liveApp => liveApp.LastRegisteredAt).HasColumnName("last_registered_at").IsRequired();

            entity.HasIndex(liveApp => liveApp.OrganizationId);
            entity.HasIndex(liveApp => liveApp.ProjectId);
            entity.HasIndex(liveApp => liveApp.EnvironmentId);
            entity.HasIndex(liveApp => new { liveApp.OrganizationId, liveApp.ProjectId, liveApp.EnvironmentId, liveApp.NormalizedRepo }).IsUnique();

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(liveApp => liveApp.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(liveApp => liveApp.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(liveApp => liveApp.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLiveAppDeployment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LiveAppDeployment>(entity =>
        {
            entity.ToTable("live_app_deployments");
            entity.HasKey(deployment => deployment.Id);

            entity.Property(deployment => deployment.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(deployment => deployment.LiveAppId).HasColumnName("live_app_id").IsRequired();
            entity.Property(deployment => deployment.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(deployment => deployment.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(deployment => deployment.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(deployment => deployment.Repo).HasColumnName("repo").HasMaxLength(220).IsRequired();
            entity.Property(deployment => deployment.ServiceUrl).HasColumnName("service_url").HasMaxLength(1000).IsRequired();
            entity.Property(deployment => deployment.HealthUrl).HasColumnName("health_url").HasMaxLength(1000).IsRequired();
            entity.Property(deployment => deployment.CommitSha).HasColumnName("commit_sha").HasMaxLength(64).IsRequired();
            entity.Property(deployment => deployment.Version).HasColumnName("version").HasMaxLength(120).IsRequired();
            entity.Property(deployment => deployment.ImageDigest).HasColumnName("image_digest").HasMaxLength(400).IsRequired();
            entity.Property(deployment => deployment.CapabilitiesJson).HasColumnName("capabilities_json").HasColumnType("jsonb").IsRequired();
            entity.Property(deployment => deployment.RegisteredAt).HasColumnName("registered_at").IsRequired();

            entity.HasIndex(deployment => new { deployment.LiveAppId, deployment.RegisteredAt });
            entity.HasIndex(deployment => new { deployment.OrganizationId, deployment.RegisteredAt });

            entity.HasOne<LiveApp>()
                .WithMany()
                .HasForeignKey(deployment => deployment.LiveAppId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(deployment => deployment.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(deployment => deployment.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(deployment => deployment.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRegistrationToken(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegistrationToken>(entity =>
        {
            entity.ToTable("registration_tokens");
            entity.HasKey(token => token.Id);

            entity.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(token => token.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(token => token.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(token => token.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(token => token.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(token => token.TokenPrefix).HasColumnName("token_prefix").HasMaxLength(32).IsRequired();
            entity.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
            entity.Property(token => token.Scope).HasColumnName("scope").HasMaxLength(80).IsRequired();
            entity.Property(token => token.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(token => token.RevokedByUserId).HasColumnName("revoked_by_user_id");
            entity.Property(token => token.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(token => token.LastUsedAt).HasColumnName("last_used_at");
            entity.Property(token => token.RevokedAt).HasColumnName("revoked_at");

            entity.HasIndex(token => token.OrganizationId);
            entity.HasIndex(token => token.ProjectId);
            entity.HasIndex(token => token.EnvironmentId);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.OrganizationId, token.ProjectId, token.EnvironmentId, token.CreatedAt });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(token => token.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(token => token.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(token => token.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(token => token.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(token => token.RevokedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureApiKey(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("api_keys");
            entity.HasKey(apiKey => apiKey.Id);

            entity.Property(apiKey => apiKey.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(apiKey => apiKey.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(apiKey => apiKey.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(apiKey => apiKey.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(apiKey => apiKey.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(apiKey => apiKey.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(32).IsRequired();
            entity.Property(apiKey => apiKey.KeyHash).HasColumnName("key_hash").HasMaxLength(64).IsRequired();
            entity.Property(apiKey => apiKey.ScopesJson).HasColumnName("scopes_json").HasColumnType("jsonb").IsRequired();
            entity.Property(apiKey => apiKey.RateLimitPerMinute).HasColumnName("rate_limit_per_minute").IsRequired();
            entity.Property(apiKey => apiKey.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(apiKey => apiKey.RevokedByUserId).HasColumnName("revoked_by_user_id");
            entity.Property(apiKey => apiKey.RotatedFromApiKeyId).HasColumnName("rotated_from_api_key_id");
            entity.Property(apiKey => apiKey.RotatedToApiKeyId).HasColumnName("rotated_to_api_key_id");
            entity.Property(apiKey => apiKey.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(apiKey => apiKey.LastUsedAt).HasColumnName("last_used_at");
            entity.Property(apiKey => apiKey.RevokedAt).HasColumnName("revoked_at");
            entity.Property(apiKey => apiKey.RotatedAt).HasColumnName("rotated_at");
            entity.Property(apiKey => apiKey.TotalRequestCount).HasColumnName("total_request_count").IsRequired();
            entity.Property(apiKey => apiKey.FailureCount).HasColumnName("failure_count").IsRequired();
            entity.Property(apiKey => apiKey.RateLimitHitCount).HasColumnName("rate_limit_hit_count").IsRequired();
            entity.Property(apiKey => apiKey.TotalLatencyMilliseconds).HasColumnName("total_latency_milliseconds").IsRequired();
            entity.Property(apiKey => apiKey.LatencySampleCount).HasColumnName("latency_sample_count").IsRequired();

            entity.HasIndex(apiKey => apiKey.OrganizationId);
            entity.HasIndex(apiKey => apiKey.ProjectId);
            entity.HasIndex(apiKey => apiKey.EnvironmentId);
            entity.HasIndex(apiKey => apiKey.KeyHash).IsUnique();
            entity.HasIndex(apiKey => new { apiKey.OrganizationId, apiKey.ProjectId, apiKey.EnvironmentId, apiKey.CreatedAt });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(apiKey => apiKey.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(apiKey => apiKey.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(apiKey => apiKey.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(apiKey => apiKey.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(apiKey => apiKey.RevokedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureApiKeyUsageDaily(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKeyUsageDaily>(entity =>
        {
            entity.ToTable("api_key_usage_daily");
            entity.HasKey(usage => usage.Id);

            entity.Property(usage => usage.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(usage => usage.ApiKeyId).HasColumnName("api_key_id").IsRequired();
            entity.Property(usage => usage.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(usage => usage.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(usage => usage.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(usage => usage.Day).HasColumnName("day").HasColumnType("date").IsRequired();
            entity.Property(usage => usage.Endpoint).HasColumnName("endpoint").HasMaxLength(160).IsRequired();
            entity.Property(usage => usage.RequestCount).HasColumnName("request_count").IsRequired();
            entity.Property(usage => usage.FailureCount).HasColumnName("failure_count").IsRequired();
            entity.Property(usage => usage.RateLimitHitCount).HasColumnName("rate_limit_hit_count").IsRequired();
            entity.Property(usage => usage.TotalLatencyMilliseconds).HasColumnName("total_latency_milliseconds").IsRequired();
            entity.Property(usage => usage.LatencySampleCount).HasColumnName("latency_sample_count").IsRequired();
            entity.Property(usage => usage.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(usage => usage.ApiKeyId);
            entity.HasIndex(usage => new { usage.OrganizationId, usage.Day });
            entity.HasIndex(usage => new { usage.ApiKeyId, usage.Endpoint, usage.Day }).IsUnique();

            entity.HasOne<ApiKey>()
                .WithMany()
                .HasForeignKey(usage => usage.ApiKeyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(usage => usage.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(usage => usage.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(usage => usage.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureApiKeyRateLimitWindow(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKeyRateLimitWindow>(entity =>
        {
            entity.ToTable("api_key_rate_limit_windows");
            entity.HasKey(window => window.Id);

            entity.Property(window => window.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(window => window.ApiKeyId).HasColumnName("api_key_id").IsRequired();
            entity.Property(window => window.Endpoint).HasColumnName("endpoint").HasMaxLength(160).IsRequired();
            entity.Property(window => window.WindowStart).HasColumnName("window_start").IsRequired();
            entity.Property(window => window.RequestCount).HasColumnName("request_count").IsRequired();
            entity.Property(window => window.RateLimitHitCount).HasColumnName("rate_limit_hit_count").IsRequired();
            entity.Property(window => window.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(window => window.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(window => window.ApiKeyId);
            entity.HasIndex(window => new { window.ApiKeyId, window.Endpoint, window.WindowStart }).IsUnique();

            entity.HasOne<ApiKey>()
                .WithMany()
                .HasForeignKey(window => window.ApiKeyId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDataProtectionKey(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataProtectionKey>(entity =>
        {
            entity.ToTable("data_protection_keys");
            entity.HasKey(dataProtectionKey => dataProtectionKey.Id);

            entity.Property(dataProtectionKey => dataProtectionKey.Id).HasColumnName("id");
            entity.Property(dataProtectionKey => dataProtectionKey.FriendlyName)
                .HasColumnName("friendly_name")
                .HasMaxLength(200);
            entity.Property(dataProtectionKey => dataProtectionKey.Xml)
                .HasColumnName("xml")
                .HasColumnType("text");
        });
    }
}
