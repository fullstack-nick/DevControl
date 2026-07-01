using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Stage8GitHubAppLiveControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "github_run_id",
                table: "live_apps",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "github_run_url",
                table: "live_apps",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "github_run_id",
                table: "live_app_deployments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "github_run_url",
                table: "live_app_deployments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "github_installations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<long>(type: "bigint", nullable: false),
                    account_login = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    account_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    repository_selection = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    permissions_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_github_installations", x => x.id);
                    table.ForeignKey(
                        name: "FK_github_installations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "github_repo_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    github_installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_app_id = table.Column<Guid>(type: "uuid", nullable: true),
                    repo = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    normalized_repo = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    default_branch = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    workflow_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    workflow_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    job_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    service_url_expression = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    health_url_expression = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    version_expression = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    image_digest_expression = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    capabilities_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_github_repo_connections", x => x.id);
                    table.ForeignKey(
                        name: "FK_github_repo_connections_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_repo_connections_github_installations_github_install~",
                        column: x => x.github_installation_id,
                        principalTable: "github_installations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_repo_connections_live_apps_live_app_id",
                        column: x => x.live_app_id,
                        principalTable: "live_apps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_repo_connections_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_repo_connections_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_repo_connections_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "github_onboarding_pull_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repo_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repo = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    workflow_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    base_branch = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    head_branch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pull_request_number = table.Column<int>(type: "integer", nullable: false),
                    pull_request_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    merged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_github_onboarding_pull_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_github_onboarding_pull_requests_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_onboarding_pull_requests_github_repo_connections_rep~",
                        column: x => x.repo_connection_id,
                        principalTable: "github_repo_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_onboarding_pull_requests_organizations_organization_~",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_onboarding_pull_requests_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_onboarding_pull_requests_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "github_workflow_dispatches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repo_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_app_id = table.Column<Guid>(type: "uuid", nullable: false),
                    control_action_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    repo = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    workflow_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    @ref = table.Column<string>(name: "ref", type: "character varying(160)", maxLength: 160, nullable: false),
                    github_run_id = table.Column<long>(type: "bigint", nullable: true),
                    run_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    conclusion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    inputs_json = table.Column<string>(type: "jsonb", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_github_workflow_dispatches", x => x.id);
                    table.ForeignKey(
                        name: "FK_github_workflow_dispatches_control_actions_control_action_id",
                        column: x => x.control_action_id,
                        principalTable: "control_actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_workflow_dispatches_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_workflow_dispatches_github_repo_connections_repo_con~",
                        column: x => x.repo_connection_id,
                        principalTable: "github_repo_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_workflow_dispatches_live_apps_live_app_id",
                        column: x => x.live_app_id,
                        principalTable: "live_apps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_workflow_dispatches_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_workflow_dispatches_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_github_workflow_dispatches_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_app_deployments_github_run_id",
                table: "live_app_deployments",
                column: "github_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_installations_organization_id",
                table: "github_installations",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_installations_organization_id_installation_id",
                table: "github_installations",
                columns: new[] { "organization_id", "installation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_github_onboarding_pull_requests_created_by_user_id",
                table: "github_onboarding_pull_requests",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_onboarding_pull_requests_environment_id",
                table: "github_onboarding_pull_requests",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_onboarding_pull_requests_organization_id",
                table: "github_onboarding_pull_requests",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_onboarding_pull_requests_organization_id_repo_pull_r~",
                table: "github_onboarding_pull_requests",
                columns: new[] { "organization_id", "repo", "pull_request_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_github_onboarding_pull_requests_organization_id_status_upda~",
                table: "github_onboarding_pull_requests",
                columns: new[] { "organization_id", "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_github_onboarding_pull_requests_project_id",
                table: "github_onboarding_pull_requests",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_onboarding_pull_requests_repo_connection_id",
                table: "github_onboarding_pull_requests",
                column: "repo_connection_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_repo_connections_created_by_user_id",
                table: "github_repo_connections",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_repo_connections_environment_id",
                table: "github_repo_connections",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_repo_connections_github_installation_id",
                table: "github_repo_connections",
                column: "github_installation_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_repo_connections_live_app_id",
                table: "github_repo_connections",
                column: "live_app_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_repo_connections_organization_id",
                table: "github_repo_connections",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_repo_connections_organization_id_project_id_environm~",
                table: "github_repo_connections",
                columns: new[] { "organization_id", "project_id", "environment_id", "normalized_repo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_github_repo_connections_project_id",
                table: "github_repo_connections",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_workflow_dispatches_control_action_id",
                table: "github_workflow_dispatches",
                column: "control_action_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_github_workflow_dispatches_environment_id",
                table: "github_workflow_dispatches",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_workflow_dispatches_github_run_id",
                table: "github_workflow_dispatches",
                column: "github_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_workflow_dispatches_live_app_id",
                table: "github_workflow_dispatches",
                column: "live_app_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_workflow_dispatches_organization_id",
                table: "github_workflow_dispatches",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_workflow_dispatches_organization_id_completed_at_upd~",
                table: "github_workflow_dispatches",
                columns: new[] { "organization_id", "completed_at", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_github_workflow_dispatches_project_id",
                table: "github_workflow_dispatches",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_workflow_dispatches_repo_connection_id",
                table: "github_workflow_dispatches",
                column: "repo_connection_id");

            migrationBuilder.CreateIndex(
                name: "IX_github_workflow_dispatches_requested_by_user_id",
                table: "github_workflow_dispatches",
                column: "requested_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "github_onboarding_pull_requests");

            migrationBuilder.DropTable(
                name: "github_workflow_dispatches");

            migrationBuilder.DropTable(
                name: "github_repo_connections");

            migrationBuilder.DropTable(
                name: "github_installations");

            migrationBuilder.DropIndex(
                name: "IX_live_app_deployments_github_run_id",
                table: "live_app_deployments");

            migrationBuilder.DropColumn(
                name: "github_run_id",
                table: "live_apps");

            migrationBuilder.DropColumn(
                name: "github_run_url",
                table: "live_apps");

            migrationBuilder.DropColumn(
                name: "github_run_id",
                table: "live_app_deployments");

            migrationBuilder.DropColumn(
                name: "github_run_url",
                table: "live_app_deployments");
        }
    }
}
