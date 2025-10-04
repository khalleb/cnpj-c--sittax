using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sittax.Cnpj.Migrations
{
    /// <inheritdoc />
    public partial class NovasTabelasReceitaFederal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rf_cnaes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rf_cnaes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rf_empresas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cnpj_basico = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    razao_social_nome_empresarial = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    natureza_juridica = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    qualificacao_responsavel = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    capital_social_empresa = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    porte_empresa = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ente_federativo_responsavel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rf_empresas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rf_estabelecimentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cnpj_completo = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    cnpj_basico = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    cnpj_ordem = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    cnpj_dv = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    identificador_matriz_filial = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    nome_fantasia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    situacao_cadastral = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    data_situacao_cadastral = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_situacao_cadastral = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    nome_cidade_exterior = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pais = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    data_inicio_atividade = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cnae_fiscal_principal = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    cnae_fiscal_secundaria = table.Column<string>(type: "text", nullable: true),
                    tipo_logradouro = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    complemento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    municipio = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    ddd_1 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    telefone_1 = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    ddd_2 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    telefone_2 = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    ddd_fax = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    fax = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    correio_eletronico = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    situacao_especial = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    data_situacao_especial = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rf_estabelecimentos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rf_motivos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rf_motivos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rf_municipios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rf_municipios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rf_naturezas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rf_naturezas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rf_paises",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rf_paises", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rf_qualificacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rf_qualificacoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rf_simples",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cnpj_basico = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    opcao_pelo_simples = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    data_opcao_simples = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_exclusao_simples = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    opcao_pelo_mei = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    data_opcao_mei = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_exclusao_mei = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rf_simples", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rf_socios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cnpj_basico = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    identificador_socio = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    nome_socio_razao_social = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cpf_cnpj_socio = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    qualificacao_socio = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    data_entrada_sociedade = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pais = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    representante_legal = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    nome_representante = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    qualificacao_representante_legal = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    faixa_etaria = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rf_socios", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rf_cnaes");

            migrationBuilder.DropTable(
                name: "rf_empresas");

            migrationBuilder.DropTable(
                name: "rf_estabelecimentos");

            migrationBuilder.DropTable(
                name: "rf_motivos");

            migrationBuilder.DropTable(
                name: "rf_municipios");

            migrationBuilder.DropTable(
                name: "rf_naturezas");

            migrationBuilder.DropTable(
                name: "rf_paises");

            migrationBuilder.DropTable(
                name: "rf_qualificacoes");

            migrationBuilder.DropTable(
                name: "rf_simples");

            migrationBuilder.DropTable(
                name: "rf_socios");
        }
    }
}
