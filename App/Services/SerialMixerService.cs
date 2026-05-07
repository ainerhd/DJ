using System.IO.Ports;
using System.Text;

namespace AudioMixerController.App;

public sealed class SerialMixerService : IDisposable
{
    private readonly LogStore _log;
    private readonly MixerFrameParser _parser = new();
    private readonly StringBuilder _buffer = new();
    private CancellationTokenSource? _cts;
    private SerialPort? _port;

    public event Action<MixerFrame>? FrameReceived;
    public event Action<string>? StatusChanged;

    public SerialMixerService(LogStore log)
    {
        _log = log;
    }

    public bool IsConnected => _port?.IsOpen == true;

    public IReadOnlyList<string> GetPortNames() => SerialPort.GetPortNames().OrderBy(name => name).ToArray();

    public async Task<string?> AutoConnectAsync(int baudRate, int handshakeTimeoutMs = 2200)
    {
        foreach (var portName in GetPortNames())
        {
            if (await TryConnectWithHandshakeAsync(portName, baudRate, handshakeTimeoutMs))
            {
                return portName;
            }
        }

        return null;
    }

    public async Task<bool> TryConnectWithHandshakeAsync(string portName, int baudRate, int handshakeTimeoutMs = 2200)
    {
        Disconnect();

        _port = new SerialPort(portName, baudRate)
        {
            Encoding = Encoding.ASCII,
            NewLine = "\n",
            ReadTimeout = 250,
            WriteTimeout = 250,
            DtrEnable = true,
            RtsEnable = true
        };

        try
        {
            await Task.Run(() => _port.Open());
            var ready = await WaitForHandshakeAsync(_port, handshakeTimeoutMs);
            if (!ready)
            {
                _log.Add($"Handshake failed on {portName}");
                Disconnect();
                return false;
            }

            _cts = new CancellationTokenSource();
            StatusChanged?.Invoke($"Connected {portName} @ {baudRate}");
            _log.Add($"Serial open {portName} @ {baudRate}");
            _ = Task.Run(() => ReadLoopAsync(_cts.Token));
            return true;
        }
        catch
        {
            Disconnect();
            throw;
        }
    }

    public async Task ConnectAsync(string portName, int baudRate)
    {
        var success = await TryConnectWithHandshakeAsync(portName, baudRate);
        if (!success)
        {
            throw new InvalidOperationException("Handshake failed");
        }
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_port is null)
        {
            return;
        }

        try
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }
        }
        catch (Exception ex)
        {
            _log.Add($"Serial close error: {ex.Message}");
        }

        _port.Dispose();
        _port = null;
        StatusChanged?.Invoke("Disconnected");
        _log.Add("Serial closed");
    }

    public Task SendAsync(string message)
    {
        if (_port is null || !_port.IsOpen)
        {
            return Task.CompletedTask;
        }

        _port.WriteLine(message);
        return Task.CompletedTask;
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_port is null || !_port.IsOpen)
                {
                    await Task.Delay(100, token);
                    continue;
                }

                var chunk = _port.ReadExisting();
                if (!string.IsNullOrEmpty(chunk))
                {
                    AppendChunk(chunk);
                }
                else
                {
                    await Task.Delay(10, token);
                }
            }
            catch (TimeoutException)
            {
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Add($"Serial read error: {ex.Message}");
                StatusChanged?.Invoke($"Read error: {ex.Message}");
                ClosePortAfterReadFailure();
                break;
            }
        }
    }

    private void ClosePortAfterReadFailure()
    {
        try
        {
            _port?.Close();
        }
        catch (Exception ex)
        {
            _log.Add($"Serial failure close error: {ex.Message}");
        }

        try
        {
            _port?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Add($"Serial failure dispose error: {ex.Message}");
        }

        _port = null;
        StatusChanged?.Invoke("Disconnected");
    }

    private void AppendChunk(string chunk)
    {
        List<MixerFrame>? frames = null;
        List<string>? rawLines = null;

        lock (_buffer)
        {
            _buffer.Append(chunk);

            while (true)
            {
                var text = _buffer.ToString();
                var newlineIndex = text.IndexOf('\n');
                if (newlineIndex < 0)
                    break;

                var line = text[..newlineIndex].Trim('\r');
                _buffer.Clear();
                if (newlineIndex + 1 < text.Length)
                    _buffer.Append(text[(newlineIndex + 1)..]);

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (string.Equals(line, "MIXER_READY", StringComparison.OrdinalIgnoreCase))
                {
                    _log.Add("Handshake confirmed");
                    continue;
                }

                if (_parser.TryParse(line, out var frame) && frame is not null)
                {
                    frames ??= new List<MixerFrame>();
                    frames.Add(frame);
                }
                else
                {
                    rawLines ??= new List<string>();
                    rawLines.Add(line);
                }
            }
        }

        if (rawLines is not null)
            foreach (var raw in rawLines)
                _log.Add($"Raw: {raw}");

        if (frames is not null)
            foreach (var frame in frames)
                FrameReceived?.Invoke(frame);
    }

    public void Dispose() => Disconnect();

    private static async Task<bool> WaitForHandshakeAsync(SerialPort port, int timeoutMs)
    {
        var start = Environment.TickCount64;
        var buffer = new StringBuilder();
        var lastHello = 0L;

        try
        {
            port.DiscardInBuffer();
        }
        catch
        {
        }

        while (Environment.TickCount64 - start < timeoutMs)
        {
            var now = Environment.TickCount64;
            if (now - lastHello >= 250)
            {
                port.WriteLine("HELLO_MIXER");
                lastHello = now;
            }

            var chunk = port.ReadExisting();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                buffer.Append(chunk);
                var text = buffer.ToString();
                if (text.Contains("MIXER_READY", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (text.Contains("MIXER,", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (buffer.Length > 4096)
                {
                    buffer.Remove(0, 2048);
                }
            }

            await Task.Delay(25);
        }

        return false;
    }
}
