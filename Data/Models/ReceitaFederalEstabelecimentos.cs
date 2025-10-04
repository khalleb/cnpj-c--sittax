using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sittax.Cnpj.Data.Models
{
    [Table("rf_estabelecimentos")]
    public class ReceitaFederalEstabelecimentos : ReceitaFederalEntityBase
    {
        [Column("cnpj_completo")]
        [MaxLength(14)]
        public string CnpjCompleto { get; set; } = string.Empty;

        [Column("cnpj_basico")]
        [MaxLength(8)]
        public string CnpjBasico { get; set; } = string.Empty;

        [Column("cnpj_ordem")]
        [MaxLength(4)]
        public string CnpjOrdem { get; set; } = string.Empty;

        [Column("cnpj_dv")]
        [MaxLength(2)]
        public string CnpjDv { get; set; } = string.Empty;

        [Column("identificador_matriz_filial")]
        [MaxLength(1)]
        public string IdentificadorMatrizFilial { get; set; } = string.Empty;

        [Column("nome_fantasia")]
        [MaxLength(200)]
        public string? NomeFantasia { get; set; }

        [Column("situacao_cadastral")]
        [MaxLength(2)]
        public string SituacaoCadastral { get; set; } = string.Empty;

        [Column("data_situacao_cadastral")]
        public DateTime? DataSituacaoCadastral { get; set; }

        [Column("motivo_situacao_cadastral")]
        [MaxLength(2)]
        public string? MotivoSituacaoCadastral { get; set; }

        [Column("nome_cidade_exterior")]
        [MaxLength(200)]
        public string? NomeCidadeExterior { get; set; }

        [Column("pais")]
        [MaxLength(5)]
        public string? Pais { get; set; }

        [Column("data_inicio_atividade")]
        public DateTime? DataInicioAtividade { get; set; }

        [Column("cnae_fiscal_principal")]
        [MaxLength(7)]
        public string CnaeFiscalPrincipal { get; set; } = string.Empty;

        [Column("cnae_fiscal_secundaria")]
        public string? CnaeFiscalSecundaria { get; set; }

        [Column("tipo_logradouro")]
        [MaxLength(50)]
        public string? TipoLogradouro { get; set; }

        [Column("logradouro")]
        [MaxLength(200)]
        public string? Logradouro { get; set; }

        [Column("numero")]
        [MaxLength(20)]
        public string? Numero { get; set; }

        [Column("complemento")]
        [MaxLength(200)]
        public string? Complemento { get; set; }

        [Column("bairro")]
        [MaxLength(100)]
        public string? Bairro { get; set; }

        [Column("cep")]
        [MaxLength(8)]
        public string? Cep { get; set; }

        [Column("uf")]
        [MaxLength(2)]
        public string? Uf { get; set; }

        [Column("municipio")]
        [MaxLength(4)]
        public string? Municipio { get; set; }

        [Column("ddd_1")]
        [MaxLength(4)]
        public string? Ddd1 { get; set; }

        [Column("telefone_1")]
        [MaxLength(15)]
        public string? Telefone1 { get; set; }

        [Column("ddd_2")]
        [MaxLength(4)]
        public string? Ddd2 { get; set; }

        [Column("telefone_2")]
        [MaxLength(15)]
        public string? Telefone2 { get; set; }

        [Column("ddd_fax")]
        [MaxLength(4)]
        public string? DddFax { get; set; }

        [Column("fax")]
        [MaxLength(15)]
        public string? Fax { get; set; }

        [Column("correio_eletronico")]
        [MaxLength(200)]
        public string? CorreioEletronico { get; set; }

        [Column("situacao_especial")]
        [MaxLength(100)]
        public string? SituacaoEspecial { get; set; }

        [Column("data_situacao_especial")]
        public DateTime? DataSituacaoEspecial { get; set; }
    }
}
