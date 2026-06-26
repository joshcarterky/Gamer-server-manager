using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using GameServerManager.Core.Models;

namespace GameServerManager.Services;

// ── Line model ───────────────────────────────────────────────────────────────

public enum ConsoleLineLevel { System, Info, Warning, Error, Rcon }

public sealed record ConsoleLineEntry(DateTime Timestamp, string Text, ConsoleLineLevel Level);

// ── Source RCON client (proper wire format) ──────────────────────────────────

public sealed class SourceRconClient : IDisposable
{
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private int _nextId = 1;

    public bool IsConnected => _tcp?.Connected ?? false;

    public async Task<bool> ConnectAsync(string host, int port, string password,
        int timeoutMs = 4000, CancellationToken ct = default)
    {
        try
        {
            _tcp = new TcpClient { ReceiveTimeout = timeoutMs, SendTimeout = timeoutMs };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await _tcp.ConnectAsync(host, port, cts.Token);
            _stream = _tcp.GetStream();

            // AUTH packet (type 3)
            int authId = _nextId++;
            await WritePacketAsync(authId, 3, password);
            var resp = await ReadPacketAsync();
            // Auth failure: server echoes ID=-1 or sends type-2 with empty body
            return resp.id != -1;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> ExecAsync(string command, CancellationToken ct = default)
    {
        if (_stream == null) throw new InvalidOperationException("Not connected.");
        int id = _nextId++;
        await WritePacketAsync(id, 2, command);
        var resp = await ReadPacketAsync(ct);
        return resp.body;
    }

    private async Task WritePacketAsync(int id, int type, string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        int size = 4 + 4 + bodyBytes.Length + 2; // id + type + body + 2 nulls
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        bw.Write(size);
        bw.Write(id);
        bw.Write(type);
        bw.Write(bodyBytes);
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Flush();
        var data = ms.ToArray();
        await _stream!.WriteAsync(data);
    }

    private async Task<(int id, int type, string body)> ReadPacketAsync(CancellationToken ct = default)
    {
        var buf = new byte[4];
        await ReadExactAsync(buf, 4, ct);
        int size = BitConverter.ToInt32(buf, 0);
        var payload = new byte[size];
        await ReadExactAsync(payload, size, ct);
        int id = BitConverter.ToInt32(payload, 0);
        int type = BitConverter.ToInt32(payload, 4);
        int bodyLen = size - 4 - 4 - 2;
        string body = bodyLen > 0 ? Encoding.UTF8.GetString(payload, 8, bodyLen) : string.Empty;
        return (id, type, body);
    }

    private async Task ReadExactAsync(byte[] buf, int count, CancellationToken ct = default)
    {
        int read = 0;
        while (read < count)
        {
            int n = await _stream!.ReadAsync(buf.AsMemory(read, count - read), ct);
            if (n == 0) throw new EndOfStreamException("RCON connection closed.");
            read += n;
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _tcp?.Dispose();
    }
}

// ── Per-server console session ────────────────────────────────────────────────

public sealed class ConsoleSession : IDisposable
{
    private readonly ServerProfile _profile;
    private readonly ServerProcessService _processService;
    private readonly string _logPath;

    private FileSystemWatcher? _watcher;
    private long _filePosition;
    private bool _disposed;

    public event Action<ConsoleLineEntry>? LineReceived;

    public string ProfileId => _profile.Id;
    public bool IsRunning => _processService.IsRunning(_profile);

    internal ConsoleSession(ServerProfile profile, ServerProcessService processService, string logPath)
    {
        _profile = profile;
        _processService = processService;
        _logPath = logPath;
    }

    // ── Initial load ─────────────────────────────────────────────────────────

    public IReadOnlyList<ConsoleLineEntry> GetInitialLines(int max = 500)
    {
        if (!File.Exists(_logPath)) return [];
        try
        {
            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
            var all = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
                all.Add(line);
            _filePosition = fs.Position;
            int start = Math.Max(0, all.Count - max);
            return all.Skip(start).Select(l => ParseLine(l)).ToList();
        }
        catch
        {
            return [];
        }
    }

    // ── File tail ────────────────────────────────────────────────────────────

    public void StartWatching()
    {
        if (_disposed) return;
        StopWatching();
        var dir = Path.GetDirectoryName(_logPath);
        if (dir == null) return;
        Directory.CreateDirectory(dir);
        _watcher = new FileSystemWatcher(dir, Path.GetFileName(_logPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
    }

    public void StopWatching()
    {
        if (_watcher is null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnFileChanged;
        _watcher.Created -= OnFileChanged;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnFileChanged(object _, FileSystemEventArgs e) => ReadNewLines();

    private void ReadNewLines()
    {
        if (!File.Exists(_logPath)) return;
        try
        {
            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < _filePosition) _filePosition = 0; // truncated/rotated
            fs.Seek(_filePosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            string? line;
            while ((line = reader.ReadLine()) != null)
                LineReceived?.Invoke(ParseLine(line));
            _filePosition = fs.Position;
        }
        catch { }
    }

    // ── Command sending ──────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> SendViaStdinAsync(string command)
    {
        if (!_processService.TryGetProcess(_profile, out var process) || process == null)
            return (false, "Server process not found.");
        try
        {
            // StandardInput is only accessible on processes we started with redirection
            await process.StandardInput.WriteLineAsync(command);
            await process.StandardInput.FlushAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error, string Response)> SendViaRconAsync(
        string host, int port, string password, string command)
    {
        using var rcon = new SourceRconClient();
        bool auth = await rcon.ConnectAsync(host, port, password);
        if (!auth) return (false, "RCON authentication failed — check the password and port.", string.Empty);
        try
        {
            string resp = await rcon.ExecAsync(command);
            return (true, null, resp);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, string.Empty);
        }
    }

    // ── Line parsing ─────────────────────────────────────────────────────────

    private static ConsoleLineEntry ParseLine(string raw)
    {
        var level = DetectLevel(raw);
        return new ConsoleLineEntry(DateTime.Now, raw, level);
    }

    private static ConsoleLineLevel DetectLevel(string text)
    {
        if (text.Contains("[ERR]", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("fatal", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("critical", StringComparison.OrdinalIgnoreCase))
            return ConsoleLineLevel.Error;

        if (text.Contains("[WARN]", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("warn:", StringComparison.OrdinalIgnoreCase))
            return ConsoleLineLevel.Warning;

        if (text.StartsWith("> ", StringComparison.Ordinal) ||
            text.Contains("[RCON]", StringComparison.OrdinalIgnoreCase))
            return ConsoleLineLevel.Rcon;

        if (text.Contains("[SYS]", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Starting ", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("[System]", StringComparison.OrdinalIgnoreCase))
            return ConsoleLineLevel.System;

        return ConsoleLineLevel.Info;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatching();
    }
}

// ── Factory ───────────────────────────────────────────────────────────────────

public sealed class ConsoleSessionService : IDisposable
{
    private readonly ServerProcessService _processService;
    private readonly AppDataPaths _paths;

    public ConsoleSessionService(ServerProcessService processService, AppDataPaths paths)
    {
        _processService = processService;
        _paths = paths;
    }

    public ConsoleSession CreateSession(ServerProfile profile)
    {
        var logPath = _processService.GetConsoleLogPath(profile);
        return new ConsoleSession(profile, _processService, logPath);
    }

    public void Dispose() { }
}
