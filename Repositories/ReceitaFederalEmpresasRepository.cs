using Sittax.Cnpj.Data;

using Microsoft.EntityFrameworkCore;

using Sittax.Cnpj.Data.Models;

namespace Sittax.Cnpj.Repositories
{
    public interface IReceitaFederalEmpresasRepository
    {
        Task AdicionarLoteAsync(IEnumerable<ReceitaFederalEmpresas> empresas);
        Task<int> ContarAsync();
        Task LimparTabelaAsync();
    }

    public class ReceitaFederalEmpresasRepository : IReceitaFederalEmpresasRepository
    {
        private readonly SittaxCnpjDbContext _context;

        public ReceitaFederalEmpresasRepository(SittaxCnpjDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarLoteAsync(IEnumerable<ReceitaFederalEmpresas> empresas)
        {
            await _context.ReceitaFederalEmpresas.AddRangeAsync(empresas);
            await _context.SaveChangesAsync();
        }

        public async Task<int> ContarAsync()
        {
         return await _context.ReceitaFederalEmpresas.CountAsync();
        }

        public async Task LimparTabelaAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE rf_empresas RESTART IDENTITY CASCADE");
        }
    }
}
