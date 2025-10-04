using System.IO.Compression;
using System.Text.RegularExpressions;

using Sittax.Cnpj.Services;
using Sittax.Cnpj.Workers.ReceitaFederalDados;

using STX.Core;

namespace Sittax.Cnpj.Workers
{
    public class ReceitaFederalDadosEmpresasWorker : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly GerenciadorDownloadProgresso _progressManager;
        private readonly ILogger<ReceitaFederalDadosEmpresasWorker> _logger;
        private readonly HttpClient _httpClient;
        private XTimer _TimerProcessarDadosReceitaFederal;

        // Configurações
        private const string BaseUrl = "https://arquivos.receitafederal.gov.br/dados/cnpj/dados_abertos_cnpj/";
        private readonly string _downloadDir;
        private readonly string _extractDir;

        // Configurações via environment ou appsettings
        private readonly bool _deleteZipAfterExtraction;
        private readonly long _minimumFreeSpaceGB;
        private readonly bool _validateCsvIntegrity;
        private readonly bool _registerAllCsvFiles;

        // Controle de recursos
        private readonly SemaphoreSlim _downloadSemaphore;
        private readonly int _maxParallelDownloads = 1;
        private readonly int _maxRetries = 7;
        private readonly int _timeoutMinutes = 60;

        public ReceitaFederalDadosEmpresasWorker(ILogger<ReceitaFederalDadosEmpresasWorker> logger, IServiceProvider serviceProvider, GerenciadorDownloadProgresso progressManager, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _progressManager = progressManager;

            // Carregar configurações
            _deleteZipAfterExtraction = configuration.GetValue<bool>("ReceitaFederal:DeleteZipAfterExtraction", false);
            _minimumFreeSpaceGB = configuration.GetValue<long>("ReceitaFederal:MinimumFreeSpaceGB", 10);
            _validateCsvIntegrity = configuration.GetValue<bool>("ReceitaFederal:ValidateCsvIntegrity", true);
            _registerAllCsvFiles = configuration.GetValue<bool>("ReceitaFederal:RegisterAllCsvFiles", false);

            // Configurar diretórios
            var baseDir = Path.Combine(Path.GetTempPath(), "receita_federal_dados");
            _downloadDir = Path.Combine(baseDir, "downloads");
            _extractDir = Path.Combine(baseDir, "extracted");

            Directory.CreateDirectory(_downloadDir);
            Directory.CreateDirectory(_extractDir);

            // Configurar HttpClient
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(_timeoutMinutes) };
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("ReceitaFederalSync", "2.0"));

            _downloadSemaphore = new SemaphoreSlim(_maxParallelDownloads, _maxParallelDownloads);

            // Log das configurações
            _logger.LogInformation("Worker configurado: DeleteZip={DeleteZip}, MinSpace={MinSpace}GB, ValidateCsv={Validate}, RegisterAll={RegisterAll}", _deleteZipAfterExtraction, _minimumFreeSpaceGB, _validateCsvIntegrity, _registerAllCsvFiles);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Iniciando ReceitaFederalDadosEmpresasWorker");

            // Executar imediatamente e depois a cada 24 horas
            _TimerProcessarDadosReceitaFederal = new XTimer(ProcessarDadosReceitaFederal, null, TimeSpan.Zero, TimeSpan.FromDays(1));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Parando ReceitaFederalDadosEmpresasWorker");

            _TimerProcessarDadosReceitaFederal?.Dispose();
            _httpClient?.Dispose();
            _downloadSemaphore?.Dispose();

            return Task.CompletedTask;
        }

        private async void ProcessarDadosReceitaFederal(object state)
        {
            try
            {
                _logger.LogInformation("=== Iniciando processamento dos dados da Receita Federal ===");

                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromHours(6));

                // Verificar espaço em disco antes de iniciar
                if (!await VerificarEspacoEmDiscoAsync())
                {
                    _logger.LogError("Espaço em disco insuficiente. Mínimo necessário: {MinSpace}GB", _minimumFreeSpaceGB);
                    return;
                }

                // 1. Buscar período mais recente
                var periodo = await BuscarPeriodoMaisRecenteAsync(cts.Token);
                if (string.IsNullOrEmpty(periodo))
                {
                    _logger.LogError("Não foi possível obter período mais recente");
                    return;
                }

                // 2. Download dos arquivos
                await ExecutarDownloadArquivosAsync(periodo, cts.Token);

                // 3. Descompactação
                await ExecutarDescompactacaoAsync(periodo, cts.Token);

                _logger.LogInformation("=== Processamento concluído com sucesso para período {Periodo} ===", periodo);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Processamento cancelado por timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante o processamento dos dados da Receita Federal");
            }
        }

        private async Task ProcessarCsvEsalvarNoBancoDeDados()
        {
            using var scope = _serviceProvider.CreateScope();
            var csvServices = scope.ServiceProvider.GetRequiredService<ReceitaFederalCsvProcessorService>();

            csvServices.ProcessarCsvIndividualAsync();
        }

        private async Task<bool> VerificarEspacoEmDiscoAsync()
        {
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(_downloadDir) ?? "C:");
                var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024L * 1024L * 1024L);

                _logger.LogInformation("Espaço livre em disco: {FreeSpace}GB (Mínimo requerido: {MinSpace}GB)", freeSpaceGB, _minimumFreeSpaceGB);

                return freeSpaceGB >= _minimumFreeSpaceGB;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao verificar espaço em disco, continuando mesmo assim");
                return true; // Continuar em caso de erro na verificação
            }
        }

        private async Task ExecutarDescompactacaoAsync(string periodo, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Iniciando descompactação dos arquivos para período {Periodo}", periodo);

                var zipFiles = Directory.GetFiles(_downloadDir, "*.zip");
                if (zipFiles.Length == 0)
                {
                    _logger.LogWarning("Nenhum arquivo ZIP encontrado para descompactar");
                    return;
                }

                var errors = new List<string>();
                var processedCount = 0;
                var skippedCount = 0;
                var totalCsvCount = 0;

                foreach (var zipFile in zipFiles)
                {
                    var fileName = Path.GetFileName(zipFile);
                    _logger.LogInformation("📦 Processando: {FileName}", fileName);

                    using var scope = _serviceProvider.CreateScope();
                    var logService = scope.ServiceProvider.GetRequiredService<ReceitaFederalLogArquivosService>();

                    try
                    {
                        // Verificar se já foi processado com sucesso
                        var logExistente = await logService.ObterPorNomeEPeriodoAsync(fileName, periodo);
                        if (logExistente != null && logExistente.StatusCsv == StatusCsv.Extraido)
                        {
                            _logger.LogInformation("⏭️ Arquivo {FileName} já foi extraído anteriormente, pulando...", fileName);
                            skippedCount++;
                            continue;
                        }

                        // Verificar espaço antes de cada extração
                        if (!await VerificarEspacoEmDiscoAsync())
                        {
                            errors.Add($"Espaço insuficiente para processar {fileName}");
                            continue;
                        }

                        // Criar diretório temporário específico
                        var tempExtractDir = Path.Combine(_extractDir, Path.GetFileNameWithoutExtension(fileName));
                        if (Directory.Exists(tempExtractDir))
                        {
                            _logger.LogDebug("🧹 Limpando diretório antigo: {Dir}", tempExtractDir);
                            Directory.Delete(tempExtractDir, recursive: true);
                        }

                        Directory.CreateDirectory(tempExtractDir);

                        // Iniciar log de descompactação
                        await logService.IniciarDescompactacaoAsync(fileName, periodo);

                        // Descompactar para o diretório temporário
                        var resultado = await DescompactarArquivoIndividualAsync(zipFile, tempExtractDir, cancellationToken);

                        // Procurar CSVs no diretório temporário
                        var csvFiles = Directory.GetFiles(tempExtractDir, "*.csv", SearchOption.AllDirectories);

                        if (csvFiles.Length > 0)
                        {
                            var csvInfos = new List<(string path, long size, string hash)>();

                            foreach (var csvPath in csvFiles)
                            {
                                var csvFileName = Path.GetFileName(csvPath);
                                var finalCsvPath = Path.Combine(_extractDir, $"{Path.GetFileNameWithoutExtension(fileName)}_{csvFileName}");

                                // Substituir se já existir
                                if (File.Exists(finalCsvPath))
                                    File.Delete(finalCsvPath);

                                File.Move(csvPath, finalCsvPath);

                                // Validar integridade se configurado
                                if (_validateCsvIntegrity && !await ValidarIntegridadeCsvAsync(finalCsvPath))
                                {
                                    errors.Add($"CSV corrompido: {Path.GetFileName(finalCsvPath)}");
                                    continue;
                                }

                                // Calcular informações do CSV
                                var fileInfo = new FileInfo(finalCsvPath);
                                var hash = await CalcularHashSha256Async(finalCsvPath);

                                csvInfos.Add((finalCsvPath, fileInfo.Length, hash));
                                totalCsvCount++;

                                _logger.LogInformation("📄 CSV extraído: {CsvName} ({Size})", Path.GetFileName(finalCsvPath), FormatarBytes(fileInfo.Length));
                            }

                            // Registrar CSVs no banco
                            if (_registerAllCsvFiles && csvInfos.Count > 1)
                            {
                                // Registrar todos os CSVs
                                foreach (var (path, size, hash) in csvInfos)
                                {
                                    await logService.RegistrarCsvAdicionalAsync(fileName, periodo, Path.GetFileName(path), size, hash);
                                }
                            }
                            else
                                if (csvInfos.Count > 0)
                                {
                                    // Registrar apenas o primeiro/principal
                                    var principal = csvInfos.First();
                                    await logService.FinalizarDescompactacaoAsync(fileName, periodo, Path.GetFileName(principal.path), principal.size, principal.hash, sucesso: !resultado.Erros.Any());
                                }

                            // Limpar diretório temporário
                            Directory.Delete(tempExtractDir, recursive: true);

                            processedCount++;
                            _logger.LogInformation("✅ Concluído {FileName}: {Extraidos} arquivo(s) extraído(s)", fileName, resultado.ArquivosExtraidos);

                            // Deletar ZIP se configurado e sucesso total
                            if (_deleteZipAfterExtraction && resultado.Erros.Count == 0)
                            {
                                File.Delete(zipFile);
                                _logger.LogInformation("🗑️ Arquivo ZIP deletado após extração: {FileName}", fileName);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Nenhum CSV encontrado no arquivo {FileName}", fileName);
                            await logService.FinalizarDescompactacaoAsync(fileName, periodo, "", 0, "", false);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Erro ao descompactar {fileName}: {ex.Message}");
                        _logger.LogError(ex, "❌ Falha na descompactação de {FileName}", fileName);

                        await logService.FinalizarDescompactacaoAsync(fileName, periodo, "", 0, "", false);
                    }
                }

                // Relatório final detalhado
                _logger.LogInformation("🎯 RESUMO DA DESCOMPACTAÇÃO:");
                _logger.LogInformation("   📦 Total de arquivos ZIP: {Total}", zipFiles.Length);
                _logger.LogInformation("   ✅ Processados: {Processed}", processedCount);
                _logger.LogInformation("   ⏭️ Pulados (já extraídos): {Skipped}", skippedCount);
                _logger.LogInformation("   📄 Total de CSVs extraídos: {CsvCount}", totalCsvCount);
                _logger.LogInformation("   ❌ Erros: {Errors}", errors.Count);

                if (errors.Any())
                {
                    foreach (var error in errors.Take(5))
                    {
                        _logger.LogWarning("   - {Error}", error);
                    }

                    if (errors.Count > 5)
                    {
                        _logger.LogWarning("   ... e mais {Count} erro(s)", errors.Count - 5);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante a descompactação");
                throw;
            }
        }

        private async Task<bool> ValidarIntegridadeCsvAsync(string csvPath)
        {
            try
            {
                using var reader = new StreamReader(csvPath);
                var lineCount = 0;
                var columnCount = -1;

                while (!reader.EndOfStream && lineCount < 100) // Validar primeiras 100 linhas
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var columns = line.Split(';').Length;

                    if (columnCount == -1)
                        columnCount = columns;
                    else
                        if (columns != columnCount)
                        {
                            _logger.LogWarning("CSV com número inconsistente de colunas: {Path}", csvPath);
                            return false;
                        }

                    lineCount++;
                }

                return lineCount > 0; // Arquivo não está vazio
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao validar CSV: {Path}", csvPath);
                return false;
            }
        }

        // Método auxiliar atualizado para receber o diretório de destino
        private async Task<ResultadoDescompactacao> DescompactarArquivoIndividualAsync(string zipFilePath, string targetDirectory, CancellationToken cancellationToken)
        {
            var resultado = new ResultadoDescompactacao();

            try
            {
                using var archive = ZipFile.OpenRead(zipFilePath);

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        continue;

                    try
                    {
                        var extractedPath = await ExtrairEntryAsync(entry, targetDirectory, cancellationToken);
                        if (extractedPath != null)
                        {
                            resultado.ArquivosExtraidos++;

                            var csvPath = await GarantirExtensaoCsvAsync(extractedPath);
                            if (csvPath != extractedPath)
                            {
                                resultado.ArquivosRenomeados++;
                                _logger.LogDebug("🏷️ Renomeado: {Original} → {Novo}", Path.GetFileName(extractedPath), Path.GetFileName(csvPath));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var error = $"Erro ao extrair '{entry.FullName}': {ex.Message}";
                        resultado.Erros.Add(error);
                        _logger.LogWarning("⚠️ {Error}", error);
                    }
                }
            }
            catch (Exception ex)
            {
                resultado.Erros.Add($"Erro ao abrir arquivo ZIP: {ex.Message}");
                throw;
            }

            return resultado;
        }

        private string ValidarEObterCaminhoSeguro(string originalPath)
        {
            // Pega apenas o nome do arquivo (descarta diretórios internos do zip)
            var fileName = Path.GetFileName(originalPath);

            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            // Substitui caracteres inválidos por underscore
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName;
        }

        // Método ExtrairEntryAsync atualizado
        private async Task<string?> ExtrairEntryAsync(ZipArchiveEntry entry, string targetDirectory, CancellationToken cancellationToken)
        {
            // Validar e criar caminho seguro
            var safePath = ValidarEObterCaminhoSeguro(entry.FullName);
            if (string.IsNullOrEmpty(safePath))
            {
                _logger.LogWarning("⚠️ Caminho inseguro ignorado: {Path}", entry.FullName);
                return null;
            }

            var fullPath = Path.Combine(targetDirectory, safePath);

            // Garantir que o diretório pai existe
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Extrair arquivo
            using var entryStream = entry.Open();
            using var outputStream = File.Create(fullPath);

            await entryStream.CopyToAsync(outputStream, cancellationToken);

            return fullPath;
        }

        private async Task<string> GarantirExtensaoCsvAsync(string filePath)
        {
            if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return filePath;

            if (!await EhArquivoDadosReceitaFederalAsync(filePath))
            {
                _logger.LogDebug("📄 Arquivo {FileName} não parece ser dados da RF, mantendo extensão original", Path.GetFileName(filePath));
                return filePath;
            }

            var newPath = $"{filePath}.csv";

            try
            {
                File.Move(filePath, newPath);
                _logger.LogDebug("🏷️ Arquivo renomeado para .csv: {FileName}", Path.GetFileName(newPath));
                return newPath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("⚠️ Erro ao renomear {FileName} para .csv: {Error}", Path.GetFileName(filePath), ex.Message);
                return filePath;
            }
        }

        private async Task ExecutarDownloadArquivosAsync(string periodo, CancellationToken cancellationToken)
        {
            var pageUrl = $"{BaseUrl}{periodo}/";
            var zipUrls = await ListarArquivosZipAsync(pageUrl, cancellationToken);

            if (zipUrls.Count == 0)
            {
                _logger.LogWarning("Nenhum arquivo ZIP encontrado para o período {Periodo}", periodo);
                return;
            }

            _logger.LogInformation("Encontrados {Total} arquivo(s) para download", zipUrls.Count);

            await DownloadTodosArquivosAsync(zipUrls, periodo, cancellationToken);
        }

        private async Task<bool> EhArquivoDadosReceitaFederalAsync(string filePath)
        {
            try
            {
                using var reader = new StreamReader(filePath);

                var firstLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(firstLine))
                    return false;

                if (!firstLine.Contains(';'))
                    return false;

                var fileName = Path.GetFileName(filePath).ToLowerInvariant();

                var padroesDaRF = new[]
                {
                    "emprecsv", "empresas", "estabele", "estabelecimentos", "sociocsv", "socios", "simples", "cnaecsv", "cnaes", "natjucsv", "naturezas", "qualscsv", "qualificacoes", "paiscsv", "paises", "municcsv", "municipios", "moticsv",
                    "motivos"
                };

                var nomeContemPadrao = padroesDaRF.Any(padrao => fileName.Contains(padrao));

                return nomeContemPadrao || firstLine.Split(';').Length >= 3;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Erro ao verificar se é arquivo da RF: {Error}", ex.Message);
                return true;
            }
        }

        private async Task<List<string>> ListarArquivosZipAsync(string pageUrl, CancellationToken cancellationToken)
        {
            var html = await _httpClient.GetStringAsync(pageUrl, cancellationToken);
            var urls = new List<string>();

            var regex = new Regex(@"href=""([^""]+\.zip)""", RegexOptions.IgnoreCase);
            foreach (Match match in regex.Matches(html))
            {
                var href = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(href))
                {
                    var fullUrl = href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : new Uri(new Uri(pageUrl), href).ToString();

                    urls.Add(fullUrl);
                }
            }

            return urls.Distinct().ToList();
        }

        private async Task DownloadTodosArquivosAsync(List<string> urls, string periodo, CancellationToken cancellationToken)
        {
            var tasks = urls.Select(async url =>
            {
                await _downloadSemaphore.WaitAsync(cancellationToken);
                try
                {
                    var fileName = Path.GetFileName(new Uri(url).LocalPath);
                    var filePath = Path.Combine(_downloadDir, fileName);

                    using var scope = _serviceProvider.CreateScope();
                    var logService = scope.ServiceProvider.GetRequiredService<ReceitaFederalLogArquivosService>();

                    var jaProcessado = await logService.VerificarSeJaProcessadoAsync(fileName, periodo);
                    if (jaProcessado)
                    {
                        _logger.LogInformation("Arquivo já processado anteriormente: {FileName}", fileName);
                        return;
                    }

                    if (File.Exists(filePath))
                    {
                        _logger.LogInformation("Arquivo já existe: {FileName}", fileName);
                        return;
                    }

                    await logService.IniciarDownloadAsync(fileName, periodo, 0);

                    var success = await DownloadArquivoComRetryAsync(url, filePath, fileName, periodo, cancellationToken);
                    if (success)
                    {
                        var hashSha256 = await CalcularHashSha256Async(filePath);
                        await logService.FinalizarDownloadAsync(fileName, periodo, hashSha256, true);
                    }
                    else
                    {
                        await logService.FinalizarDownloadAsync(fileName, periodo, "", false);
                    }
                }
                finally
                {
                    _downloadSemaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task LimparArquivoComSegurancaAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            const int maxRetries = 3;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Tentar resetar atributos do arquivo primeiro
                    File.SetAttributes(filePath, FileAttributes.Normal);

                    // Deletar o arquivo
                    File.Delete(filePath);

                    _logger.LogDebug("Arquivo removido: {FilePath}", filePath);
                    return;
                }
                catch (IOException ex) when (i < maxRetries - 1)
                {
                    _logger.LogDebug("Arquivo em uso, tentando novamente em {Delay}ms: {FilePath}", (i + 1) * 500, filePath);

                    // Aguardar progressivamente mais tempo
                    await Task.Delay((i + 1) * 500);

                    // Forçar coleta de lixo para liberar possíveis handles
                    if (i == maxRetries - 2)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Não foi possível deletar arquivo: {FilePath}", filePath);
                    return;
                }
            }
        }

// Método auxiliar para mover arquivo com retry
        private async Task MoverArquivoComRetryAsync(string source, string destination)
        {
            const int maxRetries = 3;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Deletar destino se existir
                    if (File.Exists(destination))
                    {
                        await LimparArquivoComSegurancaAsync(destination);
                    }

                    // Mover arquivo
                    File.Move(source, destination, overwrite: true);

                    _logger.LogDebug("Arquivo movido: {Source} -> {Destination}", source, destination);
                    return;
                }
                catch (IOException ex) when (i < maxRetries - 1)
                {
                    _logger.LogDebug("Erro ao mover arquivo, tentando novamente: {Error}", ex.Message);
                    await Task.Delay((i + 1) * 500);
                }
            }

            throw new IOException($"Não foi possível mover arquivo após {maxRetries} tentativas");
        }

// Método para calcular hash com retry (arquivo pode estar travado temporariamente)
        private async Task<string> CalcularHashSha256AsyncComRetry(string filePath)
        {
            const int maxRetries = 3;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using var sha256 = System.Security.Cryptography.SHA256.Create();

                    // Usar FileShare.Read para permitir leitura simultânea
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024, useAsync: true);

                    var hash = await sha256.ComputeHashAsync(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
                catch (IOException ex) when (i < maxRetries - 1)
                {
                    _logger.LogDebug("Erro ao calcular hash, tentando novamente: {Error}", ex.Message);
                    await Task.Delay((i + 1) * 500);
                }
            }

            throw new IOException($"Não foi possível calcular hash após {maxRetries} tentativas");
        }

        private async Task<bool> DownloadArquivoComRetryAsync(string url, string filePath, string fileName, string periodo, CancellationToken cancellationToken)
        {
            for (int tentativa = 1; tentativa <= _maxRetries; tentativa++)
            {
                try
                {
                    _logger.LogInformation("🔄 Tentativa {Tentativa}/{MaxTentativas}: {FileName}", tentativa, _maxRetries, fileName);

                    // Limpar arquivo parcial/travado antes de tentar novamente
                    await LimparArquivoComSegurancaAsync(filePath);

                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    // Obter tamanho do arquivo
                    long totalBytes = response.Content.Headers.ContentLength ?? 0;

                    // Atualizar o log com o tamanho inicial
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var logService = scope.ServiceProvider.GetRequiredService<ReceitaFederalLogArquivosService>();
                        await logService.IniciarDownloadAsync(fileName, periodo, totalBytes);
                    }

                    _progressManager.StartDownload(fileName, totalBytes);

                    // Usar um arquivo temporário primeiro para evitar conflitos
                    var tempFilePath = $"{filePath}.tmp";

                    try
                    {
                        // Download para arquivo temporário
                        using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                        {
                            // Usar FileOptions.Asynchronous e FileOptions.SequentialScan para melhor performance
                            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1024 * 1024, useAsync: true))
                            {
                                var buffer = new byte[64 * 1024]; // 64KB buffer
                                long totalRead = 0;
                                int bytesRead;

                                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                    totalRead += bytesRead;

                                    _progressManager.UpdateProgress(fileName, totalRead);
                                }

                                // Forçar flush para disco
                                await fileStream.FlushAsync(cancellationToken);
                            }
                        } // FileStream é fechado aqui automaticamente

                        // Aguardar um momento para garantir que o arquivo foi liberado
                        await Task.Delay(100, cancellationToken);

                        // Verificar integridade do arquivo temporário
                        if (!File.Exists(tempFilePath))
                        {
                            throw new FileNotFoundException("Arquivo temporário não encontrado após download");
                        }

                        var tempFileInfo = new FileInfo(tempFilePath);
                        if (tempFileInfo.Length == 0)
                        {
                            throw new InvalidOperationException("Arquivo baixado está vazio");
                        }

                        // Mover arquivo temporário para destino final
                        await MoverArquivoComRetryAsync(tempFilePath, filePath);

                        // Obter informações do arquivo final
                        var finalFileInfo = new FileInfo(filePath);
                        var realSize = finalFileInfo.Length;
                        var hashSha256 = await CalcularHashSha256AsyncComRetry(filePath);

                        _logger.LogInformation("✅ Download completo: {FileName} - Tamanho: {Size}, Hash: {Hash}", fileName, FormatarBytes(realSize), hashSha256);

                        // Atualizar o log com sucesso
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var logService = scope.ServiceProvider.GetRequiredService<ReceitaFederalLogArquivosService>();
                            await logService.FinalizarDownloadComTamanhoRealAsync(fileName, periodo, realSize, hashSha256, true);
                        }

                        _progressManager.CompleteDownload(fileName, true);
                        return true;
                    }
                    finally
                    {
                        // Sempre limpar arquivo temporário
                        await LimparArquivoComSegurancaAsync(tempFilePath);
                    }
                }
                catch (Exception ex) when (tentativa < _maxRetries)
                {
                    _logger.LogWarning("⚠️ Erro na tentativa {Tentativa} para {FileName}: {Erro}", tentativa, fileName, ex.Message);

                    _progressManager.CompleteDownload(fileName, false, $"Tentativa {tentativa} falhou: {ex.Message}");

                    // Limpar arquivos parciais
                    await LimparArquivoComSegurancaAsync(filePath);
                    await LimparArquivoComSegurancaAsync($"{filePath}.tmp");

                    // Backoff exponencial com jitter
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, tentativa) + Random.Shared.Next(1000, 5000) / 1000.0);
                    _logger.LogInformation("⏳ Aguardando {Delay:F1}s antes da próxima tentativa...", delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Falha definitiva no download de {FileName}: {Erro}", fileName, ex.Message);

                    _progressManager.CompleteDownload(fileName, false, ex.Message);

                    // Limpar arquivos
                    await LimparArquivoComSegurancaAsync(filePath);
                    await LimparArquivoComSegurancaAsync($"{filePath}.tmp");

                    // Registrar falha no banco
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var logService = scope.ServiceProvider.GetRequiredService<ReceitaFederalLogArquivosService>();
                        await logService.FinalizarDownloadAsync(fileName, periodo, "", false);
                    }

                    return false;
                }
            }

            return false;
        }

        private async Task<string> CalcularHashSha256Async(string filePath)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private async Task<string> BuscarPeriodoMaisRecenteAsync(CancellationToken cancellationToken)
        {
            var html = await _httpClient.GetStringAsync(BaseUrl, cancellationToken);
            var periodos = new List<string>();

            var regex = new Regex(@"href=""(\d{4}-\d{2})/""", RegexOptions.IgnoreCase);
            foreach (Match match in regex.Matches(html))
            {
                periodos.Add(match.Groups[1].Value);
            }

            if (periodos.Count == 0)
                throw new Exception("Nenhum período encontrado no servidor da RF");

            periodos.Sort((a, b) => string.Compare(b, a, StringComparison.Ordinal));
            return periodos[0];
        }

        private class ResultadoDescompactacao
        {
            public int ArquivosExtraidos { get; set; }
            public int ArquivosRenomeados { get; set; }
            public List<string> Erros { get; set; } = new();
        }

        private static string FormatarBytes(long bytes)
        {
            string[] tamanhos = { "B", "KB", "MB", "GB", "TB" };
            double tamanho = bytes;
            int ordem = 0;

            while (tamanho >= 1024 && ordem < tamanhos.Length - 1)
            {
                ordem++;
                tamanho /= 1024;
            }

            return $"{tamanho:0.##} {tamanhos[ordem]}";
        }
    }
}
