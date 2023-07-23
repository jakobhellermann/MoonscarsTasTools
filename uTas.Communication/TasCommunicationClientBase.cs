using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ModdingAPI;

namespace MoonscarsTASTools.uTas.Communication;

public abstract class TasCommunicationClientBase : IDisposable {
    private SemaphoreSlim _sendMutex = new(1);
    private SemaphoreSlim _recvMutex = new(1);

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;

    protected CancellationToken CancellationToken = CancellationToken.None;

    protected bool Connected => _tcpClient is { Connected: true };

    protected async Task ConnectAsync(IPAddress address, int port) {
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(address, port);
        _stream = _tcpClient.GetStream();
    }

    protected async Task Start() {
        if (_tcpClient is null || !_tcpClient.Connected)
            throw new Exception("attempted to Start unconnected TasCommClient");

        try {
            while (_tcpClient.Connected && !CancellationToken.IsCancellationRequested) {
                var (opcode, data) = await Recv();
                try {
                    HandleMessage(opcode, data);
                } catch (Exception e) {
                    Logger.Log($"Failed to handle message: {e}");
                }
            }
        } catch (Exception e) {
            // for some reason the ReadAsync in ReadAsyncExactly doesn't get cancelle
            if (e.InnerException is SocketException { SocketErrorCode: SocketError.OperationAborted } &&
                CancellationToken.IsCancellationRequested)
                return;

            Logger.Log($"error receiving from server: {e}");
        }
    }


    public void Dispose() {
        _stream?.Dispose();
        _tcpClient?.Dispose();
    }


    protected async Task Send(byte opcode, byte[] data) {
        if (_stream is null) throw new Exception("attempted to Send into unconnected TasCommClient");

        await _sendMutex.WaitAsync(CancellationToken);

        try {
            var lengthBytes = BitConverter.GetBytes((uint)data.Length);
            Array.Reverse(lengthBytes);
            var header = new[] { opcode, lengthBytes[0], lengthBytes[1], lengthBytes[2], lengthBytes[3] };
            await _stream.WriteAsync(header, 0, header.Length, CancellationToken);
            await _stream.WriteAsync(data, 0, data.Length, CancellationToken);
        } finally {
            _sendMutex.Release();
        }
    }

    protected async Task Send(byte opcode) {
        await Send(opcode, new byte[] { });
    }

    private async Task<(byte, byte[])> Recv() {
        if (_stream is null) throw new Exception("attempted to Recv from unconnected TasCommClient");

        await _recvMutex.WaitAsync(CancellationToken);
        try {
            var headerBuffer = await _stream.ReadExactlyAsync(5, CancellationToken);
            var opcode = headerBuffer[0];
            Array.Reverse(headerBuffer);
            var length = BitConverter.ToUInt32(headerBuffer, 0);
            var buffer = await _stream.ReadExactlyAsync((int)length, CancellationToken);

            return (opcode, buffer);
        } finally {
            _recvMutex.Release();
        }
    }

    protected abstract void HandleMessage(byte opcode, byte[] data);
}

public static class StreamExtensions {
    public static async Task<byte[]> ReadExactlyAsync(this Stream stream, int count,
        CancellationToken cancellationToken) {
        var buffer = new byte[count];
        var totalBytesRead = 0;

        while (totalBytesRead < count) {
            cancellationToken.ThrowIfCancellationRequested();
            var bytesRead = await stream.ReadAsync(buffer, totalBytesRead, count - totalBytesRead, cancellationToken);

            if (bytesRead == 0)
                throw new EndOfStreamException("End of stream reached before reading the required number of bytes.");

            totalBytesRead += bytesRead;
        }

        return buffer;
    }
}