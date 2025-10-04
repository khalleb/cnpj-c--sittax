using Sittax.Cnpj.Data;
using Sittax.Cnpj.Models;
using Microsoft.EntityFrameworkCore;

namespace Sittax.Cnpj.Repositories
{
    public interface IReceitaFederalLogArquivosRepository
    {
        Task<ReceitaFederalLogArquivos?> ObterPorNomeEPeriodoAsync(string nomeArquivo, string periodo);
        Task<ReceitaFederalLogArquivos> CriarAsync(ReceitaFederalLogArquivos logArquivo);
        Task<ReceitaFederalLogArquivos> AtualizarAsync(ReceitaFederalLogArquivos logArquivo);
        Task<List<ReceitaFederalLogArquivos>> ListarPorPeriodoAsync(string periodo);
        Task<bool> ExisteAsync(string nomeArquivo, string periodo);
        
        Task<List<ReceitaFederalLogArquivos>> ListarPorStatusCsvAsync(StatusCsv status);
        Task<ReceitaFederalLogArquivos?> ObterPorNomeArquivoCsvAsync(string nomeArquivoCsv, string periodo);
    }

    public class ReceitaFederalLogArquivosRepository : IReceitaFederalLogArquivosRepository
    {
        private readonly SittaxCnpjDbContext _context;

        public ReceitaFederalLogArquivosRepository(SittaxCnpjDbContext context)
        {
            _context = context;
        }

        public async Task<ReceitaFederalLogArquivos?> ObterPorNomeEPeriodoAsync(string nomeArquivo, string periodo)
        {
            return await _context.ReceitaFederalLogArquivos.FirstOrDefaultAsync(x => x.NomeArquivoZip == nomeArquivo && x.Periodo == periodo);
        }

        public async Task<ReceitaFederalLogArquivos> CriarAsync(ReceitaFederalLogArquivos logArquivo)
        {
            _context.ReceitaFederalLogArquivos.Add(logArquivo);
            await _context.SaveChangesAsync();
            return logArquivo;
        }

        public async Task<ReceitaFederalLogArquivos> AtualizarAsync(ReceitaFederalLogArquivos logArquivo)
        {
            _context.ReceitaFederalLogArquivos.Update(logArquivo);
            await _context.SaveChangesAsync();
            return logArquivo;
        }

        public async Task<List<ReceitaFederalLogArquivos>> ListarPorPeriodoAsync(string periodo)
        {
            return await _context.ReceitaFederalLogArquivos.Where(x => x.Periodo == periodo).OrderBy(x => x.NomeArquivoZip).ToListAsync();
        }

        public async Task<bool> ExisteAsync(string nomeArquivo, string periodo)
        {
            return await _context.ReceitaFederalLogArquivos.AnyAsync(x => x.NomeArquivoZip == nomeArquivo && x.Periodo == periodo);
        }
        
        public async Task<List<ReceitaFederalLogArquivos>> ListarPorStatusCsvAsync(StatusCsv status)
        {
            return await _context.ReceitaFederalLogArquivos
                .Where(x => x.StatusCsv == status)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<ReceitaFederalLogArquivos?> ObterPorNomeArquivoCsvAsync(string nomeArquivoCsv, string periodo)
        {
            return await _context.ReceitaFederalLogArquivos
                .FirstOrDefaultAsync(x => x.NomeArquivoCsv == nomeArquivoCsv && x.Periodo == periodo);
        }
    }
}
