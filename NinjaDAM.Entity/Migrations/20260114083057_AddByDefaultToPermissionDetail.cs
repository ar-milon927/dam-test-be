using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaDAM.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddByDefaultToPermissionDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ByDefault",
                table: "PermissionDetails",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ByDefault",
                table: "PermissionDetails");
        }
    }
}
