using Sittax.Cnpj.Data;
using Sittax.Cnpj.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Sittax.Cnpj.Repositories
{
    
    public interface IReceitaFederalMunicipiosRepository
    {
        Task AdicionarLoteAsync(IEnumerable<ReceitaFederalMunicipios> municipios);
        Task<int> ContarAsync();
        Task LimparTabelaAsync();
    }
    
    public class ReceitaFederalMunicipiosRepository : IReceitaFederalMunicipiosRepository
    {
        private readonly SittaxCnpjDbContext _context;

        public ReceitaFederalMunicipiosRepository(SittaxCnpjDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarLoteAsync(IEnumerable<ReceitaFederalMunicipios> municipios)
        {
            await _context.ReceitaFederalMunicipios.AddRangeAsync(municipios);
            await _context.SaveChangesAsync();
        }

        public async Task<int> ContarAsync()
        {
            return await _context.ReceitaFederalMunicipios.CountAsync();
        }

        public async Task LimparTabelaAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE rf_municipios RESTART IDENTITY CASCADE");
        }
    }
}
