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
