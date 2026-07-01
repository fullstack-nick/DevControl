using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Stage7MonitoringIncidentsStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incidents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    root_cause_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    postmortem_draft = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidents", x => x.id);
                    table.ForeignKey(
                        name: "FK_incidents_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incidents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incidents_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incidents_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incidents_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "status_releases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_releases", x => x.id);
                    table.ForeignKey(
                        name: "FK_status_releases_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_status_releases_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_status_releases_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_status_releases_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_status_releases_users_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "uptime_monitors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_app_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_managed_from_live_app = table.Column<bool>(type: "boolean", nullable: false),
                    is_paused = table.Column<bool>(type: "boolean", nullable: false),
                    current_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    slow_threshold_milliseconds = table.Column<int>(type: "integer", nullable: false),
                    failure_threshold = table.Column<int>(type: "integer", nullable: false),
                    recovery_threshold = table.Column<int>(type: "integer", nullable: false),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    consecutive_recoveries = table.Column<int>(type: "integer", nullable: false),
                    next_check_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_success_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_failure_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paused_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paused_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processing_lease_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    processing_lease_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uptime_monitors", x => x.id);
                    table.ForeignKey(
                        name: "FK_uptime_monitors_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_uptime_monitors_live_apps_live_app_id",
                        column: x => x.live_app_id,
                        principalTable: "live_apps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_uptime_monitors_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_uptime_monitors_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_uptime_monitors_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_uptime_monitors_users_paused_by_user_id",
                        column: x => x.paused_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_uptime_monitors_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "incident_updates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_updates", x => x.id);
                    table.ForeignKey(
                        name: "FK_incident_updates_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incident_updates_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incident_updates_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incident_updates_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incident_updates_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "incident_monitors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uptime_monitor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_monitors", x => x.id);
                    table.ForeignKey(
                        name: "FK_incident_monitors_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incident_monitors_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incident_monitors_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incident_monitors_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incident_monitors_uptime_monitors_uptime_monitor_id",
                        column: x => x.uptime_monitor_id,
                        principalTable: "uptime_monitors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "monitor_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uptime_monitor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_app_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    result_kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    duration_milliseconds = table.Column<long>(type: "bigint", nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    response_preview = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    response_truncated = table.Column<bool>(type: "boolean", nullable: false),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitor_checks", x => x.id);
                    table.ForeignKey(
                        name: "FK_monitor_checks_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_monitor_checks_live_apps_live_app_id",
                        column: x => x.live_app_id,
                        principalTable: "live_apps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_monitor_checks_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_monitor_checks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_monitor_checks_uptime_monitors_uptime_monitor_id",
                        column: x => x.uptime_monitor_id,
                        principalTable: "uptime_monitors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incident_monitors_environment_id",
                table: "incident_monitors",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_monitors_incident_id",
                table: "incident_monitors",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_monitors_incident_id_uptime_monitor_id",
                table: "incident_monitors",
                columns: new[] { "incident_id", "uptime_monitor_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_incident_monitors_organization_id",
                table: "incident_monitors",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_monitors_project_id",
                table: "incident_monitors",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_monitors_uptime_monitor_id",
                table: "incident_monitors",
                column: "uptime_monitor_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_updates_created_by_user_id",
                table: "incident_updates",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_updates_environment_id_visibility_created_at",
                table: "incident_updates",
                columns: new[] { "environment_id", "visibility", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_incident_updates_incident_id",
                table: "incident_updates",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_updates_incident_id_created_at",
                table: "incident_updates",
                columns: new[] { "incident_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_incident_updates_organization_id",
                table: "incident_updates",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_updates_project_id",
                table: "incident_updates",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_created_by_user_id",
                table: "incidents",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_environment_id",
                table: "incidents",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_environment_id_status_created_at",
                table: "incidents",
                columns: new[] { "environment_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_organization_id",
                table: "incidents",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_project_id",
                table: "incidents",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_updated_by_user_id",
                table: "incidents",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_monitor_checks_environment_id_checked_at",
                table: "monitor_checks",
                columns: new[] { "environment_id", "checked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_monitor_checks_live_app_id",
                table: "monitor_checks",
                column: "live_app_id");

            migrationBuilder.CreateIndex(
                name: "IX_monitor_checks_organization_id",
                table: "monitor_checks",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_monitor_checks_project_id",
                table: "monitor_checks",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_monitor_checks_uptime_monitor_id",
                table: "monitor_checks",
                column: "uptime_monitor_id");

            migrationBuilder.CreateIndex(
                name: "IX_monitor_checks_uptime_monitor_id_checked_at",
                table: "monitor_checks",
                columns: new[] { "uptime_monitor_id", "checked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_status_releases_created_by_user_id",
                table: "status_releases",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_status_releases_environment_id",
                table: "status_releases",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_status_releases_environment_id_status_published_at",
                table: "status_releases",
                columns: new[] { "environment_id", "status", "published_at" });

            migrationBuilder.CreateIndex(
                name: "IX_status_releases_organization_id",
                table: "status_releases",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_status_releases_project_id",
                table: "status_releases",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_status_releases_published_by_user_id",
                table: "status_releases",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_uptime_monitors_created_by_user_id",
                table: "uptime_monitors",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_uptime_monitors_environment_id",
                table: "uptime_monitors",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_uptime_monitors_environment_id_current_status",
                table: "uptime_monitors",
                columns: new[] { "environment_id", "current_status" });

            migrationBuilder.CreateIndex(
                name: "IX_uptime_monitors_is_paused_next_check_at",
                table: "uptime_monitors",
                columns: new[] { "is_paused", "next_check_at" });

            migrationBuilder.CreateIndex(
                name: "IX_uptime_monitors_live_app_id",
                table: "uptime_monitors",
                column: "live_app_id",
                unique: true,
                filter: "live_app_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_uptime_monitors_organization_id",
                table: "uptime_monitors",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_uptime_monitors_paused_by_user_id",
                table: "uptime_monitors",
                column: "paused_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_uptime_monitors_project_id",
                table: "uptime_monitors",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_uptime_monitors_updated_by_user_id",
                table: "uptime_monitors",
                column: "updated_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_monitors");

            migrationBuilder.DropTable(
                name: "incident_updates");

            migrationBuilder.DropTable(
                name: "monitor_checks");

            migrationBuilder.DropTable(
                name: "status_releases");

            migrationBuilder.DropTable(
                name: "incidents");

            migrationBuilder.DropTable(
                name: "uptime_monitors");
        }
    }
}
