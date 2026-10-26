using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garbati.OpsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerEditApplicationError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "application_error",
                table: "ops_customer_edit_requests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "application_error",
                table: "ops_customer_edit_requests");
        }
    }
}
