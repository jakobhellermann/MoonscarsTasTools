using ModdingAPI;
using MoonscarsTASTools.TASTools;
using uTas.Communication;

namespace MoonscarsTASTools.TasTools;

public class TasCommunicationClient : TasCommunicationClientBase {
    private const int Port = 34729;
    private const int RetryInterval = 5000;

    private TasController _tasController;

    public TasCommunicationClient(TasController tasController) : base(Port, RetryInterval) {
        _tasController = tasController;
        _tasController.TasCommunicationClient = this;
    }


    protected override void OnKeybindTriggered(TasKeybind keybind) {
        _tasController.OnEditorKeybind(keybind);
    }

    protected override void OnSendPath(string? path) {
        _tasController.OnPathChanged(path);
    }


    protected override void Log(string msg) {
        Logger.Log(msg);
    }
}