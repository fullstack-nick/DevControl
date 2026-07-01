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

    public DbSet<GitHubInstallation> GitHubInstallations => Set<GitHubInstallation>();

    public DbSet<GitHubRepoConnection> GitHubRepoConnections => Set<GitHubRepoConnection>();

    public DbSet<GitHubOnboardingPullRequest> GitHubOnboardingPullRequests => Set<GitHubOnboardingPullRequest>();

    public DbSet<GitHubWorkflowDispatch> GitHubWorkflowDispatches => Set<GitHubWorkflowDispatch>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<ApiKeyUsageDaily> ApiKeyUsageDaily => Set<ApiKeyUsageDaily>();

    public DbSet<ApiKeyRateLimitWindow> ApiKeyRateLimitWindows => Set<ApiKeyRateLimitWindow>();

    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    public DbSet<FeatureFlagChange> FeatureFlagChanges => Set<FeatureFlagChange>();

    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();

    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    public DbSet<WebhookDeliveryAttempt> WebhookDeliveryAttempts => Set<WebhookDeliveryAttempt>();

    public DbSet<UptimeMonitor> UptimeMonitors => Set<UptimeMonitor>();

    public DbSet<MonitorCheck> MonitorChecks => Set<MonitorCheck>();

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<IncidentUpdate> IncidentUpdates => Set<IncidentUpdate>();

    public DbSet<IncidentMonitor> IncidentMonitors => Set<IncidentMonitor>();

    public DbSet<StatusRelease> StatusReleases => Set<StatusRelease>();

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
        ConfigureGitHubInstallation(modelBuilder);
        ConfigureGitHubRepoConnection(modelBuilder);
        ConfigureGitHubOnboardingPullRequest(modelBuilder);
        ConfigureGitHubWorkflowDispatch(modelBuilder);
        ConfigureApiKey(modelBuilder);
        ConfigureApiKeyUsageDaily(modelBuilder);
        ConfigureApiKeyRateLimitWindow(modelBuilder);
        ConfigureFeatureFlag(modelBuilder);
        ConfigureFeatureFlagChange(modelBuilder);
        ConfigureWebhookEndpoint(modelBuilder);
        ConfigureWebhookEvent(modelBuilder);
        ConfigureWebhookDelivery(modelBuilder);
        ConfigureWebhookDeliveryAttempt(modelBuilder);
        ConfigureUptimeMonitor(modelBuilder);
        ConfigureMonitorCheck(modelBuilder);
        ConfigureIncident(modelBuilder);
        ConfigureIncidentUpdate(modelBuilder);
        ConfigureIncidentMonitor(modelBuilder);
        ConfigureStatusRelease(modelBuilder);
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
            entity.Property(liveApp => liveApp.GitHubRunId).HasColumnName("github_run_id");
            entity.Property(liveApp => liveApp.GitHubRunUrl).HasColumnName("github_run_url").HasMaxLength(500).IsRequired();
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
            entity.Property(deployment => deployment.GitHubRunId).HasColumnName("github_run_id");
            entity.Property(deployment => deployment.GitHubRunUrl).HasColumnName("github_run_url").HasMaxLength(500).IsRequired();
            entity.Property(deployment => deployment.RegisteredAt).HasColumnName("registered_at").IsRequired();

            entity.HasIndex(deployment => new { deployment.LiveAppId, deployment.RegisteredAt });
            entity.HasIndex(deployment => new { deployment.OrganizationId, deployment.RegisteredAt });
            entity.HasIndex(deployment => deployment.GitHubRunId);

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

    private static void ConfigureGitHubInstallation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GitHubInstallation>(entity =>
        {
            entity.ToTable("github_installations");
            entity.HasKey(installation => installation.Id);

            entity.Property(installation => installation.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(installation => installation.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(installation => installation.InstallationId).HasColumnName("installation_id").IsRequired();
            entity.Property(installation => installation.AccountLogin).HasColumnName("account_login").HasMaxLength(160).IsRequired();
            entity.Property(installation => installation.AccountType).HasColumnName("account_type").HasMaxLength(40).IsRequired();
            entity.Property(installation => installation.RepositorySelection).HasColumnName("repository_selection").HasMaxLength(40).IsRequired();
            entity.Property(installation => installation.PermissionsJson).HasColumnName("permissions_json").HasColumnType("jsonb").IsRequired();
            entity.Property(installation => installation.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(installation => installation.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(installation => installation.OrganizationId);
            entity.HasIndex(installation => new { installation.OrganizationId, installation.InstallationId }).IsUnique();

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(installation => installation.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureGitHubRepoConnection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GitHubRepoConnection>(entity =>
        {
            entity.ToTable("github_repo_connections");
            entity.HasKey(connection => connection.Id);

            entity.Property(connection => connection.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(connection => connection.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(connection => connection.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(connection => connection.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(connection => connection.GitHubInstallationId).HasColumnName("github_installation_id").IsRequired();
            entity.Property(connection => connection.LiveAppId).HasColumnName("live_app_id");
            entity.Property(connection => connection.Repo).HasColumnName("repo").HasMaxLength(220).IsRequired();
            entity.Property(connection => connection.NormalizedRepo).HasColumnName("normalized_repo").HasMaxLength(220).IsRequired();
            entity.Property(connection => connection.DefaultBranch).HasColumnName("default_branch").HasMaxLength(160).IsRequired();
            entity.Property(connection => connection.WorkflowPath).HasColumnName("workflow_path").HasMaxLength(300).IsRequired();
            entity.Property(connection => connection.WorkflowName).HasColumnName("workflow_name").HasMaxLength(160).IsRequired();
            entity.Property(connection => connection.JobId).HasColumnName("job_id").HasMaxLength(120).IsRequired();
            entity.Property(connection => connection.ServiceUrlExpression).HasColumnName("service_url_expression").HasMaxLength(500).IsRequired();
            entity.Property(connection => connection.HealthUrlExpression).HasColumnName("health_url_expression").HasMaxLength(500).IsRequired();
            entity.Property(connection => connection.VersionExpression).HasColumnName("version_expression").HasMaxLength(200).IsRequired();
            entity.Property(connection => connection.ImageDigestExpression).HasColumnName("image_digest_expression").HasMaxLength(300).IsRequired();
            entity.Property(connection => connection.CapabilitiesJson).HasColumnName("capabilities_json").HasColumnType("jsonb").IsRequired();
            entity.Property(connection => connection.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(connection => connection.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(connection => connection.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(connection => connection.OrganizationId);
            entity.HasIndex(connection => connection.ProjectId);
            entity.HasIndex(connection => connection.EnvironmentId);
            entity.HasIndex(connection => connection.GitHubInstallationId);
            entity.HasIndex(connection => connection.LiveAppId);
            entity.HasIndex(connection => new { connection.OrganizationId, connection.ProjectId, connection.EnvironmentId, connection.NormalizedRepo }).IsUnique();

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(connection => connection.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(connection => connection.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(connection => connection.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GitHubInstallation>()
                .WithMany()
                .HasForeignKey(connection => connection.GitHubInstallationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<LiveApp>()
                .WithMany()
                .HasForeignKey(connection => connection.LiveAppId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(connection => connection.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureGitHubOnboardingPullRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GitHubOnboardingPullRequest>(entity =>
        {
            entity.ToTable("github_onboarding_pull_requests");
            entity.HasKey(pullRequest => pullRequest.Id);

            entity.Property(pullRequest => pullRequest.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(pullRequest => pullRequest.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(pullRequest => pullRequest.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(pullRequest => pullRequest.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(pullRequest => pullRequest.RepoConnectionId).HasColumnName("repo_connection_id").IsRequired();
            entity.Property(pullRequest => pullRequest.Repo).HasColumnName("repo").HasMaxLength(220).IsRequired();
            entity.Property(pullRequest => pullRequest.WorkflowPath).HasColumnName("workflow_path").HasMaxLength(300).IsRequired();
            entity.Property(pullRequest => pullRequest.BaseBranch).HasColumnName("base_branch").HasMaxLength(160).IsRequired();
            entity.Property(pullRequest => pullRequest.HeadBranch).HasColumnName("head_branch").HasMaxLength(200).IsRequired();
            entity.Property(pullRequest => pullRequest.PullRequestNumber).HasColumnName("pull_request_number").IsRequired();
            entity.Property(pullRequest => pullRequest.PullRequestUrl).HasColumnName("pull_request_url").HasMaxLength(500).IsRequired();
            entity.Property(pullRequest => pullRequest.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
            entity.Property(pullRequest => pullRequest.Error).HasColumnName("error").HasMaxLength(1000).IsRequired();
            entity.Property(pullRequest => pullRequest.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(pullRequest => pullRequest.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(pullRequest => pullRequest.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(pullRequest => pullRequest.MergedAt).HasColumnName("merged_at");
            entity.Property(pullRequest => pullRequest.ClosedAt).HasColumnName("closed_at");

            entity.HasIndex(pullRequest => pullRequest.OrganizationId);
            entity.HasIndex(pullRequest => pullRequest.RepoConnectionId);
            entity.HasIndex(pullRequest => new { pullRequest.OrganizationId, pullRequest.Status, pullRequest.UpdatedAt });
            entity.HasIndex(pullRequest => new { pullRequest.OrganizationId, pullRequest.Repo, pullRequest.PullRequestNumber }).IsUnique();

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(pullRequest => pullRequest.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(pullRequest => pullRequest.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(pullRequest => pullRequest.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GitHubRepoConnection>()
                .WithMany()
                .HasForeignKey(pullRequest => pullRequest.RepoConnectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(pullRequest => pullRequest.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureGitHubWorkflowDispatch(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GitHubWorkflowDispatch>(entity =>
        {
            entity.ToTable("github_workflow_dispatches");
            entity.HasKey(dispatch => dispatch.Id);

            entity.Property(dispatch => dispatch.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(dispatch => dispatch.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(dispatch => dispatch.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(dispatch => dispatch.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(dispatch => dispatch.RepoConnectionId).HasColumnName("repo_connection_id").IsRequired();
            entity.Property(dispatch => dispatch.LiveAppId).HasColumnName("live_app_id").IsRequired();
            entity.Property(dispatch => dispatch.ControlActionId).HasColumnName("control_action_id").IsRequired();
            entity.Property(dispatch => dispatch.Action).HasColumnName("action").HasMaxLength(40).IsRequired();
            entity.Property(dispatch => dispatch.Repo).HasColumnName("repo").HasMaxLength(220).IsRequired();
            entity.Property(dispatch => dispatch.WorkflowPath).HasColumnName("workflow_path").HasMaxLength(300).IsRequired();
            entity.Property(dispatch => dispatch.Ref).HasColumnName("ref").HasMaxLength(160).IsRequired();
            entity.Property(dispatch => dispatch.GitHubRunId).HasColumnName("github_run_id");
            entity.Property(dispatch => dispatch.RunUrl).HasColumnName("run_url").HasMaxLength(500).IsRequired();
            entity.Property(dispatch => dispatch.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
            entity.Property(dispatch => dispatch.Conclusion).HasColumnName("conclusion").HasMaxLength(40).IsRequired();
            entity.Property(dispatch => dispatch.InputsJson).HasColumnName("inputs_json").HasColumnType("jsonb").IsRequired();
            entity.Property(dispatch => dispatch.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
            entity.Property(dispatch => dispatch.RequestedAt).HasColumnName("requested_at").IsRequired();
            entity.Property(dispatch => dispatch.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(dispatch => dispatch.CompletedAt).HasColumnName("completed_at");

            entity.HasIndex(dispatch => dispatch.OrganizationId);
            entity.HasIndex(dispatch => dispatch.RepoConnectionId);
            entity.HasIndex(dispatch => dispatch.LiveAppId);
            entity.HasIndex(dispatch => dispatch.ControlActionId).IsUnique();
            entity.HasIndex(dispatch => dispatch.GitHubRunId);
            entity.HasIndex(dispatch => new { dispatch.OrganizationId, dispatch.CompletedAt, dispatch.UpdatedAt });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(dispatch => dispatch.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(dispatch => dispatch.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(dispatch => dispatch.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GitHubRepoConnection>()
                .WithMany()
                .HasForeignKey(dispatch => dispatch.RepoConnectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<LiveApp>()
                .WithMany()
                .HasForeignKey(dispatch => dispatch.LiveAppId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ControlAction>()
                .WithMany()
                .HasForeignKey(dispatch => dispatch.ControlActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(dispatch => dispatch.RequestedByUserId)
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

    private static void ConfigureFeatureFlag(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeatureFlag>(entity =>
        {
            entity.ToTable("feature_flags");
            entity.HasKey(flag => flag.Id);

            entity.Property(flag => flag.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(flag => flag.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(flag => flag.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(flag => flag.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(flag => flag.Key).HasColumnName("key").HasMaxLength(120).IsRequired();
            entity.Property(flag => flag.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(flag => flag.Description).HasColumnName("description").HasMaxLength(1000).IsRequired();
            entity.Property(flag => flag.Kind)
                .HasColumnName("kind")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(flag => flag.IsEnabled).HasColumnName("is_enabled").IsRequired();
            entity.Property(flag => flag.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(flag => flag.LastChangedByUserId).HasColumnName("last_changed_by_user_id").IsRequired();
            entity.Property(flag => flag.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(flag => flag.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(flag => flag.LastChangedAt).HasColumnName("last_changed_at").IsRequired();

            entity.HasIndex(flag => flag.OrganizationId);
            entity.HasIndex(flag => flag.ProjectId);
            entity.HasIndex(flag => flag.EnvironmentId);
            entity.HasIndex(flag => new { flag.OrganizationId, flag.ProjectId, flag.EnvironmentId, flag.Key }).IsUnique();
            entity.HasIndex(flag => new { flag.EnvironmentId, flag.Kind });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(flag => flag.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(flag => flag.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(flag => flag.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(flag => flag.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(flag => flag.LastChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureFeatureFlagChange(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeatureFlagChange>(entity =>
        {
            entity.ToTable("feature_flag_changes");
            entity.HasKey(change => change.Id);

            entity.Property(change => change.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(change => change.FeatureFlagId).HasColumnName("feature_flag_id").IsRequired();
            entity.Property(change => change.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(change => change.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(change => change.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(change => change.OldValue).HasColumnName("old_value").IsRequired();
            entity.Property(change => change.NewValue).HasColumnName("new_value").IsRequired();
            entity.Property(change => change.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
            entity.Property(change => change.ChangedByUserId).HasColumnName("changed_by_user_id").IsRequired();
            entity.Property(change => change.ChangedAt).HasColumnName("changed_at").IsRequired();

            entity.HasIndex(change => change.FeatureFlagId);
            entity.HasIndex(change => new { change.OrganizationId, change.ChangedAt });
            entity.HasIndex(change => new { change.EnvironmentId, change.ChangedAt });

            entity.HasOne<FeatureFlag>()
                .WithMany()
                .HasForeignKey(change => change.FeatureFlagId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(change => change.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(change => change.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(change => change.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(change => change.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWebhookEndpoint(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebhookEndpoint>(entity =>
        {
            entity.ToTable("webhook_endpoints");
            entity.HasKey(endpoint => endpoint.Id);

            entity.Property(endpoint => endpoint.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(endpoint => endpoint.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(endpoint => endpoint.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(endpoint => endpoint.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(endpoint => endpoint.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(endpoint => endpoint.Url).HasColumnName("url").HasMaxLength(1000).IsRequired();
            entity.Property(endpoint => endpoint.SecretPrefix).HasColumnName("secret_prefix").HasMaxLength(32).IsRequired();
            entity.Property(endpoint => endpoint.ProtectedSecret).HasColumnName("protected_secret").HasColumnType("text").IsRequired();
            entity.Property(endpoint => endpoint.EventTypesJson).HasColumnName("event_types_json").HasColumnType("jsonb").IsRequired();
            entity.Property(endpoint => endpoint.IsPaused).HasColumnName("is_paused").IsRequired();
            entity.Property(endpoint => endpoint.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(endpoint => endpoint.PausedByUserId).HasColumnName("paused_by_user_id");
            entity.Property(endpoint => endpoint.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(endpoint => endpoint.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(endpoint => endpoint.PausedAt).HasColumnName("paused_at");
            entity.Property(endpoint => endpoint.LastDeliveryAt).HasColumnName("last_delivery_at");
            entity.Property(endpoint => endpoint.LastSuccessAt).HasColumnName("last_success_at");
            entity.Property(endpoint => endpoint.LastFailureAt).HasColumnName("last_failure_at");

            entity.HasIndex(endpoint => endpoint.OrganizationId);
            entity.HasIndex(endpoint => endpoint.ProjectId);
            entity.HasIndex(endpoint => endpoint.EnvironmentId);
            entity.HasIndex(endpoint => new { endpoint.OrganizationId, endpoint.ProjectId, endpoint.EnvironmentId, endpoint.CreatedAt });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(endpoint => endpoint.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(endpoint => endpoint.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(endpoint => endpoint.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(endpoint => endpoint.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(endpoint => endpoint.PausedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWebhookEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.ToTable("webhook_events");
            entity.HasKey(webhookEvent => webhookEvent.Id);

            entity.Property(webhookEvent => webhookEvent.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(webhookEvent => webhookEvent.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(webhookEvent => webhookEvent.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(webhookEvent => webhookEvent.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(webhookEvent => webhookEvent.EventType).HasColumnName("event_type").HasMaxLength(120).IsRequired();
            entity.Property(webhookEvent => webhookEvent.ResourceType).HasColumnName("resource_type").HasMaxLength(80).IsRequired();
            entity.Property(webhookEvent => webhookEvent.ResourceId).HasColumnName("resource_id").HasMaxLength(120);
            entity.Property(webhookEvent => webhookEvent.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(webhookEvent => webhookEvent.ActorEmail).HasColumnName("actor_email").HasMaxLength(320).IsRequired();
            entity.Property(webhookEvent => webhookEvent.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
            entity.Property(webhookEvent => webhookEvent.OccurredAt).HasColumnName("occurred_at").IsRequired();
            entity.Property(webhookEvent => webhookEvent.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(webhookEvent => new { webhookEvent.OrganizationId, webhookEvent.OccurredAt });
            entity.HasIndex(webhookEvent => new { webhookEvent.EnvironmentId, webhookEvent.EventType, webhookEvent.OccurredAt });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(webhookEvent => webhookEvent.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(webhookEvent => webhookEvent.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(webhookEvent => webhookEvent.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(webhookEvent => webhookEvent.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWebhookDelivery(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebhookDelivery>(entity =>
        {
            entity.ToTable("webhook_deliveries");
            entity.HasKey(delivery => delivery.Id);

            entity.Property(delivery => delivery.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(delivery => delivery.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(delivery => delivery.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(delivery => delivery.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(delivery => delivery.WebhookEndpointId).HasColumnName("webhook_endpoint_id").IsRequired();
            entity.Property(delivery => delivery.WebhookEventId).HasColumnName("webhook_event_id").IsRequired();
            entity.Property(delivery => delivery.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(delivery => delivery.AttemptCount).HasColumnName("attempt_count").IsRequired();
            entity.Property(delivery => delivery.MaxAttempts).HasColumnName("max_attempts").IsRequired();
            entity.Property(delivery => delivery.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.Property(delivery => delivery.LastAttemptAt).HasColumnName("last_attempt_at");
            entity.Property(delivery => delivery.CompletedAt).HasColumnName("completed_at");
            entity.Property(delivery => delivery.LastStatusCode).HasColumnName("last_status_code");
            entity.Property(delivery => delivery.LastError).HasColumnName("last_error").HasMaxLength(1000).IsRequired();
            entity.Property(delivery => delivery.LastResponsePreview).HasColumnName("last_response_preview").HasMaxLength(16384).IsRequired();
            entity.Property(delivery => delivery.LastResponseTruncated).HasColumnName("last_response_truncated").IsRequired();
            entity.Property(delivery => delivery.ProcessingLeaseId).HasColumnName("processing_lease_id").HasMaxLength(120);
            entity.Property(delivery => delivery.ProcessingLeaseExpiresAt).HasColumnName("processing_lease_expires_at");
            entity.Property(delivery => delivery.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(delivery => delivery.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(delivery => delivery.OrganizationId);
            entity.HasIndex(delivery => delivery.WebhookEndpointId);
            entity.HasIndex(delivery => delivery.WebhookEventId);
            entity.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAt });
            entity.HasIndex(delivery => new { delivery.OrganizationId, delivery.CreatedAt });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(delivery => delivery.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(delivery => delivery.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(delivery => delivery.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<WebhookEndpoint>()
                .WithMany()
                .HasForeignKey(delivery => delivery.WebhookEndpointId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<WebhookEvent>()
                .WithMany()
                .HasForeignKey(delivery => delivery.WebhookEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWebhookDeliveryAttempt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebhookDeliveryAttempt>(entity =>
        {
            entity.ToTable("webhook_delivery_attempts");
            entity.HasKey(attempt => attempt.Id);

            entity.Property(attempt => attempt.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(attempt => attempt.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(attempt => attempt.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(attempt => attempt.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(attempt => attempt.WebhookEndpointId).HasColumnName("webhook_endpoint_id").IsRequired();
            entity.Property(attempt => attempt.WebhookEventId).HasColumnName("webhook_event_id").IsRequired();
            entity.Property(attempt => attempt.WebhookDeliveryId).HasColumnName("webhook_delivery_id").IsRequired();
            entity.Property(attempt => attempt.AttemptNumber).HasColumnName("attempt_number").IsRequired();
            entity.Property(attempt => attempt.ResultKind).HasColumnName("result_kind").HasMaxLength(40).IsRequired();
            entity.Property(attempt => attempt.Succeeded).HasColumnName("succeeded").IsRequired();
            entity.Property(attempt => attempt.StatusCode).HasColumnName("status_code");
            entity.Property(attempt => attempt.DurationMilliseconds).HasColumnName("duration_milliseconds").IsRequired();
            entity.Property(attempt => attempt.Error).HasColumnName("error").HasMaxLength(1000).IsRequired();
            entity.Property(attempt => attempt.ResponsePreview).HasColumnName("response_preview").HasMaxLength(16384).IsRequired();
            entity.Property(attempt => attempt.ResponseTruncated).HasColumnName("response_truncated").IsRequired();
            entity.Property(attempt => attempt.ResponseBytesRead).HasColumnName("response_bytes_read").IsRequired();
            entity.Property(attempt => attempt.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(attempt => attempt.OrganizationId);
            entity.HasIndex(attempt => attempt.WebhookEndpointId);
            entity.HasIndex(attempt => attempt.WebhookEventId);
            entity.HasIndex(attempt => new { attempt.WebhookDeliveryId, attempt.AttemptNumber }).IsUnique();
            entity.HasIndex(attempt => new { attempt.OrganizationId, attempt.CreatedAt });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(attempt => attempt.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(attempt => attempt.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(attempt => attempt.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<WebhookEndpoint>()
                .WithMany()
                .HasForeignKey(attempt => attempt.WebhookEndpointId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<WebhookEvent>()
                .WithMany()
                .HasForeignKey(attempt => attempt.WebhookEventId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<WebhookDelivery>()
                .WithMany()
                .HasForeignKey(attempt => attempt.WebhookDeliveryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUptimeMonitor(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UptimeMonitor>(entity =>
        {
            entity.ToTable("uptime_monitors");
            entity.HasKey(monitor => monitor.Id);

            entity.Property(monitor => monitor.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(monitor => monitor.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(monitor => monitor.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(monitor => monitor.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(monitor => monitor.LiveAppId).HasColumnName("live_app_id");
            entity.Property(monitor => monitor.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(monitor => monitor.Url).HasColumnName("url").HasMaxLength(1000).IsRequired();
            entity.Property(monitor => monitor.IsManagedFromLiveApp).HasColumnName("is_managed_from_live_app").IsRequired();
            entity.Property(monitor => monitor.IsPaused).HasColumnName("is_paused").IsRequired();
            entity.Property(monitor => monitor.CurrentStatus)
                .HasColumnName("current_status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(monitor => monitor.IntervalSeconds).HasColumnName("interval_seconds").IsRequired();
            entity.Property(monitor => monitor.TimeoutSeconds).HasColumnName("timeout_seconds").IsRequired();
            entity.Property(monitor => monitor.SlowThresholdMilliseconds).HasColumnName("slow_threshold_milliseconds").IsRequired();
            entity.Property(monitor => monitor.FailureThreshold).HasColumnName("failure_threshold").IsRequired();
            entity.Property(monitor => monitor.RecoveryThreshold).HasColumnName("recovery_threshold").IsRequired();
            entity.Property(monitor => monitor.ConsecutiveFailures).HasColumnName("consecutive_failures").IsRequired();
            entity.Property(monitor => monitor.ConsecutiveRecoveries).HasColumnName("consecutive_recoveries").IsRequired();
            entity.Property(monitor => monitor.NextCheckAt).HasColumnName("next_check_at").IsRequired();
            entity.Property(monitor => monitor.LastCheckedAt).HasColumnName("last_checked_at");
            entity.Property(monitor => monitor.LastSuccessAt).HasColumnName("last_success_at");
            entity.Property(monitor => monitor.LastFailureAt).HasColumnName("last_failure_at");
            entity.Property(monitor => monitor.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(monitor => monitor.UpdatedByUserId).HasColumnName("updated_by_user_id");
            entity.Property(monitor => monitor.PausedByUserId).HasColumnName("paused_by_user_id");
            entity.Property(monitor => monitor.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(monitor => monitor.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(monitor => monitor.PausedAt).HasColumnName("paused_at");
            entity.Property(monitor => monitor.ProcessingLeaseId).HasColumnName("processing_lease_id").HasMaxLength(120);
            entity.Property(monitor => monitor.ProcessingLeaseExpiresAt).HasColumnName("processing_lease_expires_at");

            entity.HasIndex(monitor => monitor.OrganizationId);
            entity.HasIndex(monitor => monitor.ProjectId);
            entity.HasIndex(monitor => monitor.EnvironmentId);
            entity.HasIndex(monitor => monitor.LiveAppId).IsUnique().HasFilter("live_app_id IS NOT NULL");
            entity.HasIndex(monitor => new { monitor.IsPaused, monitor.NextCheckAt });
            entity.HasIndex(monitor => new { monitor.EnvironmentId, monitor.CurrentStatus });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(monitor => monitor.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(monitor => monitor.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(monitor => monitor.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<LiveApp>()
                .WithMany()
                .HasForeignKey(monitor => monitor.LiveAppId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(monitor => monitor.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(monitor => monitor.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(monitor => monitor.PausedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMonitorCheck(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonitorCheck>(entity =>
        {
            entity.ToTable("monitor_checks");
            entity.HasKey(check => check.Id);

            entity.Property(check => check.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(check => check.UptimeMonitorId).HasColumnName("uptime_monitor_id").IsRequired();
            entity.Property(check => check.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(check => check.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(check => check.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(check => check.LiveAppId).HasColumnName("live_app_id");
            entity.Property(check => check.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(check => check.Succeeded).HasColumnName("succeeded").IsRequired();
            entity.Property(check => check.StatusCode).HasColumnName("status_code");
            entity.Property(check => check.ResultKind).HasColumnName("result_kind").HasMaxLength(40).IsRequired();
            entity.Property(check => check.DurationMilliseconds).HasColumnName("duration_milliseconds").IsRequired();
            entity.Property(check => check.Error).HasColumnName("error").HasMaxLength(1000).IsRequired();
            entity.Property(check => check.ResponsePreview).HasColumnName("response_preview").HasMaxLength(4096).IsRequired();
            entity.Property(check => check.ResponseTruncated).HasColumnName("response_truncated").IsRequired();
            entity.Property(check => check.CheckedAt).HasColumnName("checked_at").IsRequired();

            entity.HasIndex(check => check.OrganizationId);
            entity.HasIndex(check => check.UptimeMonitorId);
            entity.HasIndex(check => new { check.UptimeMonitorId, check.CheckedAt });
            entity.HasIndex(check => new { check.EnvironmentId, check.CheckedAt });

            entity.HasOne<UptimeMonitor>()
                .WithMany()
                .HasForeignKey(check => check.UptimeMonitorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(check => check.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(check => check.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(check => check.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<LiveApp>()
                .WithMany()
                .HasForeignKey(check => check.LiveAppId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureIncident(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Incident>(entity =>
        {
            entity.ToTable("incidents");
            entity.HasKey(incident => incident.Id);

            entity.Property(incident => incident.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(incident => incident.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(incident => incident.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(incident => incident.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(incident => incident.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            entity.Property(incident => incident.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(incident => incident.Summary).HasColumnName("summary").HasMaxLength(2000).IsRequired();
            entity.Property(incident => incident.RootCauseSummary).HasColumnName("root_cause_summary").HasMaxLength(4000).IsRequired();
            entity.Property(incident => incident.PostmortemDraft).HasColumnName("postmortem_draft").HasMaxLength(8000).IsRequired();
            entity.Property(incident => incident.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(incident => incident.UpdatedByUserId).HasColumnName("updated_by_user_id");
            entity.Property(incident => incident.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(incident => incident.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(incident => incident.ResolvedAt).HasColumnName("resolved_at");

            entity.HasIndex(incident => incident.OrganizationId);
            entity.HasIndex(incident => incident.ProjectId);
            entity.HasIndex(incident => incident.EnvironmentId);
            entity.HasIndex(incident => new { incident.EnvironmentId, incident.Status, incident.CreatedAt });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(incident => incident.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(incident => incident.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(incident => incident.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(incident => incident.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(incident => incident.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureIncidentUpdate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IncidentUpdate>(entity =>
        {
            entity.ToTable("incident_updates");
            entity.HasKey(update => update.Id);

            entity.Property(update => update.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(update => update.IncidentId).HasColumnName("incident_id").IsRequired();
            entity.Property(update => update.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(update => update.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(update => update.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(update => update.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(update => update.Visibility)
                .HasColumnName("visibility")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(update => update.Message).HasColumnName("message").HasMaxLength(4000).IsRequired();
            entity.Property(update => update.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(update => update.CreatedByEmail).HasColumnName("created_by_email").HasMaxLength(320).IsRequired();
            entity.Property(update => update.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(update => update.OrganizationId);
            entity.HasIndex(update => update.IncidentId);
            entity.HasIndex(update => new { update.IncidentId, update.CreatedAt });
            entity.HasIndex(update => new { update.EnvironmentId, update.Visibility, update.CreatedAt });

            entity.HasOne<Incident>()
                .WithMany()
                .HasForeignKey(update => update.IncidentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(update => update.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(update => update.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(update => update.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(update => update.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureIncidentMonitor(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IncidentMonitor>(entity =>
        {
            entity.ToTable("incident_monitors");
            entity.HasKey(link => link.Id);

            entity.Property(link => link.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(link => link.IncidentId).HasColumnName("incident_id").IsRequired();
            entity.Property(link => link.UptimeMonitorId).HasColumnName("uptime_monitor_id").IsRequired();
            entity.Property(link => link.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(link => link.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(link => link.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(link => link.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(link => link.OrganizationId);
            entity.HasIndex(link => link.IncidentId);
            entity.HasIndex(link => link.UptimeMonitorId);
            entity.HasIndex(link => new { link.IncidentId, link.UptimeMonitorId }).IsUnique();

            entity.HasOne<Incident>()
                .WithMany()
                .HasForeignKey(link => link.IncidentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UptimeMonitor>()
                .WithMany()
                .HasForeignKey(link => link.UptimeMonitorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(link => link.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(link => link.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(link => link.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureStatusRelease(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StatusRelease>(entity =>
        {
            entity.ToTable("status_releases");
            entity.HasKey(release => release.Id);

            entity.Property(release => release.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(release => release.OrganizationId).HasColumnName("organization_id").IsRequired();
            entity.Property(release => release.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(release => release.EnvironmentId).HasColumnName("environment_id").IsRequired();
            entity.Property(release => release.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            entity.Property(release => release.Version).HasColumnName("version").HasMaxLength(120).IsRequired();
            entity.Property(release => release.Body).HasColumnName("body").HasMaxLength(8000).IsRequired();
            entity.Property(release => release.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(release => release.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(release => release.PublishedByUserId).HasColumnName("published_by_user_id");
            entity.Property(release => release.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(release => release.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(release => release.PublishedAt).HasColumnName("published_at");

            entity.HasIndex(release => release.OrganizationId);
            entity.HasIndex(release => release.ProjectId);
            entity.HasIndex(release => release.EnvironmentId);
            entity.HasIndex(release => new { release.EnvironmentId, release.Status, release.PublishedAt });

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(release => release.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(release => release.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectEnvironment>()
                .WithMany()
                .HasForeignKey(release => release.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(release => release.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(release => release.PublishedByUserId)
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
