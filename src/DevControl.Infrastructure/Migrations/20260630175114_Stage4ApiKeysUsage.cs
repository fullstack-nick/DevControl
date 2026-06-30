using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Stage4ApiKeysUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    key_prefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scopes_json = table.Column<string>(type: "jsonb", nullable: false),
                    rate_limit_per_minute = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rotated_from_api_key_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rotated_to_api_key_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rotated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_request_count = table.Column<long>(type: "bigint", nullable: false),
                    failure_count = table.Column<long>(type: "bigint", nullable: false),
                    rate_limit_hit_count = table.Column<long>(type: "bigint", nullable: false),
                    total_latency_milliseconds = table.Column<long>(type: "bigint", nullable: false),
                    latency_sample_count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_api_keys_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_api_keys_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_api_keys_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_api_keys_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_api_keys_users_revoked_by_user_id",
                        column: x => x.revoked_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "api_key_rate_limit_windows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    window_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    request_count = table.Column<int>(type: "integer", nullable: false),
                    rate_limit_hit_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_key_rate_limit_windows", x => x.id);
                    table.ForeignKey(
                        name: "FK_api_key_rate_limit_windows_api_keys_api_key_id",
                        column: x => x.api_key_id,
                        principalTable: "api_keys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "api_key_usage_daily",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    request_count = table.Column<long>(type: "bigint", nullable: false),
                    failure_count = table.Column<long>(type: "bigint", nullable: false),
                    rate_limit_hit_count = table.Column<long>(type: "bigint", nullable: false),
                    total_latency_milliseconds = table.Column<long>(type: "bigint", nullable: false),
                    latency_sample_count = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_key_usage_daily", x => x.id);
                    table.ForeignKey(
                        name: "FK_api_key_usage_daily_api_keys_api_key_id",
                        column: x => x.api_key_id,
                        principalTable: "api_keys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_api_key_usage_daily_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_api_key_usage_daily_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_api_key_usage_daily_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_key_rate_limit_windows_api_key_id",
                table: "api_key_rate_limit_windows",
                column: "api_key_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_key_rate_limit_windows_api_key_id_endpoint_window_start",
                table: "api_key_rate_limit_windows",
                columns: new[] { "api_key_id", "endpoint", "window_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_key_usage_daily_api_key_id",
                table: "api_key_usage_daily",
                column: "api_key_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_key_usage_daily_api_key_id_endpoint_day",
                table: "api_key_usage_daily",
                columns: new[] { "api_key_id", "endpoint", "day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_key_usage_daily_environment_id",
                table: "api_key_usage_daily",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_key_usage_daily_organization_id_day",
                table: "api_key_usage_daily",
                columns: new[] { "organization_id", "day" });

            migrationBuilder.CreateIndex(
                name: "IX_api_key_usage_daily_project_id",
                table: "api_key_usage_daily",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_created_by_user_id",
                table: "api_keys",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_environment_id",
                table: "api_keys",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_key_hash",
                table: "api_keys",
                column: "key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_organization_id",
                table: "api_keys",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_organization_id_project_id_environment_id_created_~",
                table: "api_keys",
                columns: new[] { "organization_id", "project_id", "environment_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_project_id",
                table: "api_keys",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_revoked_by_user_id",
                table: "api_keys",
                column: "revoked_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_key_rate_limit_windows");

            migrationBuilder.DropTable(
                name: "api_key_usage_daily");

            migrationBuilder.DropTable(
                name: "api_keys");
        }
    }
}
