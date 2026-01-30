using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaDAM.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddFileChecksumToAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileChecksum",
                table: "Assets",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_FileChecksum",
                table: "Assets",
                column: "FileChecksum");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assets_FileChecksum",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FileChecksum",
                table: "Assets");
        }
    }
}
