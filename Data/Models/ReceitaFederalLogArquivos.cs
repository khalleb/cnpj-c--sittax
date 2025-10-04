using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Sittax.Cnpj.Data.Models;

namespace Sittax.Cnpj.Models
{
    [Table("rf_log_arquivos")]
    public class ReceitaFederalLogArquivos : ReceitaFederalEntityBase
    {
        [Column("nome_arquivo_zip")]
        [MaxLength(255)]
        public string NomeArquivoZip { get; set; } = string.Empty;

        [Column("nome_arquivo_csv")]
        [MaxLength(255)]
        public string? NomeArquivoCsv { get; set; }

        [Column("hash_sha256_zip")]
        [MaxLength(64)]
        public string HashSha256Zip { get; set; } = string.Empty;

        [Column("hash_sha256_csv")]
        [MaxLength(64)]
        public string HashSha256Csv { get; set; } = string.Empty;

        [Column("tamanho_zip")]
        public long TamanhoZip { get; set; }

        [Column("tamanho_csv")]
        public long? TamanhoCsv { get; set; }

        [Column("status_download")]
        public StatusDownload StatusDownload { get; set; }

        [Column("status_csv")]
        public StatusCsv StatusCsv { get; set; }

        [Column("data_download")]
        public DateTime DataDownload { get; set; }

        [Column("periodo")]
        [MaxLength(7)] // YYYY-MM
        public string Periodo { get; set; } = string.Empty;

        [Column("ultima_verificacao")]
        public DateTime UltimaVerificacao { get; set; }
    }
}

public enum StatusDownload
{
    NaoIniciado = 0,
    EmProgresso = 1,
    Finalizado = 2,
    Erro = 3
}

public enum StatusCsv
{
    NaoProcessado = 0,
    Extraindo = 1,
    Extraido = 2,
    Processando = 3,
    Processado = 4,
    Erro = 5
}
