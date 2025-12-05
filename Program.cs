using DotNetEnv;
using BackupSync;

Env.Load();

var bucketsSelecionados = Env.GetString("BUCKETS_SELECIONADOS")?
    .Split(",", StringSplitOptions.RemoveEmptyEntries)
    .Select(b => b.Trim())
    .ToList() ?? new List<string>();

var logDir = Env.GetString("LOG_DIR") ?? "/var/log";

var service = new SyncService(bucketsSelecionados, logDir);
await service.RunAsync();