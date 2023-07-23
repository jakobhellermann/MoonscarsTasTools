using System;
using System.Threading.Tasks;
using MoonscarsTASTools.TasTools;
using MoonscarsTASTools.uTas.Communication;
using TasFormat;
using UnityEngine;
using Logger = ModdingAPI.Logger;

namespace MoonscarsTASTools.TASTools;

internal enum AdvanceState {
    Queued,
    Advancing,
    No
}

public class TasController : MonoBehaviour {
    public TasCommunicationClient TasCommunicationClient = null!; // initialized by Mod
    private UnityTasPlayer _player = null!;

    private bool _tasRunning;
    private string? _tasPath;

    private bool _gamePaused;
    private AdvanceState _advanceState = AdvanceState.No;


    private bool _canShutdown;

    private void Awake() {
        _player = new UnityTasPlayer();
        _player.OnAdvance += OnTasAdvanced;
        _player.OnTasFinished += OnTasFinished;
        _player.OnBreakpointHit += OnBreakpointHit;

        Application.wantsToQuit += () => {
            Task.Run(async () => {
                if (TasCommunicationClient.Connected) await TasCommunicationClient.Send(ClientOpCode.CloseConnection);
                TasCommunicationClient.Cancel();
                TasCommunicationClient.Dispose();
                _canShutdown = true;
                Application.Quit();
            });
            return _canShutdown;
        };
    }

    private void OnDestroy() {
        _player.Disable();
    }

    private void OnTasFinished() {
        _tasRunning = false;
        SendStudioStop();
    }

    private void OnTasAdvanced(TasLine.FrameInput frameInput, int lineNumber, int frameInsideLine) {
        _player.ActiveFrame++;

        var studioInfo = new StudioInfo(lineNumber, $"{frameInsideLine + 1}", _player.ActiveFrame, 0, 0, 0, "", "");
        Task.Run(async () => await TasCommunicationClient.Send(ClientOpCode.SetStudioInfo, studioInfo.ToByteArray()));
    }

    private void OnBreakpointHit(TasLine.Breakpoint breakpoint) {
    }


    public void RestartTas() {
        if (!_tasRunning) {
            if (_tasPath is null) {
                Logger.LogError("Cant start TAS without knowing the path");
                return;
            }

            StartTas();
        } else {
            StopTas();
            Resume();
        }

        SendStudioStop();
    }

    private void StartTas() {
        Logger.Log("Start");

        _player.ActiveFrame = 0;
        _tasRunning = true;

        _player.Start();
    }

    private void StopTas() {
        Logger.Log("Stop");
        _tasRunning = false;
        _player.ActiveFrame = 0;

        Time.captureFramerate = 0;

        _player.Stop();
    }


    private void SendStudioStop() {
        Task.Run(async () => {
            await TasCommunicationClient.Send(ClientOpCode.SetStudioInfo, StudioInfo.Invalid.ToByteArray());
        });
    }


    public void PauseResume() {
        if (_gamePaused)
            Resume();
        else
            Pause();
    }

    private void Pause() {
        if (_gamePaused) return;

        _player.Stop();

        Logger.Log("Pause");
        _gamePaused = true;
        SlowMoController.Instance.enabled = false;
        Time.timeScale = 0;
    }


    private void Resume() {
        if (!_gamePaused) return;

        _player.Start();

        Logger.Log("Resume");
        SlowMoController.Instance.enabled = true;
        Time.timeScale = 1;
        _gamePaused = false;
    }


    public void Advance() {
        Logger.Log("Advancing");
        Pause();

        _advanceState = AdvanceState.Queued;
    }

    private void LateUpdate() {
        switch (_advanceState) {
            case AdvanceState.Queued:
                Resume();
                _advanceState = AdvanceState.Advancing;
                break;
            case AdvanceState.Advancing:
                Pause();
                _advanceState = AdvanceState.No;
                break;
            case AdvanceState.No:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (TasCommunicationClient.Connected)
            Task.Run(async () => {
                if (SceneController.Instance is null) return;
                var infoString = SceneController.Instance.Player.transform.position.ToString();
                await TasCommunicationClient.Send(ClientOpCode.SetInfoString, infoString);
            });
    }

    public void OnEditorKeybind(TasKeybind keybind) {
        Logger.Log($"Got keybind {keybind}");
        switch (keybind) {
            case TasKeybind.StartStop:
                RestartTas();
                break;
            case TasKeybind.FrameAdvance:
                Advance();
                break;
            case TasKeybind.PauseResume:
                PauseResume();
                break;
            case TasKeybind.ToggleHitboxes:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(keybind), keybind, null);
        }
    }

    public void OnPathChanged(string? path) {
        if (path == null) {
            _tasPath = null;
            _player.TasPath = null;
            Logger.Log("Unset path");
            return;
        }

        Logger.Log($"Got path {path}");
        try {
            _player.TasPath = path;
            _tasPath = path;
        } catch (Exception e) {
            Logger.LogError($"Failed to parse TAS file: {e}");
        }
    }
}