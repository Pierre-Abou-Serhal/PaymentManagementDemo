using System.Collections.Concurrent;
using PaymentManagement.Api.Persistence;

namespace PaymentManagement.Api.Imports;

public sealed class FolderPoller(PaymentStore store, IConfiguration config, ILogger<FolderPoller> logger) : BackgroundService
{
    private readonly Dictionary<string, (long Size, DateTime Modified, int Stable)> _observations = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<Guid, byte> Active = new();
    public static bool TryClaim(Guid id) => Active.TryAdd(id, 0);
    public static void Release(Guid id) => Active.TryRemove(id, out _);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Scan(); }
            catch (Exception e) { logger.LogError(e, "Folder polling failed; retrying"); }
            await Task.Delay(TimeSpan.FromSeconds(config.GetValue<int?>("Demo:PollSeconds") ?? 3), stoppingToken);
        }
    }
    private async Task Scan()
    {
        await store.RecoverMoves();
        var institutions = await store.ActiveInstitutions();
        Directory.CreateDirectory(store.Root);
        var known = institutions.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateDirectories(store.Root))
            if (!known.Contains(Path.GetFileName(path))) logger.LogWarning("Unregistered institution folder: {Folder}", path);
        foreach (var institution in institutions)
        {
            try
            {
                foreach (var purpose in new[] { "IN", "OUT", "ARCHIVED", "ERROR" }) Directory.CreateDirectory(store.Folder(institution.Code, purpose));
                var inbound = store.Folder(institution.Code, "IN");
                foreach (var path in Directory.EnumerateFiles(inbound))
                {
                    var name = Path.GetFileName(path);
                    var marker = name.LastIndexOf(".processing.", StringComparison.OrdinalIgnoreCase);
                    if (marker < 0 && !name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
                    FileInfo info;
                    try { info = new FileInfo(path); } catch (IOException) { continue; }
                    if (!_observations.TryGetValue(path, out var old) || old.Size != info.Length || old.Modified != info.LastWriteTimeUtc)
                    { _observations[path] = (info.Length, info.LastWriteTimeUtc, 1); continue; }
                    _observations[path] = (info.Length, info.LastWriteTimeUtc, old.Stable + 1);
                    if (old.Stable < 1) continue;
                    Guid id;
                    string claimed;
                    if (marker >= 0)
                    {
                        if (!Guid.TryParseExact(name[(marker + ".processing.".Length)..], "N", out id))
                        { logger.LogWarning("Unrecognized claim filename {Path}", path); continue; }
                        claimed = path;
                    }
                    else
                    {
                        id = Guid.NewGuid();
                        claimed = path + ".processing." + id.ToString("N");
                        try { File.Move(path, claimed); } catch (IOException) { continue; }
                    }
                    if (!TryClaim(id)) continue;
                    try
                    {
                        var original = name[..(marker >= 0 ? marker : name.Length)];
                        var prefix = id.ToString("N") + "__";
                        var channel = original.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? "Upload" : "Poll";
                        if (channel == "Upload") original = original[prefix.Length..];
                        await store.ImportClaimed(id, institution.Id, original, claimed, channel);
                    }
                    catch (Exception e) { logger.LogError(e, "Receipt {ReceiptId} remains claimed for retry", id); }
                    finally { Release(id); _observations.Remove(path); }
                }
            }
            catch (Exception e) { logger.LogError(e, "Could not poll {Institution}", institution.Code); }
        }
    }
}
