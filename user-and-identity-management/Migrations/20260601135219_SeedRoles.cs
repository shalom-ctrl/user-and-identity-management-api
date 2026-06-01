using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace user_and_identity_management.Migrations
{
    /// <inheritdoc />
    public partial class SeedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "40d74dfe-cdf2-4a28-b57f-c01f7dc49210", "2", "User", "User" },
                    { "8bfc9bec-0e83-4d99-a05e-a5f1aa5ae8d3", "1", "Admin", "Admin" },
                    { "eb0112e1-317b-4cd2-b2c7-24b2c2112b67", "3", "HR", "HR" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "40d74dfe-cdf2-4a28-b57f-c01f7dc49210");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8bfc9bec-0e83-4d99-a05e-a5f1aa5ae8d3");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "eb0112e1-317b-4cd2-b2c7-24b2c2112b67");
        }
    }
}
