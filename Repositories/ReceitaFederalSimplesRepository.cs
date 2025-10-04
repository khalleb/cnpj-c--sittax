using Sittax.Cnpj.Data;
using Sittax.Cnpj.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Sittax.Cnpj.Repositories
{
    public interface IReceitaFederalSimplesRepository
    {
        Task AdicionarLoteAsync(IEnumerable<ReceitaFederalSimples> simples);
        Task<int> ContarAsync();
        Task LimparTabelaAsync();
    }
    
    public class ReceitaFederalSimplesRepository : IReceitaFederalSimplesRepository
    {
        private readonly SittaxCnpjDbContext _context;

        public ReceitaFederalSimplesRepository(SittaxCnpjDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarLoteAsync(IEnumerable<ReceitaFederalSimples> simples)
        {
            await _context.ReceitaFederalSimples.AddRangeAsync(simples);
            await _context.SaveChangesAsync();
        }

        public async Task<int> ContarAsync()
        {
            return await _context.ReceitaFederalSimples.CountAsync();
        }

        public async Task LimparTabelaAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE rf_simples RESTART IDENTITY CASCADE");
        }
    }
}
