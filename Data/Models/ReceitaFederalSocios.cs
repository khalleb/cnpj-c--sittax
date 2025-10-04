using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sittax.Cnpj.Data.Models
{
    [Table("rf_socios")]
    public class ReceitaFederalSocios : ReceitaFederalEntityBase
    {
        [Column("cnpj_basico")]
        [MaxLength(8)]
        public string CnpjBasico { get; set; } = string.Empty;

        [Column("identificador_socio")]
        [MaxLength(1)]
        public string IdentificadorSocio { get; set; } = string.Empty;

        [Column("nome_socio_razao_social")]
        [MaxLength(500)]
        public string? NomeSocioRazaoSocial { get; set; }

        [Column("cpf_cnpj_socio")]
        [MaxLength(14)]
        public string? CpfCnpjSocio { get; set; }

        [Column("qualificacao_socio")]
        [MaxLength(2)]
        public string? QualificacaoSocio { get; set; }

        [Column("data_entrada_sociedade")]
        public DateTime? DataEntradaSociedade { get; set; }

        [Column("pais")]
        [MaxLength(3)]
        public string? Pais { get; set; }

        [Column("representante_legal")]
        [MaxLength(11)]
        public string? RepresentanteLegal { get; set; }

        [Column("nome_representante")]
        [MaxLength(500)]
        public string? NomeRepresentante { get; set; }

        [Column("qualificacao_representante_legal")]
        [MaxLength(2)]
        public string? QualificacaoRepresentanteLegal { get; set; }

        [Column("faixa_etaria")]
        [MaxLength(1)]
        public string? FaixaEtaria { get; set; }
    }
}
