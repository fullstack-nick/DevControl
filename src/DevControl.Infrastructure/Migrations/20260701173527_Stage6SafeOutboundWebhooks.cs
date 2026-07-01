using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Stage6SafeOutboundWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "webhook_endpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    secret_prefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    protected_secret = table.Column<string>(type: "text", nullable: false),
                    event_types_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_paused = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paused_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paused_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_delivery_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_success_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_failure_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_endpoints", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhook_endpoints_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_endpoints_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_endpoints_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_endpoints_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_endpoints_users_paused_by_user_id",
                        column: x => x.paused_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "webhook_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    resource_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhook_events_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_events_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_events_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_endpoint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_status_code = table.Column<int>(type: "integer", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    last_response_preview = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: false),
                    last_response_truncated = table.Column<bool>(type: "boolean", nullable: false),
                    processing_lease_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    processing_lease_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_webhook_endpoints_webhook_endpoint_id",
                        column: x => x.webhook_endpoint_id,
                        principalTable: "webhook_endpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_webhook_events_webhook_event_id",
                        column: x => x.webhook_event_id,
                        principalTable: "webhook_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "webhook_delivery_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_endpoint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    result_kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    duration_milliseconds = table.Column<long>(type: "bigint", nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    response_preview = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: false),
                    response_truncated = table.Column<bool>(type: "boolean", nullable: false),
                    response_bytes_read = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_delivery_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhook_delivery_attempts_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_delivery_attempts_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_delivery_attempts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_delivery_attempts_webhook_deliveries_webhook_delive~",
                        column: x => x.webhook_delivery_id,
                        principalTable: "webhook_deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_delivery_attempts_webhook_endpoints_webhook_endpoin~",
                        column: x => x.webhook_endpoint_id,
                        principalTable: "webhook_endpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_delivery_attempts_webhook_events_webhook_event_id",
                        column: x => x.webhook_event_id,
                        principalTable: "webhook_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_environment_id",
                table: "webhook_deliveries",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_organization_id",
                table: "webhook_deliveries",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_organization_id_created_at",
                table: "webhook_deliveries",
                columns: new[] { "organization_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_project_id",
                table: "webhook_deliveries",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_status_next_attempt_at",
                table: "webhook_deliveries",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_webhook_endpoint_id",
                table: "webhook_deliveries",
                column: "webhook_endpoint_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_webhook_event_id",
                table: "webhook_deliveries",
                column: "webhook_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_attempts_environment_id",
                table: "webhook_delivery_attempts",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_attempts_organization_id",
                table: "webhook_delivery_attempts",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_attempts_organization_id_created_at",
                table: "webhook_delivery_attempts",
                columns: new[] { "organization_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_attempts_project_id",
                table: "webhook_delivery_attempts",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_attempts_webhook_delivery_id_attempt_number",
                table: "webhook_delivery_attempts",
                columns: new[] { "webhook_delivery_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_attempts_webhook_endpoint_id",
                table: "webhook_delivery_attempts",
                column: "webhook_endpoint_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_attempts_webhook_event_id",
                table: "webhook_delivery_attempts",
                column: "webhook_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_endpoints_created_by_user_id",
                table: "webhook_endpoints",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_endpoints_environment_id",
                table: "webhook_endpoints",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_endpoints_organization_id",
                table: "webhook_endpoints",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_endpoints_organization_id_project_id_environment_id~",
                table: "webhook_endpoints",
                columns: new[] { "organization_id", "project_id", "environment_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_endpoints_paused_by_user_id",
                table: "webhook_endpoints",
                column: "paused_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_endpoints_project_id",
                table: "webhook_endpoints",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_actor_user_id",
                table: "webhook_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_environment_id_event_type_occurred_at",
                table: "webhook_events",
                columns: new[] { "environment_id", "event_type", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_organization_id_occurred_at",
                table: "webhook_events",
                columns: new[] { "organization_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_project_id",
                table: "webhook_events",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "webhook_delivery_attempts");

            migrationBuilder.DropTable(
                name: "webhook_deliveries");

            migrationBuilder.DropTable(
                name: "webhook_endpoints");

            migrationBuilder.DropTable(
                name: "webhook_events");
        }
    }
}
