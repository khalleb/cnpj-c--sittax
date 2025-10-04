using Sittax.Cnpj.Data;
using Sittax.Cnpj.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Sittax.Cnpj.Repositories
{
    
    public interface IReceitaFederalNaturezasRepository
    {
        Task AdicionarLoteAsync(IEnumerable<ReceitaFederalNaturezas> naturezas);
        Task<int> ContarAsync();
        Task LimparTabelaAsync();
    }
    
    public class ReceitaFederalNaturezasRepository : IReceitaFederalNaturezasRepository
    {
        private readonly SittaxCnpjDbContext _context;

        public ReceitaFederalNaturezasRepository(SittaxCnpjDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarLoteAsync(IEnumerable<ReceitaFederalNaturezas> naturezas)
        {
            await _context.ReceitaFederalNaturezas.AddRangeAsync(naturezas);
            await _context.SaveChangesAsync();
        }

        public async Task<int> ContarAsync()
        {
            return await _context.ReceitaFederalNaturezas.CountAsync();
        }

        public async Task LimparTabelaAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE rf_naturezas RESTART IDENTITY CASCADE");
        }
    }
}
