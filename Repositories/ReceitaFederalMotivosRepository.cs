using Sittax.Cnpj.Data;
using Sittax.Cnpj.Data.Models;

using Microsoft.EntityFrameworkCore;

namespace Sittax.Cnpj.Repositories
{
    public interface IReceitaFederalMotivosRepository
    {
        Task AdicionarLoteAsync(IEnumerable<ReceitaFederalMotivos> motivos);
        Task<int> ContarAsync();
        Task LimparTabelaAsync();
    }

    public class ReceitaFederalMotivosRepository : IReceitaFederalMotivosRepository
    {
        private readonly SittaxCnpjDbContext _context;

        public ReceitaFederalMotivosRepository(SittaxCnpjDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarLoteAsync(IEnumerable<ReceitaFederalMotivos> motivos)
        {
            await _context.ReceitaFederalMotivos.AddRangeAsync(motivos);
            await _context.SaveChangesAsync();
        }

        public async Task<int> ContarAsync()
        {
            return await _context.ReceitaFederalMotivos.CountAsync();
        }

        public async Task LimparTabelaAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE rf_motivos RESTART IDENTITY CASCADE");
        }
    }
}
