using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sittax.Cnpj.Data.Models
{
    [Table("rf_simples")]
    public class ReceitaFederalSimples : ReceitaFederalEntityBase
    {
        [Column("cnpj_basico")]
        [MaxLength(8)]
        public string CnpjBasico { get; set; } = string.Empty;

        [Column("opcao_pelo_simples")]
        [MaxLength(1)]
        public string? OpcaoPeloSimples { get; set; }

        [Column("data_opcao_simples")]
        public DateTime? DataOpcaoSimples { get; set; }

        [Column("data_exclusao_simples")]
        public DateTime? DataExclusaoSimples { get; set; }

        [Column("opcao_pelo_mei")]
        [MaxLength(1)]
        public string? OpcaoPeloMei { get; set; }

        [Column("data_opcao_mei")]
        public DateTime? DataOpcaoMei { get; set; }

        [Column("data_exclusao_mei")]
        public DateTime? DataExclusaoMei { get; set; }
    }
}
