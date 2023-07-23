using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ModdingAPI;
using MoonscarsTASTools.TASTools;
using MoonscarsTASTools.uTas.Communication;

namespace MoonscarsTASTools.TasTools;

public class TasCommunicationClient : TasCommunicationClientBase {
    private const int Port = 34729;

    private TasController _tasController;

    private bool _started;

    public new bool Connected => _started && base.Connected;


    private CancellationTokenSource _cancellationTokenSource = new();

    public TasCommunicationClient(TasController tasController) {
        CancellationToken = _cancellationTokenSource.Token;
        _tasController = tasController;
        _tasController.TasCommunicationClient = this;
    }

    public void StartConnectionLoop() {
        Task.Run(RestartClient);
    }

    public void Restart() {
        _started = false;
        Dispose();
        StartConnectionLoop();
    }

    private async Task RestartClient() {
        if (CancellationToken.IsCancellationRequested) return;

        Dispose();

        Logger.Log($"Connecting to server at {Port}");
        try {
            await ConnectAsync(IPAddress.Loopback, Port);
            await base.Send((byte)ClientOpCode.EstablishConnection);

            _started = true;
            await Start();
            Restart();
        } catch (OperationCanceledException) {
        } catch (Exception e) {
            Logger.LogError($"Could not connect, retrying in 5s: {e.Message}");
            await Task.Delay(5000, CancellationToken);
            await RestartClient();
        }
    }

    public void Cancel() => _cancellationTokenSource.Cancel();

    public async Task Send(ClientOpCode opcode, byte[] message) {
        if (!Connected) throw new Exception("attempted to send into unconnected clienet");

        try {
            await base.Send((byte)opcode, message);
        } catch (IOException exception) {
            Logger.LogError($"IO Exception talking to server, restarting {exception}");
            Restart();
        }
    }

    public async Task Send(ClientOpCode opcode, string message) {
        await base.Send((byte)opcode, Encoding.UTF8.GetBytes(message));
    }

    public async Task Send(ClientOpCode opcode) {
        await base.Send((byte)opcode, Array.Empty<byte>());
    }


    protected override void HandleMessage(byte opcodeByte, byte[] data) {
        var opcode = (ServerOpCode)opcodeByte;

        switch (opcode) {
            case ServerOpCode.KeybindTriggered:
                var keybind = (TasKeybind)data[0];
                _tasController.OnEditorKeybind(keybind);
                break;
            case ServerOpCode.SendPath:
                var path = Encoding.UTF8.GetString(data);
                _tasController.OnPathChanged(path == "" ? null : path);
                break;
            default:
                Logger.LogError($"Unexpected opcode: {opcodeByte}");
                throw new ArgumentOutOfRangeException();
        }
    }
}