using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Sittax.Cnpj.Data;
using Sittax.Cnpj.Data.Models;

namespace Sittax.Cnpj.Services
{
    public class ReceitaFederalCsvProcessorService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReceitaFederalCsvProcessorService> _logger;
        private readonly ReceitaFederalLogArquivosService _logService;

        // Configurações de processamento
        private const int BATCH_SIZE = 1000; // Tamanho do lote para insert
        private const int COMMIT_INTERVAL = 10000; // Commit a cada N registros
        private readonly Dictionary<string, TipoArquivoCsv> _tipoArquivoMap;

        public ReceitaFederalCsvProcessorService(IServiceProvider serviceProvider, ILogger<ReceitaFederalCsvProcessorService> logger, ReceitaFederalLogArquivosService logService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _logService = logService;

            // Mapeamento de padrões de nome para tipo de arquivo
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

            // Registrar encoding para arquivos da Receita Federal (ISO-8859-1 / Latin1)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public async Task ProcessarTodosCsvsPendentesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("🚀 Iniciando processamento de todos os CSVs pendentes");

                // Buscar arquivos extraídos mas não processados
                var arquivosPendentes = await _logService.ObterArquivosPendentesProcessamentoAsync();

                if (!arquivosPendentes.Any())
                {
                    _logger.LogInformation("Nenhum arquivo CSV pendente para processar");
                    return;
                }

                _logger.LogInformation("Encontrados {Count} arquivo(s) para processar", arquivosPendentes.Count);

                foreach (var arquivo in arquivosPendentes)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await ProcessarCsvIndividualAsync(arquivo.NomeArquivoCsv, arquivo.Periodo, cancellationToken);
                }

                _logger.LogInformation("✅ Processamento de CSVs concluído");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante processamento de CSVs");
                throw;
            }
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

            // Desabilitar tracking para melhor performance
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            await foreach (var linha in LerArquivoCsvAsync(caminhoArquivo, cancellationToken))
            {
                try
                {
                    var campos = linha.Split(';');
                    if (campos.Length < 7) continue;

                    var empresa = new ReceitaFederalEmpresas
                    {
                        CnpjBasico = campos[0].Trim(),
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
                    _logger.LogWarning(ex, "Erro ao processar linha: {Linha}", linha);
                }
            }

            // Salvar último lote
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

            await foreach (var linha in LerArquivoCsvAsync(caminhoArquivo, cancellationToken))
            {
                try
                {
                    var campos = linha.Split(';');
                    if (campos.Length < 30) continue;

                    var estabelecimento = new ReceitaFederalEstabelecimentos
                    {
                        CnpjBasico = campos[0].Trim(),
                        CnpjOrdem = campos[1].Trim(),
                        CnpjDv = campos[2].Trim(),
                        CnpjCompleto = campos[0].Trim() + campos[1].Trim() + campos[2].Trim(),
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
                        Complemento = LimparCampo(campos[16]),
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
                    _logger.LogWarning(ex, "Erro ao processar linha: {Linha}", linha);
                }
            }

            if (lote.Any())
            {
                await SalvarLoteAsync(context, lote, "Estabelecimentos");
                totalProcessado += lote.Count;
            }

            return totalProcessado;
        }

        // Métodos auxiliares para outros tipos (implementar de forma similar)
        private async Task<int> ProcessarSociosAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            var totalProcessado = 0;
            var lote = new List<ReceitaFederalSocios>(BATCH_SIZE);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SittaxCnpjDbContext>();
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            await foreach (var linha in LerArquivoCsvAsync(caminhoArquivo, cancellationToken))
            {
                try
                {
                    var campos = linha.Split(';');
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
            return await ProcessarTabelaAuxiliarAsync<ReceitaFederalCnaes>(caminhoArquivo, campos => new ReceitaFederalCnaes { Codigo = campos[0].Trim(), Descricao = LimparCampo(campos[1]) }, "CNAEs", cancellationToken);
        }

        // Método genérico para tabelas auxiliares simples (código/descrição)
        private async Task<int> ProcessarTabelaAuxiliarAsync<T>(string caminhoArquivo, Func<string[], T> criarEntidade, string nomeTabela, CancellationToken cancellationToken) where T : ReceitaFederalEntityBase
        {
            var totalProcessado = 0;
            var lote = new List<T>(BATCH_SIZE);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SittaxCnpjDbContext>();
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            await foreach (var linha in LerArquivoCsvAsync(caminhoArquivo, cancellationToken))
            {
                try
                {
                    var campos = linha.Split(';');
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
        private async Task<int> ProcessarSimplesAsync(string caminhoArquivo, CancellationToken cancellationToken) => await ProcessarTabelaAuxiliarAsync<ReceitaFederalSimples>(caminhoArquivo,
            campos => new ReceitaFederalSimples
            {
                CnpjBasico = campos[0].Trim(),
                OpcaoPeloSimples = LimparCampo(campos[1]),
                DataOpcaoSimples = ParseData(campos[2]),
                DataExclusaoSimples = ParseData(campos[3]),
                OpcaoPeloMei = LimparCampo(campos[4]),
                DataOpcaoMei = ParseData(campos[5]),
                DataExclusaoMei = ParseData(campos[6])
            }, "Simples", cancellationToken);

        private async Task<int> ProcessarNaturezasAsync(string caminhoArquivo, CancellationToken cancellationToken) => await ProcessarTabelaAuxiliarAsync<ReceitaFederalNaturezas>(caminhoArquivo,
            campos => new ReceitaFederalNaturezas { Codigo = campos[0].Trim(), Descricao = LimparCampo(campos[1]) }, "Naturezas", cancellationToken);

        private async Task<int> ProcessarQualificacoesAsync(string caminhoArquivo, CancellationToken cancellationToken) => await ProcessarTabelaAuxiliarAsync<ReceitaFederalQualificacoes>(caminhoArquivo,
            campos => new ReceitaFederalQualificacoes { Codigo = campos[0].Trim(), Descricao = LimparCampo(campos[1]) }, "Qualificações", cancellationToken);

        private async Task<int> ProcessarPaisesAsync(string caminhoArquivo, CancellationToken cancellationToken) =>
            await ProcessarTabelaAuxiliarAsync<ReceitaFederalPaises>(caminhoArquivo, campos => new ReceitaFederalPaises { Codigo = campos[0].Trim(), Descricao = LimparCampo(campos[1]) }, "Países", cancellationToken);

        private async Task<int> ProcessarMunicipiosAsync(string caminhoArquivo, CancellationToken cancellationToken) => await ProcessarTabelaAuxiliarAsync<ReceitaFederalMunicipios>(caminhoArquivo,
            campos => new ReceitaFederalMunicipios { Codigo = campos[0].Trim(), Descricao = LimparCampo(campos[1]) }, "Municípios", cancellationToken);

        private async Task<int> ProcessarMotivosAsync(string caminhoArquivo, CancellationToken cancellationToken) =>
            await ProcessarTabelaAuxiliarAsync<ReceitaFederalMotivos>(caminhoArquivo, campos => new ReceitaFederalMotivos { Codigo = campos[0].Trim(), Descricao = LimparCampo(campos[1]) }, "Motivos", cancellationToken);

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

        private async IAsyncEnumerable<string> LerArquivoCsvAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            // Encoding Latin1 para arquivos da Receita Federal
            using var reader = new StreamReader(caminhoArquivo, Encoding.GetEncoding("ISO-8859-1"));

            string? linha;
            var linhaNumero = 0;

            while ((linha = await reader.ReadLineAsync()) != null)
            {
                linhaNumero++;

                if (cancellationToken.IsCancellationRequested)
                    yield break;

                if (string.IsNullOrWhiteSpace(linha))
                    continue;

                yield return linha;

                // Log de progresso
                if (linhaNumero % 10000 == 0)
                {
                    _logger.LogDebug("Lidas {Count} linhas de {File}", linhaNumero, Path.GetFileName(caminhoArquivo));
                }
            }
        }

        private string? LimparCampo(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null;

            valor = valor.Trim().Trim('"');
            return string.IsNullOrWhiteSpace(valor) ? null : valor;
        }

        private DateTime? ParseData(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor) || valor == "0" || valor == "00000000")
                return null;

            if (valor.Length == 8 && DateTime.TryParseExact(valor, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            {
                return data;
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
