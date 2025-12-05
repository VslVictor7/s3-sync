using System.Diagnostics;

namespace BackupSync;

public class SyncService
{
    private readonly List<string> _bucketsSelecionados;
    private readonly string _logDir;
    private readonly string _logFile;

    public SyncService(List<string> bucketsSelecionados, string logDir)
    {
        _bucketsSelecionados = bucketsSelecionados;
        _logDir = logDir;

        Directory.CreateDirectory(_logDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
        _logFile = Path.Combine(_logDir, $"rclone_sync_{timestamp}.log");
    }

    private void Log(string msg)
    {
        Console.WriteLine(msg);
        File.AppendAllText(_logFile, msg + Environment.NewLine);
    }

    public async Task RunAsync()
    {
        Log($"=== {DateTime.Now} ===");

        var todos = await ListarBucketsAsync();

        var buckets = todos.Where(b => _bucketsSelecionados.Contains(b)).ToList();

        if (!buckets.Any())
        {
            Log("Nenhum bucket selecionado encontrado no MinIO.");
            return;
        }

        foreach (var bucket in buckets)
        {
            await SincronizarBucketAsync(bucket);
        }

        Log("Sincronização concluída.");
    }

    private async Task<List<string>> ListarBucketsAsync()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "rclone",
                Arguments = "lsd minio:",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            Log($"Erro ao listar buckets: {error.Trim()}");
            return new List<string>();
        }

        return output.Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(" ", StringSplitOptions.RemoveEmptyEntries).Last())
            .ToList();
    }

    private async Task SincronizarBucketAsync(string bucket)
    {
        Log($"Sincronizando {bucket}...");

        var args =
            $"sync minio:{bucket} gdrive:backups/{bucket} --transfers=8 --checkers=8 --log-file=\"{_logFile}\" --log-level INFO";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "rclone",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();

        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            Log($"✅ {bucket} sincronizado com sucesso.\n");
        else
            Log($"❌ Erro ao sincronizar {bucket}: {error}\n");
    }
}