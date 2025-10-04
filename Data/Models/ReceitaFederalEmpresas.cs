using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace Sittax.Cnpj.Data.Models
{
    [Table("rf_empresas")]
    public class ReceitaFederalEmpresas : ReceitaFederalEntityBase
    {
        [Column("cnpj_basico")]
        [MaxLength(8)]
        public string CnpjBasico { get; set; } = string.Empty;

        [Column("razao_social_nome_empresarial")]
        [MaxLength(500)]
        public string? RazaoSocialNomeEmpresarial { get; set; }

        [Column("natureza_juridica")]
        [MaxLength(4)]
        public string? NaturezaJuridica { get; set; }

        [Column("qualificacao_responsavel")]
        [MaxLength(2)]
        public string? QualificacaoResponsavel { get; set; }

        [Column("capital_social_empresa")]
        [Precision(14, 2)]
        public decimal? CapitalSocialEmpresa { get; set; }

        [Column("porte_empresa")]
        [MaxLength(2)]
        public string? PorteEmpresa { get; set; }

        [Column("ente_federativo_responsavel")]
        [MaxLength(200)]
        public string? EnteFederativoResponsavel { get; set; }
    }
}
