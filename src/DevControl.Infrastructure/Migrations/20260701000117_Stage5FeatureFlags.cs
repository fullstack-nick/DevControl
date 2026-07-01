using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Stage5FeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feature_flags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_flags", x => x.id);
                    table.ForeignKey(
                        name: "FK_feature_flags_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feature_flags_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feature_flags_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feature_flags_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feature_flags_users_last_changed_by_user_id",
                        column: x => x.last_changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "feature_flag_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_flag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_value = table.Column<bool>(type: "boolean", nullable: false),
                    new_value = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_flag_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_feature_flag_changes_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feature_flag_changes_feature_flags_feature_flag_id",
                        column: x => x.feature_flag_id,
                        principalTable: "feature_flags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feature_flag_changes_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feature_flag_changes_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feature_flag_changes_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feature_flag_changes_changed_by_user_id",
                table: "feature_flag_changes",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_flag_changes_environment_id_changed_at",
                table: "feature_flag_changes",
                columns: new[] { "environment_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_feature_flag_changes_feature_flag_id",
                table: "feature_flag_changes",
                column: "feature_flag_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_flag_changes_organization_id_changed_at",
                table: "feature_flag_changes",
                columns: new[] { "organization_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_feature_flag_changes_project_id",
                table: "feature_flag_changes",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_created_by_user_id",
                table: "feature_flags",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_environment_id",
                table: "feature_flags",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_environment_id_kind",
                table: "feature_flags",
                columns: new[] { "environment_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_last_changed_by_user_id",
                table: "feature_flags",
                column: "last_changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_organization_id",
                table: "feature_flags",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_organization_id_project_id_environment_id_key",
                table: "feature_flags",
                columns: new[] { "organization_id", "project_id", "environment_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_project_id",
                table: "feature_flags",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_flag_changes");

            migrationBuilder.DropTable(
                name: "feature_flags");
        }
    }
}
