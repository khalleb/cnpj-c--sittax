using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sittax.Cnpj.Migrations
{
    /// <inheritdoc />
    public partial class CreateReceitaFederalLogArquivosTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rf_log_arquivos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo_zip = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    nome_arquivo_csv = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    hash_sha256_zip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    hash_sha256_csv = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tamanho_zip = table.Column<long>(type: "bigint", nullable: false),
                    tamanho_csv = table.Column<long>(type: "bigint", nullable: true),
                    status_download = table.Column<int>(type: "integer", nullable: false),
                    status_csv = table.Column<int>(type: "integer", nullable: false),
                    data_download = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    periodo = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    ultima_verificacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rf_log_arquivos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rf_log_arquivos_nome_arquivo_zip",
                table: "rf_log_arquivos",
                column: "nome_arquivo_zip");

            migrationBuilder.CreateIndex(
                name: "IX_rf_log_arquivos_nome_arquivo_zip_periodo",
                table: "rf_log_arquivos",
                columns: new[] { "nome_arquivo_zip", "periodo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rf_log_arquivos_periodo",
                table: "rf_log_arquivos",
                column: "periodo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rf_log_arquivos");
        }
    }
}
