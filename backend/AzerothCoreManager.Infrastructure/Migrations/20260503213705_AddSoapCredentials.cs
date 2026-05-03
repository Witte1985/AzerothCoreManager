using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzerothCoreManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoapCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SoapPassword",
                table: "ManagedStacks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SoapUsername",
                table: "ManagedStacks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoapPassword",
                table: "ManagedStacks");

            migrationBuilder.DropColumn(
                name: "SoapUsername",
                table: "ManagedStacks");
        }
    }
}
