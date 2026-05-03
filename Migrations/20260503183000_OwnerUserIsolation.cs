using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dadaApp.Migrations
{
    /// <inheritdoc />
    public partial class OwnerUserIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comptes_numero_compte_code_client",
                table: "Comptes");

            migrationBuilder.AddColumn<int>(
                name: "owner_user_id",
                table: "Comptes",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                @"INSERT INTO ""Users"" (""Username"", ""Password"")
SELECT '__legacy_owner__', ''
WHERE EXISTS (SELECT 1 FROM ""Comptes"" LIMIT 1)
  AND NOT EXISTS (SELECT 1 FROM ""Users"" LIMIT 1);");

            migrationBuilder.Sql(
                @"UPDATE ""Comptes"" SET owner_user_id = (SELECT MIN(""Id"") FROM ""Users"") WHERE owner_user_id IS NULL");

            migrationBuilder.AlterColumn<int>(
                name: "owner_user_id",
                table: "Comptes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "LettragesManuels",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                @"INSERT INTO ""Users"" (""Username"", ""Password"")
SELECT '__legacy_lettrage_owner__', ''
WHERE EXISTS (SELECT 1 FROM ""LettragesManuels"" LIMIT 1)
  AND NOT EXISTS (SELECT 1 FROM ""Users"" LIMIT 1);");

            migrationBuilder.Sql(
                @"UPDATE ""LettragesManuels"" SET ""OwnerUserId"" = (SELECT MIN(""Id"") FROM ""Users"") WHERE ""OwnerUserId"" IS NULL");

            migrationBuilder.AlterColumn<int>(
                name: "OwnerUserId",
                table: "LettragesManuels",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comptes_owner_user_id",
                table: "Comptes",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_Comptes_OwnerUserId_NumeroCompte_CodeClient",
                table: "Comptes",
                columns: new[] { "owner_user_id", "numero_compte", "code_client" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LettragesManuels_OwnerUserId",
                table: "LettragesManuels",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comptes_Users_owner_user_id",
                table: "Comptes",
                column: "owner_user_id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LettragesManuels_Users_OwnerUserId",
                table: "LettragesManuels",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comptes_Users_owner_user_id",
                table: "Comptes");

            migrationBuilder.DropForeignKey(
                name: "FK_LettragesManuels_Users_OwnerUserId",
                table: "LettragesManuels");

            migrationBuilder.DropIndex(
                name: "IX_Comptes_owner_user_id",
                table: "Comptes");

            migrationBuilder.DropIndex(
                name: "IX_Comptes_OwnerUserId_NumeroCompte_CodeClient",
                table: "Comptes");

            migrationBuilder.DropIndex(
                name: "IX_LettragesManuels_OwnerUserId",
                table: "LettragesManuels");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "Comptes");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "LettragesManuels");

            migrationBuilder.CreateIndex(
                name: "IX_Comptes_numero_compte_code_client",
                table: "Comptes",
                columns: new[] { "numero_compte", "code_client" },
                unique: true);
        }
    }
}
