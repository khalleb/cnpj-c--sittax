using Microsoft.EntityFrameworkCore;

using Sittax.Cnpj.Data;
using Sittax.Cnpj.Data.Models;

namespace Sittax.Cnpj.Repositories
{
    public interface IReceitaFederalCnaesRepository
    {
        Task AdicionarLoteAsync(IEnumerable<ReceitaFederalCnaes> empresas);
        Task<int> ContarAsync();
        Task LimparTabelaAsync();
    }
    
    public class ReceitaFederalCnaesRepository : IReceitaFederalCnaesRepository
    {
        private readonly SittaxCnpjDbContext _context;

        public ReceitaFederalCnaesRepository(SittaxCnpjDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarLoteAsync(IEnumerable<ReceitaFederalCnaes> cnaes)
        {
            await _context.ReceitaFederalCnaes.AddRangeAsync(cnaes);
            await _context.SaveChangesAsync();
        }

        public async Task<int> ContarAsync()
        {
            return await _context.ReceitaFederalCnaes.CountAsync();
        }

        public async Task LimparTabelaAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE rf_cnaes RESTART IDENTITY CASCADE");
        }
    }
}
