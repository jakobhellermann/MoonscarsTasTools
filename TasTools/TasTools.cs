using System;
using System.Reflection;
using JetBrains.Annotations;
using ModdingAPI;
using MoonscarsTASTools.TasTools;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MoonscarsTASTools.TASTools;

[UsedImplicitly]
internal class TasTools : Mod {
    public override string GetName() => Assembly.GetExecutingAssembly().GetName().Name;

    public override string Version() => Assembly.GetExecutingAssembly().GetName().Version.ToString();


    private TasCommunicationClient _tasCommunicationClient = null!;

    private GameObject _debugmodGameObject = null!;
    private TasController _tasController = null!;

    private InputActionMap _keybindings = null!;

    public override void Load() {
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

        _debugmodGameObject = new GameObject();
        Object.DontDestroyOnLoad(_debugmodGameObject);
        _tasController = _debugmodGameObject.AddComponent<TasController>();

        _keybindings = GetKeybindings();

        _keybindings.Enable();

        _tasCommunicationClient = new TasCommunicationClient(_tasController);
        _tasCommunicationClient.StartConnectionLoop();
    }

    public override void Unload() {
        _keybindings.Dispose();
        Object.Destroy(_debugmodGameObject);

        _tasCommunicationClient.Dispose();
    }


    private InputActionMap GetKeybindings() {
        var map = new InputActionMap("TASTools");
        AddButtonAction(map, "PauseResume", "numpadEnter", _tasController.PauseResume);
        AddButtonAction(map, "Advance", "numpadPlus", _tasController.Advance);
        AddButtonAction(map, "StartStop", "numpadMinus", _tasController.RestartTas);

        return map;
    }

    private static void AddButtonAction(InputActionMap map, string name, string key, Action performed) {
        var action = map.AddAction(name, InputActionType.Button, $"<Keyboard>/{key}");
        action.performed += _ => performed();
    }
}