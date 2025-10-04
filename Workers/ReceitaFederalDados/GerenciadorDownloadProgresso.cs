using System.Collections.Concurrent;

namespace Sittax.Cnpj.Workers.ReceitaFederalDados
{
    public class GerenciadorDownloadProgresso
    {
        private readonly ILogger<GerenciadorDownloadProgresso> _logger;
        private readonly ConcurrentDictionary<string, DownloadStatus> _downloads = new();
        private readonly Timer _progressTimer;
        private readonly object _lock = new();

        public GerenciadorDownloadProgresso(ILogger<GerenciadorDownloadProgresso> logger)
        {
            _logger = logger;

            // Timer para reportar progresso a cada 10 segundos
            _progressTimer = new Timer(ReportProgress, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public void StartDownload(string fileName, long totalBytes)
        {
            var status = new DownloadStatus { FileName = fileName, TotalBytes = totalBytes, StartTime = DateTime.UtcNow, Status = "Iniciando" };

            _downloads.AddOrUpdate(fileName, status, (key, existing) => status);

            _logger.LogInformation("📥 Iniciando download: {FileName} ({Size})", fileName, FormatarBytes(totalBytes));
        }

        public void UpdateProgress(string fileName, long downloadedBytes)
        {
            if (_downloads.TryGetValue(fileName, out var status))
            {
                lock (_lock)
                {
                    status.DownloadedBytes = downloadedBytes;
                    status.LastUpdate = DateTime.UtcNow;
                    status.Status = "Baixando";

                    // Calcular velocidade e ETA
                    var elapsed = status.LastUpdate - status.StartTime;
                    if (elapsed.TotalSeconds > 0)
                    {
                        status.SpeedBytesPerSecond = downloadedBytes / elapsed.TotalSeconds;

                        if (status.TotalBytes > 0 && status.SpeedBytesPerSecond > 0)
                        {
                            var remaining = status.TotalBytes - downloadedBytes;
                            status.EstimatedTimeRemaining = TimeSpan.FromSeconds(remaining / status.SpeedBytesPerSecond);
                        }
                    }
                }
            }
        }

        public void CompleteDownload(string fileName, bool success = true, string? error = null)
        {
            if (_downloads.TryGetValue(fileName, out var status))
            {
                lock (_lock)
                {
                    status.Status = success ? "Concluído" : "Erro";
                    status.EndTime = DateTime.UtcNow;
                    status.Error = error;
                    status.IsCompleted = true;
                }

                var totalTime = status.EndTime.Value - status.StartTime;
                var icon = success ? "✅" : "❌";

                _logger.LogInformation("{Icon} {Status}: {FileName} em {Duration:mm\\:ss}", icon, status.Status, fileName, totalTime);

                if (!success && !string.IsNullOrEmpty(error))
                {
                    _logger.LogWarning("   Erro: {Error}", error);
                }
            }
        }

        private void ReportProgress(object? state)
        {
            var activeDownloads = _downloads.Values.Where(d => !d.IsCompleted).ToList();

            if (!activeDownloads.Any())
                return;

            _logger.LogInformation("📊 Progresso dos downloads ativos:");

            foreach (var download in activeDownloads.OrderBy(d => d.FileName))
            {
                var percentage = download.TotalBytes > 0 ? (double)download.DownloadedBytes / download.TotalBytes * 100 : 0;

                var speedMBps = download.SpeedBytesPerSecond / 1024.0 / 1024.0;
                var eta = download.EstimatedTimeRemaining?.ToString(@"mm\:ss") ?? "--:--";

                var progressBar = CreateProgressBar(percentage);

                _logger.LogInformation("   {ProgressBar} {FileName}", progressBar, download.FileName);
                _logger.LogInformation("   {Percentage:F1}% | {Downloaded}/{Total} | {Speed:F1} MB/s | ETA: {ETA}", percentage, FormatarBytes(download.DownloadedBytes), FormatarBytes(download.TotalBytes), speedMBps, eta);
            }

            // Estatísticas gerais
            var totalDownloaded = activeDownloads.Sum(d => d.DownloadedBytes);
            var totalSize = activeDownloads.Sum(d => d.TotalBytes);
            var overallPercentage = totalSize > 0 ? (double)totalDownloaded / totalSize * 100 : 0;

            _logger.LogInformation("📈 Total: {Overall:F1}% | {ActiveCount} arquivo(s) ativo(s)", overallPercentage, activeDownloads.Count);
        }

        private string CreateProgressBar(double percentage)
        {
            const int barLength = 20;
            var filled = (int)(percentage / 100 * barLength);
            var bar = new string('█', filled) + new string('░', barLength - filled);
            return $"[{bar}]";
        }

        private static string FormatarBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int index = 0;
            double size = bytes;

            while (size >= 1024 && index < suffixes.Length - 1)
            {
                size /= 1024;
                index++;
            }

            return $"{size:F2} {suffixes[index]}";
        }

        public void Dispose()
        {
            _progressTimer?.Dispose();
        }
    }
}

public class DownloadStatus
{
    public string FileName { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime LastUpdate { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public double SpeedBytesPerSecond { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public bool IsCompleted { get; set; }
    public string? Error { get; set; }
}

