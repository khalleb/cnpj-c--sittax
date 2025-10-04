using Sittax.Cnpj.Data;
using Sittax.Cnpj.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Sittax.Cnpj.Repositories
{
    
    public interface IReceitaFederalQualificacoesRepository
    {
        Task AdicionarLoteAsync(IEnumerable<ReceitaFederalQualificacoes> qualificacoes);
        Task<int> ContarAsync();
        Task LimparTabelaAsync();
    }
    
    public class ReceitaFederalQualificacoesRepository : IReceitaFederalQualificacoesRepository
    {
        private readonly SittaxCnpjDbContext _context;

        public ReceitaFederalQualificacoesRepository(SittaxCnpjDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarLoteAsync(IEnumerable<ReceitaFederalQualificacoes> qualificacoes)
        {
            await _context.ReceitaFederalQualificacoes.AddRangeAsync(qualificacoes);
            await _context.SaveChangesAsync();
        }

        public async Task<int> ContarAsync()
        {
            return await _context.ReceitaFederalQualificacoes.CountAsync();
        }

        public async Task LimparTabelaAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE rf_qualificacoes RESTART IDENTITY CASCADE");
        }
    }
}
