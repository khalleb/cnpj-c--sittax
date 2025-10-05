using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Sittax.Cnpj.Data;
using Sittax.Cnpj.Data.Models;
using Sittax.Utils;

namespace Sittax.Cnpj.Services
{
    public class ReceitaFederalCsvProcessorService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReceitaFederalCsvProcessorService> _logger;
        private readonly ReceitaFederalLogArquivosService _logService;


        private const int BATCH_SIZE = 2000;
        private const int COMMIT_INTERVAL = 10000;
        private readonly Dictionary<string, TipoArquivoCsv> _tipoArquivoMap;

        public ReceitaFederalCsvProcessorService(IServiceProvider serviceProvider, ILogger<ReceitaFederalCsvProcessorService> logger, ReceitaFederalLogArquivosService logService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _logService = logService;

            _tipoArquivoMap = new Dictionary<string, TipoArquivoCsv>(StringComparer.OrdinalIgnoreCase)
            {
                ["empresas"] = TipoArquivoCsv.Empresas,
                ["emprecsv"] = TipoArquivoCsv.Empresas,
                ["estabelecimentos"] = TipoArquivoCsv.Estabelecimentos,
                ["estabele"] = TipoArquivoCsv.Estabelecimentos,
                ["socios"] = TipoArquivoCsv.Socios,
                ["sociocsv"] = TipoArquivoCsv.Socios,
                ["simples"] = TipoArquivoCsv.Simples,
                ["cnaes"] = TipoArquivoCsv.Cnaes,
                ["cnaecsv"] = TipoArquivoCsv.Cnaes,
                ["naturezas"] = TipoArquivoCsv.Naturezas,
                ["natjucsv"] = TipoArquivoCsv.Naturezas,
                ["qualificacoes"] = TipoArquivoCsv.Qualificacoes,
                ["qualscsv"] = TipoArquivoCsv.Qualificacoes,
                ["paises"] = TipoArquivoCsv.Paises,
                ["paiscsv"] = TipoArquivoCsv.Paises,
                ["municipios"] = TipoArquivoCsv.Municipios,
                ["municcsv"] = TipoArquivoCsv.Municipios,
                ["motivos"] = TipoArquivoCsv.Motivos,
                ["moticsv"] = TipoArquivoCsv.Motivos
            };

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }


        public async Task ProcessarCsvIndividualAsync(string nomeArquivoCsv, string periodo, CancellationToken cancellationToken = default)
        {
            var caminhoCompleto = ObterCaminhoCsv(nomeArquivoCsv);

            if (!File.Exists(caminhoCompleto))
            {
                _logger.LogWarning("Arquivo CSV não encontrado: {Path}", caminhoCompleto);
                return;
            }

            var tipoArquivo = IdentificarTipoArquivo(nomeArquivoCsv);
            if (tipoArquivo == TipoArquivoCsv.Desconhecido)
            {
                _logger.LogWarning("Tipo de arquivo não identificado: {FileName}", nomeArquivoCsv);
                return;
            }

            _logger.LogInformation("📄 Processando {Tipo}: {FileName}", tipoArquivo, nomeArquivoCsv);

            try
            {
                // Atualizar status para processando
                await _logService.AtualizarStatusCsvAsync(nomeArquivoCsv, periodo, StatusCsv.Processando);

                var totalProcessado = tipoArquivo switch
                {
                    TipoArquivoCsv.Empresas => await ProcessarEmpresasAsync(caminhoCompleto, cancellationToken),
                    TipoArquivoCsv.Estabelecimentos => await ProcessarEstabelecimentosAsync(caminhoCompleto, cancellationToken),
                    TipoArquivoCsv.Socios => await ProcessarSociosAsync(caminhoCompleto, cancellationToken),
                    TipoArquivoCsv.Simples => await ProcessarSimplesAsync(caminhoCompleto, cancellationToken),
                    TipoArquivoCsv.Cnaes => await ProcessarCnaesAsync(caminhoCompleto, cancellationToken),
                    TipoArquivoCsv.Naturezas => await ProcessarNaturezasAsync(caminhoCompleto, cancellationToken),
                    TipoArquivoCsv.Qualificacoes => await ProcessarQualificacoesAsync(caminhoCompleto, cancellationToken),
                    TipoArquivoCsv.Paises => await ProcessarPaisesAsync(caminhoCompleto, cancellationToken),
                    TipoArquivoCsv.Municipios => await ProcessarMunicipiosAsync(caminhoCompleto, cancellationToken),
                    TipoArquivoCsv.Motivos => await ProcessarMotivosAsync(caminhoCompleto, cancellationToken),
                    _ => 0
                };

                // Atualizar status para processado
                await _logService.AtualizarStatusCsvAsync(nomeArquivoCsv, periodo, StatusCsv.Processado, totalProcessado);

                _logger.LogInformation("✅ Processamento concluído: {FileName} - {Total} registros", nomeArquivoCsv, totalProcessado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar {FileName}", nomeArquivoCsv);
                await _logService.AtualizarStatusCsvAsync(nomeArquivoCsv, periodo, StatusCsv.Erro);
                throw;
            }
        }


        private async Task<int> ProcessarEmpresasAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            var totalProcessado = 0;
            var lote = new List<ReceitaFederalEmpresas>(BATCH_SIZE);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SittaxCnpjDbContext>();
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            await foreach (var campos in CsvUtil.LerArquivoCsvAsync(caminhoArquivo, cancellationToken))
            {
                try
                {
                    if (campos.Length < 7) continue;

                    var empresa = new ReceitaFederalEmpresas
                    {
                        CnpjBasico = LimparCampo(campos[0]) ?? string.Empty, // Aplicar LimparCampo aqui também
                        RazaoSocialNomeEmpresarial = LimparCampo(campos[1]),
                        NaturezaJuridica = LimparCampo(campos[2]),
                        QualificacaoResponsavel = LimparCampo(campos[3]),
                        CapitalSocialEmpresa = ParseDecimal(campos[4]),
                        PorteEmpresa = LimparCampo(campos[5]),
                        EnteFederativoResponsavel = LimparCampo(campos[6])
                    };

                    lote.Add(empresa);

                    if (lote.Count >= BATCH_SIZE)
                    {
                        await SalvarLoteAsync(context, lote, "Empresas");
                        totalProcessado += lote.Count;
                        lote.Clear();

                        if (totalProcessado % COMMIT_INTERVAL == 0)
                        {
                            _logger.LogDebug("Processados {Count} registros de Empresas", totalProcessado);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao processar linha: {Linha}", campos);
                }
            }

            if (lote.Any())
            {
                await SalvarLoteAsync(context, lote, "Empresas");
                totalProcessado += lote.Count;
            }

            return totalProcessado;
        }

        private async Task<int> ProcessarEstabelecimentosAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            var totalProcessado = 0;
            var lote = new List<ReceitaFederalEstabelecimentos>(BATCH_SIZE);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SittaxCnpjDbContext>();
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            await foreach (var campos in CsvUtil.LerArquivoCsvAsync(caminhoArquivo, cancellationToken))
            {
                try
                {
                    if (campos.Length < 30) continue;

                    var estabelecimento = new ReceitaFederalEstabelecimentos
                    {
                        CnpjBasico = LimparCampo(campos[0].Trim()),
                        CnpjOrdem = LimparCampo(campos[1].Trim()),
                        CnpjDv = LimparCampo(campos[2].Trim()),
                        CnpjCompleto = LimparCampo(campos[0].Trim()) + LimparCampo(campos[1].Trim()) + LimparCampo(campos[2].Trim()),
                        IdentificadorMatrizFilial = LimparCampo(campos[3]),
                        NomeFantasia = LimparCampo(campos[4]),
                        SituacaoCadastral = LimparCampo(campos[5]),
                        DataSituacaoCadastral = ParseData(campos[6]),
                        MotivoSituacaoCadastral = LimparCampo(campos[7]),
                        NomeCidadeExterior = LimparCampo(campos[8]),
                        Pais = LimparCampo(campos[9]),
                        DataInicioAtividade = ParseData(campos[10]),
                        CnaeFiscalPrincipal = LimparCampo(campos[11]),
                        CnaeFiscalSecundaria = LimparCampo(campos[12]),
                        TipoLogradouro = LimparCampo(campos[13]),
                        Logradouro = LimparCampo(campos[14]),
                        Numero = LimparCampo(campos[15]),
                        Complemento = campos[16],
                        Bairro = LimparCampo(campos[17]),
                        Cep = LimparCampo(campos[18]),
                        Uf = LimparCampo(campos[19]),
                        Municipio = LimparCampo(campos[20]),
                        Ddd1 = LimparCampo(campos[21]),
                        Telefone1 = LimparCampo(campos[22]),
                        Ddd2 = LimparCampo(campos[23]),
                        Telefone2 = LimparCampo(campos[24]),
                        DddFax = LimparCampo(campos[25]),
                        Fax = LimparCampo(campos[26]),
                        CorreioEletronico = LimparCampo(campos[27]),
                        SituacaoEspecial = LimparCampo(campos[28]),
                        DataSituacaoEspecial = ParseData(campos[29])
                    };
                    if (estabelecimento?.CnpjBasico?.Length > 8 || estabelecimento?.Cep?.Length > 8)
                    {
                        Console.WriteLine("sdsds");
                    }

                    lote.Add(estabelecimento);

                    if (lote.Count >= BATCH_SIZE)
                    {
                        await SalvarLoteAsync(context, lote, "Estabelecimentos");
                        totalProcessado += lote.Count;
                        lote.Clear();

                        if (totalProcessado % COMMIT_INTERVAL == 0)
                        {
                            _logger.LogDebug("Processados {Count} registros de Estabelecimentos", totalProcessado);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao processar linha: {Linha}", campos);
                }
            }

            if (lote.Any())
            {
                await SalvarLoteAsync(context, lote, "Estabelecimentos");
                totalProcessado += lote.Count;
            }

            return totalProcessado;
        }

        public async Task LimparDadosAntigosAsync(string periodo, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogWarning("🧹 Iniciando limpeza de dados antigos para reprocessamento");

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SittaxCnpjDbContext>();

                // Obter contagem antes da limpeza
                var counts = new Dictionary<string, int>
                {
                    ["Empresas"] = await context.ReceitaFederalEmpresas.CountAsync(cancellationToken),
                    ["Estabelecimentos"] = await context.ReceitaFederalEstabelecimentos.CountAsync(cancellationToken),
                    ["Sócios"] = await context.ReceitaFederalSocios.CountAsync(cancellationToken),
                    ["Simples"] = await context.ReceitaFederalSimples.CountAsync(cancellationToken)
                };

                foreach (var kvp in counts.Where(x => x.Value > 0))
                {
                    _logger.LogInformation("📊 {Tabela}: {Count:N0} registros serão removidos", kvp.Key, kvp.Value);
                }

                // Executar TRUNCATE para cada tabela (mais rápido que DELETE)
                var tables = new[] { "rf_socios", "rf_simples", "rf_estabelecimentos", "rf_empresas", "rf_cnaes", "rf_motivos", "rf_municipios", "rf_naturezas", "rf_paises", "rf_qualificacoes" };

                foreach (var table in tables)
                {
                    await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {table} RESTART IDENTITY CASCADE", cancellationToken);
                    _logger.LogDebug("✅ Tabela {Table} limpa", table);
                }

                _logger.LogInformation("✅ Limpeza de dados concluída");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao limpar dados antigos");
                throw;
            }
        }

        public async Task<bool> ValidarIntegridadeDadosAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("🔍 Validando integridade dos dados");

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SittaxCnpjDbContext>();

                var problemas = new List<string>();

                // Validar estabelecimentos sem empresa correspondente
                var estabelecimentosSemEmpresa = await context.Database.ExecuteSqlRawAsync(@"
            SELECT COUNT(*)
            FROM rf_estabelecimentos e
            WHERE NOT EXISTS (
                SELECT 1 FROM rf_empresas emp
                WHERE emp.cnpj_basico = e.cnpj_basico
            )", cancellationToken);

                if (estabelecimentosSemEmpresa > 0)
                {
                    problemas.Add($"Encontrados {estabelecimentosSemEmpresa} estabelecimentos sem empresa correspondente");
                }

                // Validar sócios sem empresa correspondente
                var sociosSemEmpresa = await context.Database.ExecuteSqlRawAsync(@"
            SELECT COUNT(*)
            FROM rf_socios s
            WHERE NOT EXISTS (
                SELECT 1 FROM rf_empresas emp
                WHERE emp.cnpj_basico = s.cnpj_basico
            )", cancellationToken);

                if (sociosSemEmpresa > 0)
                {
                    problemas.Add($"Encontrados {sociosSemEmpresa} sócios sem empresa correspondente");
                }

                // Validar CNAEs inválidos
                var estabelecimentosCnaesInvalidos = await context.Database.ExecuteSqlRawAsync(@"
            SELECT COUNT(*)
            FROM rf_estabelecimentos e
            WHERE e.cnae_fiscal_principal IS NOT NULL
            AND e.cnae_fiscal_principal != ''
            AND NOT EXISTS (
                SELECT 1 FROM rf_cnaes c
                WHERE c.codigo = e.cnae_fiscal_principal
            )", cancellationToken);

                if (estabelecimentosCnaesInvalidos > 0)
                {
                    problemas.Add($"Encontrados {estabelecimentosCnaesInvalidos} estabelecimentos com CNAE principal inválido");
                }

                if (problemas.Any())
                {
                    _logger.LogWarning("⚠️ Problemas de integridade encontrados:");
                    foreach (var problema in problemas)
                    {
                        _logger.LogWarning("   - {Problema}", problema);
                    }

                    return false;
                }

                _logger.LogInformation("✅ Validação de integridade concluída sem problemas");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao validar integridade");
                return false;
            }
        }

        public async Task<RelatorioProcessamento> GerarRelatorioProcessamentoAsync(string periodo, CancellationToken cancellationToken = default)
        {
            var relatorio = new RelatorioProcessamento { Periodo = periodo, DataProcessamento = DateTime.UtcNow };

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SittaxCnpjDbContext>();

                // Contagens gerais
                relatorio.TotalEmpresas = await context.ReceitaFederalEmpresas.CountAsync(cancellationToken);
                relatorio.TotalEstabelecimentos = await context.ReceitaFederalEstabelecimentos.CountAsync(cancellationToken);
                relatorio.TotalSocios = await context.ReceitaFederalSocios.CountAsync(cancellationToken);
                relatorio.TotalSimples = await context.ReceitaFederalSimples.CountAsync(cancellationToken);

                // Estatísticas de estabelecimentos por situação
                relatorio.EstabelecimentosAtivos = await context.ReceitaFederalEstabelecimentos.CountAsync(e => e.SituacaoCadastral == "02", cancellationToken);

                relatorio.EstabelecimentosBaixados = await context.ReceitaFederalEstabelecimentos.CountAsync(e => e.SituacaoCadastral == "08", cancellationToken);

                // Estatísticas por UF
                relatorio.EstabelecimentosPorUf = await context.ReceitaFederalEstabelecimentos.Where(e => e.Uf != null).GroupBy(e => e.Uf).Select(g => new EstabelecimentosPorUf { Uf = g.Key, Total = g.Count() }).OrderBy(x => x.Uf)
                    .ToListAsync(cancellationToken);

                // Top 10 CNAEs
                relatorio.TopCnaes = await context.ReceitaFederalEstabelecimentos.Where(e => e.CnaeFiscalPrincipal != null).GroupBy(e => e.CnaeFiscalPrincipal).Select(g => new TopCnae { Cnae = g.Key, Total = g.Count() })
                    .OrderByDescending(x => x.Total).Take(10).ToListAsync(cancellationToken);

                _logger.LogInformation("📊 Relatório de processamento gerado com sucesso");

                return relatorio;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao gerar relatório");
                throw;
            }
        }

        // Métodos auxiliares para outros tipos (implementar de forma similar)
        private async Task<int> ProcessarSociosAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            var totalProcessado = 0;
            var lote = new List<ReceitaFederalSocios>(BATCH_SIZE);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SittaxCnpjDbContext>();
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            await foreach (var campos in CsvUtil.LerArquivoCsvAsync(caminhoArquivo, cancellationToken))
            {
                try
                {
                    if (campos.Length < 11) continue;

                    var socio = new ReceitaFederalSocios
                    {
                        CnpjBasico = campos[0].Trim(),
                        IdentificadorSocio = LimparCampo(campos[1]),
                        NomeSocioRazaoSocial = LimparCampo(campos[2]),
                        CpfCnpjSocio = LimparCampo(campos[3]),
                        QualificacaoSocio = LimparCampo(campos[4]),
                        DataEntradaSociedade = ParseData(campos[5]),
                        Pais = LimparCampo(campos[6]),
                        RepresentanteLegal = LimparCampo(campos[7]),
                        NomeRepresentante = LimparCampo(campos[8]),
                        QualificacaoRepresentanteLegal = LimparCampo(campos[9]),
                        FaixaEtaria = LimparCampo(campos[10])
                    };

                    lote.Add(socio);

                    if (lote.Count >= BATCH_SIZE)
                    {
                        await SalvarLoteAsync(context, lote, "Sócios");
                        totalProcessado += lote.Count;
                        lote.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao processar linha de sócios");
                }
            }

            if (lote.Any())
            {
                await SalvarLoteAsync(context, lote, "Sócios");
                totalProcessado += lote.Count;
            }

            return totalProcessado;
        }

        private async Task<int> ProcessarCnaesAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            return await ProcessarTabelaAuxiliarAsync<ReceitaFederalCnaes>(caminhoArquivo, campos => new ReceitaFederalCnaes
            {
                Codigo = LimparCampo(campos[0]) ?? string.Empty, // Usar LimparCampo aqui
                Descricao = LimparCampo(campos[1])
            }, "CNAEs", cancellationToken);
        }

        // Método genérico para tabelas auxiliares simples (código/descrição)
        private async Task<int> ProcessarTabelaAuxiliarAsync<T>(string caminhoArquivo, Func<string[], T> criarEntidade, string nomeTabela, CancellationToken cancellationToken) where T : ReceitaFederalEntityBase
        {
            var totalProcessado = 0;
            var lote = new List<T>(BATCH_SIZE);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SittaxCnpjDbContext>();
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            await foreach (var campos in CsvUtil.LerArquivoCsvAsync(caminhoArquivo, cancellationToken))
            {
                try
                {
                    if (campos.Length < 2) continue;

                    var entidade = criarEntidade(campos);
                    lote.Add(entidade);

                    if (lote.Count >= BATCH_SIZE)
                    {
                        await SalvarLoteAsync(context, lote, nomeTabela);
                        totalProcessado += lote.Count;
                        lote.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao processar linha de {Tabela}", nomeTabela);
                }
            }

            if (lote.Any())
            {
                await SalvarLoteAsync(context, lote, nomeTabela);
                totalProcessado += lote.Count;
            }

            return totalProcessado;
        }

        // Implementar os demais processadores
        private async Task<int> ProcessarSimplesAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            var totalProcessado = 0;
            var lote = new List<ReceitaFederalSimples>(BATCH_SIZE);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SittaxCnpjDbContext>();
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            await foreach (var campos in CsvUtil.LerArquivoCsvAsync(caminhoArquivo, cancellationToken))
            {
                try
                {
                    if (campos.Length < 7) continue;

                    var simples = new ReceitaFederalSimples
                    {
                        CnpjBasico = LimparCampo(campos[0]) ?? string.Empty,
                        OpcaoPeloSimples = LimparCampo(campos[1]),
                        DataOpcaoSimples = ParseData(campos[2]),
                        DataExclusaoSimples = ParseData(campos[3]),
                        OpcaoPeloMei = LimparCampo(campos[4]),
                        DataOpcaoMei = ParseData(campos[5]),
                        DataExclusaoMei = ParseData(campos[6])
                    };

                    lote.Add(simples);

                    if (lote.Count >= BATCH_SIZE)
                    {
                        await SalvarLoteAsync(context, lote, "Simples");
                        totalProcessado += lote.Count;
                        lote.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao processar linha de Simples");
                }
            }

            if (lote.Any())
            {
                await SalvarLoteAsync(context, lote, "Simples");
                totalProcessado += lote.Count;
            }

            return totalProcessado;
        }

        private async Task<int> ProcessarNaturezasAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            return await ProcessarTabelaAuxiliarAsync<ReceitaFederalNaturezas>(caminhoArquivo, campos => new ReceitaFederalNaturezas { Codigo = LimparCampo(campos[0]) ?? string.Empty, Descricao = LimparCampo(campos[1]) }, "Naturezas",
                cancellationToken);
        }

        private async Task<int> ProcessarQualificacoesAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            return await ProcessarTabelaAuxiliarAsync<ReceitaFederalQualificacoes>(caminhoArquivo, campos => new ReceitaFederalQualificacoes { Codigo = LimparCampo(campos[0]) ?? string.Empty, Descricao = LimparCampo(campos[1]) }, "Qualificações",
                cancellationToken);
        }

        private async Task<int> ProcessarPaisesAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            return await ProcessarTabelaAuxiliarAsync<ReceitaFederalPaises>(caminhoArquivo, campos => new ReceitaFederalPaises { Codigo = LimparCampo(campos[0]) ?? string.Empty, Descricao = LimparCampo(campos[1]) }, "Países", cancellationToken);
        }

        private async Task<int> ProcessarMunicipiosAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            return await ProcessarTabelaAuxiliarAsync<ReceitaFederalMunicipios>(caminhoArquivo, campos => new ReceitaFederalMunicipios { Codigo = LimparCampo(campos[0]) ?? string.Empty, Descricao = LimparCampo(campos[1]) }, "Municípios",
                cancellationToken);
        }

        private async Task<int> ProcessarMotivosAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            return await ProcessarTabelaAuxiliarAsync<ReceitaFederalMotivos>(caminhoArquivo, campos => new ReceitaFederalMotivos { Codigo = LimparCampo(campos[0]) ?? string.Empty, Descricao = LimparCampo(campos[1]) }, "Motivos", cancellationToken);
        }

        // Métodos auxiliares
        private async Task SalvarLoteAsync<T>(DbContext context, List<T> lote, string nomeTabela) where T : class
        {
            if (!lote.Any()) return;

            try
            {
                await context.Set<T>().AddRangeAsync(lote);
                await context.SaveChangesAsync();
                _logger.LogTrace("Salvos {Count} registros de {Tabela}", lote.Count, nomeTabela);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar lote de {Tabela}", nomeTabela);
                throw;
            }
        }

        private string? LimparCampo(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null;

            // Remove espaços no início e fim
            valor = valor.Trim();

            // Remove aspas duplas no início e fim (podem ter múltiplas camadas)
            while (valor.StartsWith("\"") && valor.EndsWith("\"") && valor.Length > 1)
            {
                valor = valor.Substring(1, valor.Length - 2);
            }

            // Remove aspas simples se existirem
            while (valor.StartsWith("'") && valor.EndsWith("'") && valor.Length > 1)
            {
                valor = valor.Substring(1, valor.Length - 2);
            }

            // Limpa espaços novamente após remover as aspas
            valor = valor.Trim();

            // Substitui aspas duplas duplicadas por aspas simples (caso existam no meio do texto)
            valor = valor.Replace("\"\"", "\"");

            return string.IsNullOrWhiteSpace(valor) ? null : valor;
        }

        private DateTime? ParseData(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor) || valor == "0" || valor == "00000000")
                return null;

            if (valor.Length == 8 && DateTime.TryParseExact(valor, "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            {
                return DateTime.SpecifyKind(data, DateTimeKind.Utc);
            }

            return null;
        }


        private decimal? ParseDecimal(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null;

            valor = valor.Replace(",", ".");

            if (decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var resultado))
                return resultado;

            return null;
        }

        private TipoArquivoCsv IdentificarTipoArquivo(string nomeArquivo)
        {
            var nomeArquivoLower = nomeArquivo.ToLowerInvariant();

            foreach (var kvp in _tipoArquivoMap)
            {
                if (nomeArquivoLower.Contains(kvp.Key))
                    return kvp.Value;
            }

            return TipoArquivoCsv.Desconhecido;
        }

        private string ObterCaminhoCsv(string nomeArquivo)
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "receita_federal_dados", "extracted");
            return Path.Combine(baseDir, nomeArquivo);
        }

        public class RelatorioProcessamento
        {
            public string Periodo { get; set; } = string.Empty;
            public DateTime DataProcessamento { get; set; }
            public int TotalEmpresas { get; set; }
            public int TotalEstabelecimentos { get; set; }
            public int TotalSocios { get; set; }
            public int TotalSimples { get; set; }
            public int EstabelecimentosAtivos { get; set; }
            public int EstabelecimentosBaixados { get; set; }
            public List<EstabelecimentosPorUf> EstabelecimentosPorUf { get; set; } = new();
            public List<TopCnae> TopCnaes { get; set; } = new();
        }

        public class EstabelecimentosPorUf
        {
            public string? Uf { get; set; }
            public int Total { get; set; }
        }

        public class TopCnae
        {
            public string? Cnae { get; set; }
            public int Total { get; set; }
        }
    }

    public enum TipoArquivoCsv
    {
        Desconhecido,
        Empresas,
        Estabelecimentos,
        Socios,
        Simples,
        Cnaes,
        Naturezas,
        Qualificacoes,
        Paises,
        Municipios,
        Motivos
    }
}
