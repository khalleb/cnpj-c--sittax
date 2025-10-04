using Microsoft.EntityFrameworkCore;

using Sittax.Cnpj.Data;
using Sittax.Cnpj.Data.Models;

namespace Sittax.Cnpj.Repositories
{
    public interface IReceitaFederalEstabelecimentosRepository
    {
        Task AdicionarLoteAsync(IEnumerable<ReceitaFederalEstabelecimentos> estabelecimentos);
        Task<int> ContarAsync();
        Task LimparTabelaAsync();
    }

    public class ReceitaFederalEstabelecimentosRepository : IReceitaFederalEstabelecimentosRepository
    {
        private readonly SittaxCnpjDbContext _context;

        public ReceitaFederalEstabelecimentosRepository(SittaxCnpjDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarLoteAsync(IEnumerable<ReceitaFederalEstabelecimentos> estabelecimentos)
        {
            await _context.ReceitaFederalEstabelecimentos.AddRangeAsync(estabelecimentos);
            await _context.SaveChangesAsync();
        }

        public async Task<int> ContarAsync()
        {
            return await _context.ReceitaFederalEstabelecimentos.CountAsync();
        }

        public async Task LimparTabelaAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE rf_estabelecimentos RESTART IDENTITY CASCADE");
        }
    }
}
