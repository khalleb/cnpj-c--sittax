using Sittax.Cnpj.Data;
using Sittax.Cnpj.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Sittax.Cnpj.Repositories
{
    
    public interface IReceitaFederalSociosRepository
    {
        Task AdicionarLoteAsync(IEnumerable<ReceitaFederalSocios> socios);
        Task<int> ContarAsync();
        Task LimparTabelaAsync();
    }
    
    public class ReceitaFederalSociosRepository : IReceitaFederalSociosRepository
    {
        private readonly SittaxCnpjDbContext _context;

        public ReceitaFederalSociosRepository(SittaxCnpjDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarLoteAsync(IEnumerable<ReceitaFederalSocios> socios)
        {
            await _context.ReceitaFederalSocios.AddRangeAsync(socios);
            await _context.SaveChangesAsync();
        }

        public async Task<int> ContarAsync()
        {
            return await _context.ReceitaFederalSocios.CountAsync();
        }

        public async Task LimparTabelaAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE rf_socios RESTART IDENTITY CASCADE");
        }
    }
}
