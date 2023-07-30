using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using uTas.TasFormat;
using Logger = ModdingAPI.Logger;

namespace MoonscarsTASTools.TasTools;

public class UnityTasPlayer {
    private const int TasFramerate = 50;

    public event Action? OnTasFinished;
    public event Action<TasLine.FrameInput, int, int>? OnAdvance;
    public event Action<bool>? OnBreakpointHit;

    public string? TasPath;
    public int ActiveFrame;

    private InputDevice _virtualKeyboard;

    private bool _shouldRun;


    private int _previousApplicationTargetFramerate;

    public UnityTasPlayer() {
        InputSystem.RegisterLayout(@"
               {
                   ""name"" : ""VirtualKeyboard"",
                   ""extend"" : ""Keyboard"",
                   ""device"" : {
                       ""interface"" : ""Virtual"",
                       ""deviceClass"" : ""Keyboard""
                   }
               }
            ");
        _virtualKeyboard = InputSystem.AddDevice(new InputDeviceDescription {
            interfaceName = "Virtual",
            deviceClass = "Keyboard"
        });
        _virtualKeyboard = InputSystem.AddDevice<Keyboard>("VirtualKeyboard");

        InputSystem.onBeforeUpdate += OnBeforeUpdate;
    }

    public void Disable() {
        InputSystem.onBeforeUpdate -= OnBeforeUpdate;
    }

    public void Start() {
        _shouldRun = true;

        var nextBreakpoint = GetNextBreakpoint();
        var speedupFactor = nextBreakpoint == null ? 1 : nextBreakpoint?.Factor;
        var targetFrameRate = speedupFactor switch {
            { } factor => (int)(TasFramerate * factor),
            null => -1
        };
        _previousApplicationTargetFramerate = Application.targetFrameRate;
        Application.targetFrameRate = targetFrameRate;

        Time.captureFramerate = TasFramerate;
    }

    public void Stop() {
        _shouldRun = false;

        Time.captureFramerate = 0;
        Application.targetFrameRate = _previousApplicationTargetFramerate;

        ClearInput();
    }

    private void OnFinish() {
        Stop();

        OnTasFinished?.Invoke();
    }

    private void ClearInput() {
        InputSystem.QueueStateEvent(_virtualKeyboard, new KeyboardState());
    }


    private TasLine.Breakpoint? GetNextBreakpoint() {
        if (TasPath is not { } path) return null;
        var file = TasFile.Parse(File.ReadAllText(path)); // TODO be more smart about this
        if (file.GetCursorStateAt(ActiveFrame) is not { } state) return null;

        return file.Lines.Skip(state.TasFileLineIndex)
            .Select(line => line.Line)
            .OfType<TasLine.Breakpoint>().FirstOrDefault();
    }


    private void OnBeforeUpdate() {
        if (!_shouldRun) return;

        try {
            if (TasPath is not { } path) return;
            var file = TasFile.Parse(File.ReadAllText(path));

            PrepareUpdate(file);
        } catch (Exception e) {
            Logger.LogError($"Failed to perform input: {e}");
        }
    }

    private void PrepareUpdate(TasFile tasFile) {
        if (tasFile.GetCursorStateAt(ActiveFrame) is not { } state) {
            OnFinish();
            return;
        }

        foreach (var other in state.Other)
            switch (other) {
                case TasLine.Breakpoint breakpoint:
                    var nextBreakpoint = GetNextBreakpoint();
                    OnBreakpointHit?.Invoke(nextBreakpoint == null);
                    break;
                case TasLine.Call:
                    SceneController.Instance.Player.transform.position = new Vector3(-247.98f, 6.25f, -1f);
                    break;
                case TasLine.Comment:
                    break;
                case TasLine.Property:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(other));
            }


        var inputs = state.Input.Inputs.Select(input => InputToKey(input.Key)).ToArray();
        var keyboardState = new KeyboardState(inputs);
        InputSystem.QueueStateEvent(_virtualKeyboard, keyboardState);

        OnAdvance?.Invoke(state.Input, state.LineNumber, state.FrameInsideLine);
    }


    private static Key InputToKey(string key) {
        return key switch {
            "L" => Key.A,
            "R" => Key.D,
            "U" => Key.W,
            "D" => Key.S,
            "J" => Key.Space,
            "X" => Key.LeftShift,
            "P" => Key.P,
            "A" => Key.K,
            "S" => Key.L,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not a valid input")
        };
    }
}