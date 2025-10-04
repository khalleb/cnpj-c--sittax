using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sittax.Cnpj.Data.Models
{
    [Table("rf_motivos")]
    public class ReceitaFederalMotivos : ReceitaFederalEntityBase
    {
        [Column("codigo")]
        [MaxLength(10)]
        public string Codigo { get; set; } = string.Empty;

        [Column("descricao")]
        [MaxLength(500)]
        public string? Descricao { get; set; }
    }
}
