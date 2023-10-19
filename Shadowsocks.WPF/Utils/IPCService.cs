using System;
using System.IO.Pipes;
using System.Net;
using System.Text;

namespace Shadowsocks.WPF.Utils;

internal class RequestAddUrlEventArgs(string url) : EventArgs
{
    public readonly string Url = url;
}

internal class IpcService
{
    private const int INT32_LEN = 4;
    private const int OP_OPEN_URL = 1;
    private static readonly string _pipePath = $"Shadowsocks\\{Utilities.ExecutablePath.GetHashCode()}";

    public event EventHandler<RequestAddUrlEventArgs>? OpenUrlRequested;

    public async void RunServer()
    {
        var buf = new byte[4096];
        while (true)
        {
            await using var stream = new NamedPipeServerStream(_pipePath);
            await stream.WaitForConnectionAsync();
            await stream.ReadAsync(buf, 0, INT32_LEN);
            var opcode = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
            if (opcode == OP_OPEN_URL)
            {
                await stream.ReadAsync(buf, 0, INT32_LEN);
                var strlen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));

                await stream.ReadAsync(buf, 0, strlen);
                var url = Encoding.UTF8.GetString(buf, 0, strlen);

                OpenUrlRequested?.Invoke(this, new RequestAddUrlEventArgs(url));
            }
            stream.Close();
        }
    }

    private static (NamedPipeClientStream, bool) TryConnect()
    {
        var pipe = new NamedPipeClientStream(_pipePath);
        bool exist;
        try
        {
            pipe.Connect(10);
            exist = true;
        }
        catch (TimeoutException)
        {
            exist = false;
        }
        return (pipe, exist);
    }

    public static bool AnotherInstanceRunning()
    {
        var (pipe, exist) = TryConnect();
        pipe.Dispose();
        return exist;
    }

    public static void RequestOpenUrl(string url)
    {
        var (pipe, exist) = TryConnect();
        if (!exist) return;
        var opAddUrl = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(OP_OPEN_URL));
        pipe.Write(opAddUrl, 0, INT32_LEN); // opcode addurl
        var b = Encoding.UTF8.GetBytes(url);
        var blen = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(b.Length));
        pipe.Write(blen, 0, INT32_LEN);
        pipe.Write(b, 0, b.Length);
        pipe.Close();
        pipe.Dispose();
    }
}