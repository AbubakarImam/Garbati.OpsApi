using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garbati.OpsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddReversalAndCustomerEditRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ops_customer_edit_requests",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    target_customer_id = table.Column<string>(type: "text", nullable: false),
                    proposed_full_name = table.Column<string>(type: "text", nullable: true),
                    proposed_email = table.Column<string>(type: "text", nullable: true),
                    proposed_phone_number = table.Column<string>(type: "text", nullable: true),
                    requested_by_staff_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    approved_by_staff_id = table.Column<string>(type: "text", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ops_customer_edit_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ops_reversal_requests",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    original_transaction_reference = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    reason_code = table.Column<string>(type: "text", nullable: false),
                    requested_by_staff_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    approved_by_staff_id = table.Column<string>(type: "text", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    executed_reference = table.Column<string>(type: "text", nullable: true),
                    execution_detail = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ops_reversal_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ops_customer_edit_requests_requested_by_staff_id",
                table: "ops_customer_edit_requests",
                column: "requested_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_ops_customer_edit_requests_status",
                table: "ops_customer_edit_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_ops_reversal_requests_approved_by_staff_id",
                table: "ops_reversal_requests",
                column: "approved_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_ops_reversal_requests_requested_by_staff_id_idempotency_key",
                table: "ops_reversal_requests",
                columns: new[] { "requested_by_staff_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ops_reversal_requests_status",
                table: "ops_reversal_requests",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ops_customer_edit_requests");

            migrationBuilder.DropTable(
                name: "ops_reversal_requests");
        }
    }
}
