using Microsoft.EntityFrameworkCore;

using Sittax.Cnpj.Data.Models;
using Sittax.Cnpj.Models;

namespace Sittax.Cnpj.Data
{
    public class SittaxCnpjDbContext : DbContext
    {
        public SittaxCnpjDbContext(DbContextOptions<SittaxCnpjDbContext> options)
            : base(options)
        {
        }

        public DbSet<ReceitaFederalLogArquivos> ReceitaFederalLogArquivos { get; set; }
        
        public DbSet<ReceitaFederalCnaes> ReceitaFederalCnaes { get; set; }
        public DbSet<ReceitaFederalEmpresas> ReceitaFederalEmpresas { get; set; }
        public DbSet<ReceitaFederalEstabelecimentos> ReceitaFederalEstabelecimentos { get; set; }
        public DbSet<ReceitaFederalMotivos> ReceitaFederalMotivos { get; set; }
        public DbSet<ReceitaFederalMunicipios> ReceitaFederalMunicipios { get; set; }
        public DbSet<ReceitaFederalNaturezas> ReceitaFederalNaturezas { get; set; }
        public DbSet<ReceitaFederalPaises> ReceitaFederalPaises { get; set; }
        public DbSet<ReceitaFederalQualificacoes> ReceitaFederalQualificacoes { get; set; }
        public DbSet<ReceitaFederalSimples> ReceitaFederalSimples { get; set; }
        public DbSet<ReceitaFederalSocios> ReceitaFederalSocios { get; set; }
  
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurações específicas
            modelBuilder.Entity<ReceitaFederalLogArquivos>(entity =>
            {
                entity.HasIndex(e => e.Periodo);
                entity.HasIndex(e => e.NomeArquivoZip);
                entity.HasIndex(e => new { e.NomeArquivoZip, e.Periodo }).IsUnique();
            });
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Property("UpdatedAt") != null)
                {
                    entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
                }
            }
        }
    }
}
