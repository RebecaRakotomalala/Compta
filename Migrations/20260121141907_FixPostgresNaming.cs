using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace dadaApp.Migrations
{
    /// <inheritdoc />
    public partial class FixPostgresNaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comptes",
                columns: table => new
                {
                    compte_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_compte = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    code_client = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    nom_client = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    date_creation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comptes", x => x.compte_id);
                });

            migrationBuilder.CreateTable(
                name: "VueSoldesComptes",
                columns: table => new
                {
                    CompteId = table.Column<int>(type: "integer", nullable: false),
                    NumeroCompte = table.Column<string>(type: "text", nullable: false),
                    NomClient = table.Column<string>(type: "text", nullable: false),
                    TotalDebit = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalCredit = table.Column<decimal>(type: "numeric", nullable: true),
                    Solde = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Ecritures",
                columns: table => new
                {
                    ecriture_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    compte_id = table.Column<int>(type: "integer", nullable: false),
                    date_comptable = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    numero_piece = table.Column<string>(type: "text", nullable: true),
                    code_journal = table.Column<string>(type: "text", nullable: true),
                    libelle = table.Column<string>(type: "text", nullable: true),
                    date_facture = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reference = table.Column<string>(type: "text", nullable: true),
                    date_echeance = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    debit = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    credit = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    numero_lettrage = table.Column<string>(type: "text", nullable: true),
                    date_saisie = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ecritures", x => x.ecriture_id);
                    table.ForeignKey(
                        name: "FK_Ecritures_Comptes_compte_id",
                        column: x => x.compte_id,
                        principalTable: "Comptes",
                        principalColumn: "compte_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comptes_numero_compte",
                table: "Comptes",
                column: "numero_compte",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ecritures_compte_id",
                table: "Ecritures",
                column: "compte_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ecritures");

            migrationBuilder.DropTable(
                name: "VueSoldesComptes");

            migrationBuilder.DropTable(
                name: "Comptes");
        }
    }
}
