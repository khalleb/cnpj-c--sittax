using Microsoft.EntityFrameworkCore;

using Sittax.Cnpj.Data;
using Sittax.Cnpj.Data.Models;

namespace Sittax.Cnpj.Repositories
{
    
    public interface IReceitaFederalPaisesRepository
    {
        Task AdicionarLoteAsync(IEnumerable<ReceitaFederalPaises> paises);
        Task<int> ContarAsync();
        Task LimparTabelaAsync();
    }
    
    public class ReceitaFederalPaisesRepository : IReceitaFederalPaisesRepository
    {
        private readonly SittaxCnpjDbContext _context;

        public ReceitaFederalPaisesRepository(SittaxCnpjDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarLoteAsync(IEnumerable<ReceitaFederalPaises> paises)
        {
            await _context.ReceitaFederalPaises.AddRangeAsync(paises);
            await _context.SaveChangesAsync();
        }

        public async Task<int> ContarAsync()
        {
            return await _context.ReceitaFederalPaises.CountAsync();
        }

        public async Task LimparTabelaAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE rf_paises RESTART IDENTITY CASCADE");
        }
    }
}
