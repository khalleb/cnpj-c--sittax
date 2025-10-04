using Sittax.Cnpj.Models;
using Sittax.Cnpj.Repositories;

namespace Sittax.Cnpj.Services
{
    public class ReceitaFederalLogArquivosService
    {
        private readonly IReceitaFederalLogArquivosRepository _logRepository;
        private readonly ILogger<ReceitaFederalLogArquivosService> _logger;

        public ReceitaFederalLogArquivosService(IReceitaFederalLogArquivosRepository logRepository, ILogger<ReceitaFederalLogArquivosService> logger)
        {
            _logRepository = logRepository;
            _logger = logger;
        }

        public async Task<ReceitaFederalLogArquivos?> ObterPorNomeEPeriodoAsync(string nomeArquivo, string periodo)
        {
            return await _logRepository.ObterPorNomeEPeriodoAsync(nomeArquivo, periodo);
        }

        public async Task<ReceitaFederalLogArquivos> IniciarDownloadAsync(string nomeArquivo, string periodo, long tamanhoZip)
        {
            var logExistente = await _logRepository.ObterPorNomeEPeriodoAsync(nomeArquivo, periodo);

            if (logExistente != null)
            {
                logExistente.StatusDownload = StatusDownload.EmProgresso;
                logExistente.DataDownload = DateTime.UtcNow;
                logExistente.TamanhoZip = tamanhoZip;
                return await _logRepository.AtualizarAsync(logExistente);
            }

            var novoLog = new ReceitaFederalLogArquivos
            {
                NomeArquivoZip = nomeArquivo,
                Periodo = periodo,
                TamanhoZip = tamanhoZip,
                StatusDownload = StatusDownload.EmProgresso,
                StatusCsv = StatusCsv.NaoProcessado,
                DataDownload = DateTime.UtcNow,
                UltimaVerificacao = DateTime.UtcNow
            };

            var result = await _logRepository.CriarAsync(novoLog);
            return result;
        }

        public async Task FinalizarDownloadAsync(string nomeArquivo, string periodo, string hashSha256, bool sucesso = true)
        {
            var log = await _logRepository.ObterPorNomeEPeriodoAsync(nomeArquivo, periodo);
            if (log != null)
            {
                log.StatusDownload = sucesso ? StatusDownload.Finalizado : StatusDownload.Erro;
                log.HashSha256Zip = hashSha256;
                log.UltimaVerificacao = DateTime.UtcNow;
                await _logRepository.AtualizarAsync(log);
            }
        }

        public async Task RegistrarExtracaoAsync(string nomeArquivoZip, string periodo, string nomeArquivoCsv, long tamanhoCsv, string hashCsv)
        {
            var log = await _logRepository.ObterPorNomeEPeriodoAsync(nomeArquivoZip, periodo);
            if (log != null)
            {
                log.NomeArquivoCsv = nomeArquivoCsv;
                log.TamanhoCsv = tamanhoCsv;
                log.HashSha256Csv = hashCsv;
                log.StatusCsv = StatusCsv.Extraido;
                await _logRepository.AtualizarAsync(log);
            }
        }

        public async Task<long> ObterTamanhoArquivoRemotoAsync(string url)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Primeiro tenta com HEAD request (mais eficiente)
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength.HasValue)
                {
                    return response.Content.Headers.ContentLength.Value;
                }

                // Se HEAD não funcionar, tenta com GET parcial
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                getRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);

                using var getResponse = await httpClient.SendAsync(getRequest);

                if (getResponse.Content.Headers.ContentLength.HasValue)
                {
                    return getResponse.Content.Headers.ContentLength.Value;
                }

                // Se Content-Range estiver presente
                if (getResponse.Content.Headers.ContentRange?.Length.HasValue == true)
                {
                    return getResponse.Content.Headers.ContentRange.Length.Value;
                }

                _logger.LogWarning("Não foi possível obter o tamanho do arquivo remoto: {Url}", url);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter tamanho do arquivo remoto: {Url}", url);
                return 0;
            }
        }

        public async Task FinalizarDownloadComTamanhoRealAsync(string nomeArquivo, string periodo, long tamanhoReal, string hashSha256, bool sucesso = true)
        {
            var log = await _logRepository.ObterPorNomeEPeriodoAsync(nomeArquivo, periodo);
            if (log != null)
            {
                log.StatusDownload = sucesso ? StatusDownload.Finalizado : StatusDownload.Erro;
                log.HashSha256Zip = hashSha256;
                log.TamanhoZip = tamanhoReal; // Usar o tamanho real do arquivo
                log.UltimaVerificacao = DateTime.UtcNow;

                await _logRepository.AtualizarAsync(log);

                _logger.LogInformation("Download finalizado - Arquivo: {NomeArquivo}, Tamanho: {Tamanho} bytes, Hash: {Hash}", nomeArquivo, tamanhoReal, hashSha256);
            }
            else
            {
                _logger.LogWarning("Tentativa de finalizar download para arquivo não registrado: {NomeArquivo}", nomeArquivo);
            }
        }

        public async Task RegistrarCsvAdicionalAsync(string nomeArquivoZip, string periodo, string nomeArquivoCsv, long tamanhoCsv, string hashCsv)
        {
            // Criar um novo registro ou atualizar existente
            // Você pode criar uma tabela relacionada para múltiplos CSVs
            // ou adicionar lógica específica aqui
            _logger.LogInformation("CSV adicional registrado: {Csv} para {Zip}", nomeArquivoCsv, nomeArquivoZip);
        }

        public async Task<bool> VerificarSeJaProcessadoAsync(string nomeArquivo, string periodo)
        {
            var log = await _logRepository.ObterPorNomeEPeriodoAsync(nomeArquivo, periodo);
            return log?.StatusDownload == StatusDownload.Finalizado && log?.StatusCsv == StatusCsv.Processado;
        }

        public async Task<List<ReceitaFederalLogArquivos>> ObterRelatorioProcessamentoAsync(string periodo)
        {
            return await _logRepository.ListarPorPeriodoAsync(periodo);
        }

        public async Task IniciarDescompactacaoAsync(string nomeArquivoZip, string periodo)
        {
            var log = await _logRepository.ObterPorNomeEPeriodoAsync(nomeArquivoZip, periodo);
            if (log != null)
            {
                log.StatusCsv = StatusCsv.Extraindo;
                log.UltimaVerificacao = DateTime.UtcNow;
                await _logRepository.AtualizarAsync(log);
                _logger.LogDebug("Status de descompactação atualizado para 'Extraindo' - {NomeArquivo}", nomeArquivoZip);
            }
            else
            {
                _logger.LogWarning("Tentativa de iniciar descompactação para arquivo não registrado: {NomeArquivo}", nomeArquivoZip);
            }
        }

        public async Task FinalizarDescompactacaoAsync(string nomeArquivoZip, string periodo, string nomeArquivoCsv, long tamanhoCsv, string hashCsv, bool sucesso)
        {
            var log = await _logRepository.ObterPorNomeEPeriodoAsync(nomeArquivoZip, periodo);
            if (log != null)
            {
                if (sucesso)
                {
                    log.StatusCsv = StatusCsv.Extraido;
                    log.NomeArquivoCsv = nomeArquivoCsv;
                    log.TamanhoCsv = tamanhoCsv;
                    log.HashSha256Csv = hashCsv;
                    _logger.LogInformation("Descompactação concluída com sucesso - {NomeArquivo}", nomeArquivoZip);
                }
                else
                {
                    log.StatusCsv = StatusCsv.Erro;
                    _logger.LogError("Descompactação falhou - {NomeArquivo}", nomeArquivoZip);
                }

                log.UltimaVerificacao = DateTime.UtcNow;
                await _logRepository.AtualizarAsync(log);
            }
            else
            {
                _logger.LogWarning("Tentativa de finalizar descompactação para arquivo não registrado: {NomeArquivo}", nomeArquivoZip);
            }
        }

        // Adicionar estes métodos no ReceitaFederalLogArquivosService.cs

        public async Task<List<ReceitaFederalLogArquivos>> ObterArquivosPendentesProcessamentoAsync()
        {
            return await _logRepository.ListarPorStatusCsvAsync(StatusCsv.Extraido);
        }

        public async Task AtualizarStatusCsvAsync(string nomeArquivoCsv, string periodo, StatusCsv novoStatus, long? totalRegistros = null)
        {
            var log = await _logRepository.ObterPorNomeArquivoCsvAsync(nomeArquivoCsv, periodo);

            if (log != null)
            {
                log.StatusCsv = novoStatus;
                log.UltimaVerificacao = DateTime.UtcNow;

                if (totalRegistros.HasValue && novoStatus == StatusCsv.Processado)
                {
                    // Você pode adicionar um campo TotalRegistros na entidade se quiser armazenar isso
                    _logger.LogInformation("CSV processado com sucesso: {NomeArquivo} - {Total} registros", nomeArquivoCsv, totalRegistros);
                }

                await _logRepository.AtualizarAsync(log);
            }
            else
            {
                _logger.LogWarning("Tentativa de atualizar status de CSV não registrado: {NomeArquivo}", nomeArquivoCsv);
            }
        }
    }
}
