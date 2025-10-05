using System.Globalization;
using System.Text;
using Sittax.Cnpj.Data.Models;
using Sittax.Cnpj.Repositories;
using Sittax.Utils;

namespace Sittax.Cnpj.Services;

public class ReceitaFederalCsvProcessorService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReceitaFederalCsvProcessorService> _logger;
    private readonly ReceitaFederalLogArquivosService _servicoLog;

    private readonly Dictionary<string, TipoArquivoCsv> _mapeamentoTipoArquivo;

    private const int TAMANHO_LOTE = 1000; // Reduzido para melhor gestão de memória
    private const int INTERVALO_COMMIT = 5000; // Reduzido para commits mais frequentes
    private const int INTERVALO_LIMPEZA_MEMORIA = 10000; // A cada 10k registros, força limpeza

    public ReceitaFederalCsvProcessorService(IServiceProvider serviceProvider,
        ILogger<ReceitaFederalCsvProcessorService> logger, ReceitaFederalLogArquivosService servicoLog)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _servicoLog = servicoLog;

        _mapeamentoTipoArquivo = new Dictionary<string, TipoArquivoCsv>(StringComparer.OrdinalIgnoreCase)
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


    public async Task ProcessarCsvIndividualAsync(string nomeArquivoCsv, string periodo,
        CancellationToken tokenCancelamento = default)
    {
        var caminhoCompleto = ObterCaminhoCsv(nomeArquivoCsv);

        if (!File.Exists(caminhoCompleto))
        {
            var erro = $"Arquivo CSV não encontrado: {caminhoCompleto}";
            _logger.LogWarning(erro);
            return;
        }

        TipoArquivoCsv tipoArquivo = IdentificarTipoArquivo(nomeArquivoCsv);
        if (tipoArquivo == TipoArquivoCsv.Desconhecido)
        {
            var erro = $"Tipo de arquivo não identificado: {nomeArquivoCsv}";
            _logger.LogWarning(erro);
            return;
        }

        _logger.LogInformation("📄 Processando {Tipo}: {FileName}", tipoArquivo, nomeArquivoCsv);

        try
        {
            await _servicoLog.AtualizarStatusCsvAsync(nomeArquivoCsv, periodo, StatusCsv.Processando)
                .ConfigureAwait(true);

            var totalProcessado = tipoArquivo switch
            {
                TipoArquivoCsv.Empresas => await ProcessarEmpresasAsync(caminhoCompleto, tokenCancelamento)
                    .ConfigureAwait(true),
                TipoArquivoCsv.Estabelecimentos => await ProcessarEstabelecimentosAsync(caminhoCompleto,
                    tokenCancelamento).ConfigureAwait(true),
                TipoArquivoCsv.Socios => await ProcessarSociosAsync(caminhoCompleto, tokenCancelamento)
                    .ConfigureAwait(true),
                TipoArquivoCsv.Simples => await ProcessarSimplesAsync(caminhoCompleto, tokenCancelamento)
                    .ConfigureAwait(true),
                TipoArquivoCsv.Cnaes => await ProcessarCnaesAsync(caminhoCompleto, tokenCancelamento)
                    .ConfigureAwait(true),
                TipoArquivoCsv.Naturezas => await ProcessarNaturezasAsync(caminhoCompleto, tokenCancelamento)
                    .ConfigureAwait(true),
                TipoArquivoCsv.Qualificacoes => await ProcessarQualificacoesAsync(caminhoCompleto,
                    tokenCancelamento).ConfigureAwait(true),
                TipoArquivoCsv.Paises => await ProcessarPaisesAsync(caminhoCompleto, tokenCancelamento)
                    .ConfigureAwait(true),
                TipoArquivoCsv.Municipios => await ProcessarMunicipiosAsync(caminhoCompleto, tokenCancelamento)
                    .ConfigureAwait(true),
                TipoArquivoCsv.Motivos => await ProcessarMotivosAsync(caminhoCompleto, tokenCancelamento)
                    .ConfigureAwait(true),
                _ => 0
            };

            await _servicoLog.AtualizarStatusCsvAsync(nomeArquivoCsv, periodo, StatusCsv.Processado,
                totalProcessado).ConfigureAwait(true);
            _logger.LogInformation("✅ Processamento concluído: {FileName} - {Total} registros", nomeArquivoCsv,
                totalProcessado);
        }
        catch (Exception ex)
        {
            var erro = $"Erro ao processar {nomeArquivoCsv}: {ex.Message}";
            _logger.LogError(ex, erro);
            await _servicoLog.AtualizarStatusCsvAsync(nomeArquivoCsv, periodo, StatusCsv.Erro).ConfigureAwait(true);
            throw;
        }
    }


    private async Task<int> ProcessarEmpresasAsync(string caminhoArquivo, CancellationToken tokenCancelamento)
    {
        var totalProcessado = 0;
        var lote = new List<ReceitaFederalEmpresas>(TAMANHO_LOTE);

        using IServiceScope escopo = _serviceProvider.CreateScope();
        IReceitaFederalEmpresasRepository repositorio = escopo.ServiceProvider.GetRequiredService<IReceitaFederalEmpresasRepository>();

        try
        {
            await foreach (var campos in CsvUtil.LerArquivoCsvAsync(caminhoArquivo, tokenCancelamento)
                               .ConfigureAwait(true))
            {
                try
                {
                    if (campos.Length < 7)
                    {
                        continue;
                    }

                    var empresa = new ReceitaFederalEmpresas
                    {
                        CnpjBasico = LimparCampo(campos[0]) ?? string.Empty,
                        RazaoSocialNomeEmpresarial = LimparCampo(campos[1]),
                        NaturezaJuridica = LimparCampo(campos[2]),
                        QualificacaoResponsavel = LimparCampo(campos[3]),
                        CapitalSocialEmpresa = ConverterParaDecimal(campos[4]),
                        PorteEmpresa = LimparCampo(campos[5]),
                        EnteFederativoResponsavel = LimparCampo(campos[6])
                    };

                    lote.Add(empresa);

                    if (lote.Count >= TAMANHO_LOTE)
                    {
                        await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true);
                        totalProcessado += lote.Count;

                        lote.Clear();
                        lote = new List<ReceitaFederalEmpresas>(TAMANHO_LOTE); // Nova lista para liberar memória

                        if (totalProcessado % INTERVALO_COMMIT == 0)
                        {
                            _logger.LogDebug("Processados {Count:N0} registros de Empresas", totalProcessado);
                        }

                        if (totalProcessado % INTERVALO_LIMPEZA_MEMORIA == 0)
                        {
                            ForcarLimpezaMemoria();
                        }
                    }

                    tokenCancelamento.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao processar linha");
                }
            }

            if (lote.Count != 0)
            {
                await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true);
                totalProcessado += lote.Count;
            }
        }
        finally
        {
            lote.Clear();
            ForcarLimpezaMemoria();
        }

        return totalProcessado;
    }

    private async Task<int> ProcessarEstabelecimentosAsync(string caminhoArquivo,
        CancellationToken tokenCancelamento)
    {
        var totalProcessado = 0;
        var lote = new List<ReceitaFederalEstabelecimentos>(TAMANHO_LOTE);

        using IServiceScope escopo = _serviceProvider.CreateScope();
        IReceitaFederalEstabelecimentosRepository repositorio = escopo.ServiceProvider.GetRequiredService<IReceitaFederalEstabelecimentosRepository>();

        try
        {
            await foreach (var campos in CsvUtil.LerArquivoCsvAsync(caminhoArquivo, tokenCancelamento)
                               .ConfigureAwait(true))
            {
                try
                {
                    if (campos.Length < 30)
                    {
                        continue;
                    }

                    var estabelecimento = CriarEstabelecimento(campos);
                    lote.Add(estabelecimento);

                    if (lote.Count >= TAMANHO_LOTE)
                    {
                        await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true);
                        totalProcessado += lote.Count;

                        lote.Clear();
                        lote = new List<ReceitaFederalEstabelecimentos>(TAMANHO_LOTE);

                        if (totalProcessado % INTERVALO_COMMIT == 0)
                        {
                            _logger.LogDebug("Processados {Count:N0} registros de Estabelecimentos",
                                totalProcessado);
                        }

                        if (totalProcessado % INTERVALO_LIMPEZA_MEMORIA == 0)
                        {
                            ForcarLimpezaMemoria();
                        }
                    }

                    tokenCancelamento.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao processar linha");
                }
            }

            if (lote.Count != 0)
            {
                await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true);
                totalProcessado += lote.Count;
            }
        }
        finally
        {
            lote.Clear();
            ForcarLimpezaMemoria();
        }

        return totalProcessado;
    }

    private static ReceitaFederalEstabelecimentos CriarEstabelecimento(string[] campos)
    {
        return new ReceitaFederalEstabelecimentos
        {
            CnpjBasico = LimparCampo(campos[0]) ?? string.Empty,
            CnpjOrdem = LimparCampo(campos[1]) ?? string.Empty,
            CnpjDv = LimparCampo(campos[2]) ?? string.Empty,
            CnpjCompleto =
                (LimparCampo(campos[0]) ?? "") + (LimparCampo(campos[1]) ?? "") + (LimparCampo(campos[2]) ?? ""),
            IdentificadorMatrizFilial = LimparCampo(campos[3]) ?? string.Empty,
            NomeFantasia = LimparCampo(campos[4]),
            SituacaoCadastral = LimparCampo(campos[5]) ?? string.Empty,
            DataSituacaoCadastral = ConverterParaData(campos[6]),
            MotivoSituacaoCadastral = LimparCampo(campos[7]),
            NomeCidadeExterior = LimparCampo(campos[8]),
            Pais = LimparCampo(campos[9]),
            DataInicioAtividade = ConverterParaData(campos[10]),
            CnaeFiscalPrincipal = LimparCampo(campos[11]) ?? string.Empty,
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
            DataSituacaoEspecial = ConverterParaData(campos[29])
        };
    }

    private static void ForcarLimpezaMemoria()
    {
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);
    }

    private async Task<int> ProcessarSociosAsync(string caminhoArquivo, CancellationToken tokenCancelamento)
    {
        var totalProcessado = 0;
        var lote = new List<ReceitaFederalSocios>(TAMANHO_LOTE);

        using IServiceScope escopo = _serviceProvider.CreateScope();
        IReceitaFederalSociosRepository repositorio = escopo.ServiceProvider.GetRequiredService<IReceitaFederalSociosRepository>();

        try
        {
            await foreach (var campos in CsvUtil.LerArquivoCsvAsync(caminhoArquivo, tokenCancelamento)
                               .ConfigureAwait(true))
            {
                try
                {
                    if (campos.Length < 11)
                    {
                        continue;
                    }

                    var socio = new ReceitaFederalSocios
                    {
                        CnpjBasico = LimparCampo(campos[0]) ?? string.Empty,
                        IdentificadorSocio = LimparCampo(campos[1]) ?? string.Empty,
                        NomeSocioRazaoSocial = LimparCampo(campos[2]),
                        CpfCnpjSocio = LimparCampo(campos[3]),
                        QualificacaoSocio = LimparCampo(campos[4]),
                        DataEntradaSociedade = ConverterParaData(campos[5]),
                        Pais = LimparCampo(campos[6]),
                        RepresentanteLegal = LimparCampo(campos[7]),
                        NomeRepresentante = LimparCampo(campos[8]),
                        QualificacaoRepresentanteLegal = LimparCampo(campos[9]),
                        FaixaEtaria = LimparCampo(campos[10])
                    };

                    lote.Add(socio);

                    if (lote.Count >= TAMANHO_LOTE)
                    {
                        await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true);
                        totalProcessado += lote.Count;

                        lote.Clear();
                        lote = new List<ReceitaFederalSocios>(TAMANHO_LOTE);

                        if (totalProcessado % INTERVALO_LIMPEZA_MEMORIA == 0)
                        {
                            ForcarLimpezaMemoria();
                        }
                    }
                }
                catch (Exception)
                {
                    // await _servicoLogErros.RegistrarErroAsync(
                    //     $"Erro ao processar linha de sócio", "ProcessarSocios", ex);
                }
            }

            if (lote.Count != 0)
            {
                await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true);
                totalProcessado += lote.Count;
            }
        }
        finally
        {
            lote.Clear();
            ForcarLimpezaMemoria();
        }

        return totalProcessado;
    }

    private async Task<int> ProcessarCnaesAsync(string caminhoArquivo, CancellationToken tokenCancelamento)
    {
        using IServiceScope escopo = _serviceProvider.CreateScope();
        IReceitaFederalCnaesRepository repositorio = escopo.ServiceProvider.GetRequiredService<IReceitaFederalCnaesRepository>();

        return await ProcessarTabelaAuxiliarAsync(
            caminhoArquivo,
            campos => new ReceitaFederalCnaes
            {
                Codigo = LimparCampo(campos[0]) ?? string.Empty, Descricao = LimparCampo(campos[1])
            },
            async lote => await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true),
            "CNAEs",
            tokenCancelamento).ConfigureAwait(true);
    }

    private static async Task<int> ProcessarTabelaAuxiliarAsync<T>(
        string caminhoArquivo,
        Func<string[], T> criarEntidade,
        Func<List<T>, Task> salvarLote,
        string nomeTabela,
        CancellationToken tokenCancelamento) where T : ReceitaFederalEntityBase
    {
        var totalProcessado = 0;
        var lote = new List<T>(TAMANHO_LOTE);

        try
        {
            await foreach (var campos in CsvUtil.LerArquivoCsvAsync(caminhoArquivo, tokenCancelamento)
                               .ConfigureAwait(true))
            {
                try
                {
                    if (campos.Length < 2)
                    {
                        continue;
                    }

                    var entidade = criarEntidade(campos);
                    lote.Add(entidade);

                    if (lote.Count >= TAMANHO_LOTE)
                    {
                        await salvarLote(lote).ConfigureAwait(true);
                        totalProcessado += lote.Count;
                        lote.Clear();
                        lote = new List<T>(TAMANHO_LOTE);

                        if (totalProcessado % INTERVALO_LIMPEZA_MEMORIA == 0)
                        {
                            ForcarLimpezaMemoria();
                        }
                    }
                }
                catch (Exception)
                {
                    // await _servicoLogErros.RegistrarErroAsync(
                    //     $"Erro ao processar linha de {nomeTabela}",
                    //     $"ProcessarTabelaAuxiliar_{nomeTabela}", ex);
                }
            }

            if (lote.Count != 0)
            {
                await salvarLote(lote).ConfigureAwait(true);
                totalProcessado += lote.Count;
            }
        }
        finally
        {
            lote.Clear();
            ForcarLimpezaMemoria();
        }

        return totalProcessado;
    }

    private async Task<int> ProcessarSimplesAsync(string caminhoArquivo, CancellationToken tokenCancelamento)
    {
        var totalProcessado = 0;
        var lote = new List<ReceitaFederalSimples>(TAMANHO_LOTE);

        using IServiceScope escopo = _serviceProvider.CreateScope();
        IReceitaFederalSimplesRepository repositorio = escopo.ServiceProvider.GetRequiredService<IReceitaFederalSimplesRepository>();

        try
        {
            await foreach (var campos in CsvUtil.LerArquivoCsvAsync(caminhoArquivo, tokenCancelamento)
                               .ConfigureAwait(true))
            {
                try
                {
                    if (campos.Length < 7)
                    {
                        continue;
                    }

                    var simples = new ReceitaFederalSimples
                    {
                        CnpjBasico = LimparCampo(campos[0]) ?? string.Empty,
                        OpcaoPeloSimples = LimparCampo(campos[1]),
                        DataOpcaoSimples = ConverterParaData(campos[2]),
                        DataExclusaoSimples = ConverterParaData(campos[3]),
                        OpcaoPeloMei = LimparCampo(campos[4]),
                        DataOpcaoMei = ConverterParaData(campos[5]),
                        DataExclusaoMei = ConverterParaData(campos[6])
                    };

                    lote.Add(simples);

                    if (lote.Count >= TAMANHO_LOTE)
                    {
                        await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true);
                        totalProcessado += lote.Count;
                        lote.Clear();
                        lote = new List<ReceitaFederalSimples>(TAMANHO_LOTE);
                    }
                }
                catch (Exception)
                {
                    // await _servicoLogErros.RegistrarErroAsync(
                    //     $"Erro ao processar linha de Simples", "ProcessarSimples", ex);
                }
            }

            if (lote.Count != 0)
            {
                await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true);
                totalProcessado += lote.Count;
            }
        }
        finally
        {
            lote.Clear();
            ForcarLimpezaMemoria();
        }

        return totalProcessado;
    }

    private async Task<int> ProcessarNaturezasAsync(string caminhoArquivo, CancellationToken tokenCancelamento)
    {
        using IServiceScope escopo = _serviceProvider.CreateScope();
        IReceitaFederalNaturezasRepository repositorio = escopo.ServiceProvider.GetRequiredService<IReceitaFederalNaturezasRepository>();

        return await ProcessarTabelaAuxiliarAsync(
            caminhoArquivo,
            campos => new ReceitaFederalNaturezas
            {
                Codigo = LimparCampo(campos[0]) ?? string.Empty, Descricao = LimparCampo(campos[1])
            },
            async lote => await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true),
            "Naturezas",
            tokenCancelamento).ConfigureAwait(true);
    }

    private async Task<int> ProcessarQualificacoesAsync(string caminhoArquivo, CancellationToken tokenCancelamento)
    {
        using IServiceScope escopo = _serviceProvider.CreateScope();
        IReceitaFederalQualificacoesRepository repositorio = escopo.ServiceProvider.GetRequiredService<IReceitaFederalQualificacoesRepository>();

        return await ProcessarTabelaAuxiliarAsync(
            caminhoArquivo,
            campos => new ReceitaFederalQualificacoes
            {
                Codigo = LimparCampo(campos[0]) ?? string.Empty, Descricao = LimparCampo(campos[1])
            },
            async lote => await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true),
            "Qualificações",
            tokenCancelamento).ConfigureAwait(true);
    }

    private async Task<int> ProcessarPaisesAsync(string caminhoArquivo, CancellationToken tokenCancelamento)
    {
        using IServiceScope escopo = _serviceProvider.CreateScope();
        IReceitaFederalPaisesRepository repositorio = escopo.ServiceProvider.GetRequiredService<IReceitaFederalPaisesRepository>();

        return await ProcessarTabelaAuxiliarAsync(
            caminhoArquivo,
            campos => new ReceitaFederalPaises
            {
                Codigo = LimparCampo(campos[0]) ?? string.Empty, Descricao = LimparCampo(campos[1])
            },
            async lote => await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true),
            "Países",
            tokenCancelamento).ConfigureAwait(true);
    }

    private async Task<int> ProcessarMunicipiosAsync(string caminhoArquivo, CancellationToken tokenCancelamento)
    {
        using IServiceScope escopo = _serviceProvider.CreateScope();
        IReceitaFederalMunicipiosRepository repositorio = escopo.ServiceProvider.GetRequiredService<IReceitaFederalMunicipiosRepository>();

        return await ProcessarTabelaAuxiliarAsync(
            caminhoArquivo,
            campos => new ReceitaFederalMunicipios
            {
                Codigo = LimparCampo(campos[0]) ?? string.Empty, Descricao = LimparCampo(campos[1])
            },
            async lote => await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true),
            "Municipios",
            tokenCancelamento).ConfigureAwait(true);
    }

    private async Task<int> ProcessarMotivosAsync(string caminhoArquivo, CancellationToken tokenCancelamento)
    {
        using IServiceScope escopo = _serviceProvider.CreateScope();
        IReceitaFederalMotivosRepository repositorio = escopo.ServiceProvider.GetRequiredService<IReceitaFederalMotivosRepository>();

        return await ProcessarTabelaAuxiliarAsync(
            caminhoArquivo,
            campos => new ReceitaFederalMotivos
            {
                Codigo = LimparCampo(campos[0]) ?? string.Empty, Descricao = LimparCampo(campos[1])
            },
            async lote => await repositorio.AdicionarLoteAsync(lote).ConfigureAwait(true),
            "Motivos",
            tokenCancelamento).ConfigureAwait(true);
    }


    private static string? LimparCampo(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }


        valor = valor.Trim();


        while (valor.StartsWith("\"") && valor.EndsWith("\"") && valor.Length > 1)
        {
            valor = valor.Substring(1, valor.Length - 2);
        }


        while (valor.StartsWith("'") && valor.EndsWith("'") && valor.Length > 1)
        {
            valor = valor.Substring(1, valor.Length - 2);
        }


        valor = valor.Trim();


        valor = valor.Replace("\"\"", "\"");

        return string.IsNullOrWhiteSpace(valor) ? null : valor;
    }

    private static DateTime? ConverterParaData(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor == "0" || valor == "00000000")
        {
            return null;
        }

        if (valor.Length == 8 && DateTime.TryParseExact(valor, "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
        {
            return DateTime.SpecifyKind(data, DateTimeKind.Utc);
        }

        return null;
    }


    private TipoArquivoCsv IdentificarTipoArquivo(string nomeArquivo)
    {
        var nomeArquivoLower = nomeArquivo.ToLowerInvariant();

        foreach (var kvp in _mapeamentoTipoArquivo)
        {
            if (nomeArquivoLower.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        return TipoArquivoCsv.Desconhecido;
    }

    private static decimal? ConverterParaDecimal(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        valor = valor.Replace(",", ".");

        if (decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var resultado))
        {
            return resultado;
        }

        return null;
    }

    private static string ObterCaminhoCsv(string nomeArquivo)
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
