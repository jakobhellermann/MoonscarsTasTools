using System;
using System.IO;
using System.Linq;
using TasFormat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using Logger = ModdingAPI.Logger;

namespace MoonscarsTASTools.TasTools;

public class UnityTasPlayer {
    private const int TasFramerate = 50;

    public event Action? OnTasFinished;
    public event Action<TasLine.FrameInput, int, int>? OnAdvance;
    public event Action<TasLine.Breakpoint>? OnBreakpointHit;

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

        Logger.Log("registered onBeforeInput");
        InputSystem.onBeforeUpdate += OnBeforeUpdate;
    }

    public void Disable() {
        InputSystem.onBeforeUpdate -= OnBeforeUpdate;
    }

    public void Start() {
        _shouldRun = true;

        _previousApplicationTargetFramerate = Application.targetFrameRate;
        Application.targetFrameRate = TasFramerate;
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
                    OnBreakpointHit?.Invoke(breakpoint);
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