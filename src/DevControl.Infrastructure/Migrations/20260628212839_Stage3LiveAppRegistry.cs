using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Stage3LiveAppRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repo = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    normalized_repo = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    service_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    health_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    current_commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    image_digest = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    capabilities_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_apps", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_apps_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_apps_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_apps_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registration_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    token_prefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scope = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_registration_tokens_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_tokens_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_tokens_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_tokens_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_tokens_users_revoked_by_user_id",
                        column: x => x.revoked_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_app_deployments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_app_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repo = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    service_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    health_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    image_digest = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    capabilities_json = table.Column<string>(type: "jsonb", nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_app_deployments", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_app_deployments_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_app_deployments_live_apps_live_app_id",
                        column: x => x.live_app_id,
                        principalTable: "live_apps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_app_deployments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_live_app_deployments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_app_deployments_environment_id",
                table: "live_app_deployments",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_live_app_deployments_live_app_id_registered_at",
                table: "live_app_deployments",
                columns: new[] { "live_app_id", "registered_at" });

            migrationBuilder.CreateIndex(
                name: "IX_live_app_deployments_organization_id_registered_at",
                table: "live_app_deployments",
                columns: new[] { "organization_id", "registered_at" });

            migrationBuilder.CreateIndex(
                name: "IX_live_app_deployments_project_id",
                table: "live_app_deployments",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_live_apps_environment_id",
                table: "live_apps",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_live_apps_organization_id",
                table: "live_apps",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_live_apps_organization_id_project_id_environment_id_normali~",
                table: "live_apps",
                columns: new[] { "organization_id", "project_id", "environment_id", "normalized_repo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_apps_project_id",
                table: "live_apps",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_registration_tokens_created_by_user_id",
                table: "registration_tokens",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_registration_tokens_environment_id",
                table: "registration_tokens",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_registration_tokens_organization_id",
                table: "registration_tokens",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_registration_tokens_organization_id_project_id_environment_~",
                table: "registration_tokens",
                columns: new[] { "organization_id", "project_id", "environment_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_registration_tokens_project_id",
                table: "registration_tokens",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_registration_tokens_revoked_by_user_id",
                table: "registration_tokens",
                column: "revoked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_registration_tokens_token_hash",
                table: "registration_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_app_deployments");

            migrationBuilder.DropTable(
                name: "registration_tokens");

            migrationBuilder.DropTable(
                name: "live_apps");
        }
    }
}
