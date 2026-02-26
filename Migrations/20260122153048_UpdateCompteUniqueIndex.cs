using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dadaApp.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCompteUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comptes_numero_compte",
                table: "Comptes");

            migrationBuilder.CreateIndex(
                name: "IX_Comptes_numero_compte_code_client",
                table: "Comptes",
                columns: new[] { "numero_compte", "code_client" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comptes_numero_compte_code_client",
                table: "Comptes");

            migrationBuilder.CreateIndex(
                name: "IX_Comptes_numero_compte",
                table: "Comptes",
                column: "numero_compte",
                unique: true);
        }
    }
}
