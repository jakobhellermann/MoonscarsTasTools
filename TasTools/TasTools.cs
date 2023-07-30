using System;
using System.Reflection;
using JetBrains.Annotations;
using ModdingAPI;
using MonoMod.RuntimeDetour;
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


    private static float GetUnscaledTime() => Time.time;

    private static float GetUnscaledDeltaTime() => Time.deltaTime * Time.timeScale;

    public override void Load() {
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

        var methodInfoUnscaledTime =
            typeof(Time).GetMethod("get_unscaledTime", BindingFlags.Static | BindingFlags.Public)!;
        var targetUnscaledTime =
            typeof(TasTools).GetMethod("GetUnscaledTime", BindingFlags.Static | BindingFlags.NonPublic)!;
        var unscaledTimeDetour = new NativeDetour(methodInfoUnscaledTime, targetUnscaledTime);

        var methodInfoUnscaledDeltaTime =
            typeof(Time).GetMethod("get_unscaledDeltaTime", BindingFlags.Static | BindingFlags.Public)!;
        var targetUnscaledDeltaTime =
            typeof(TasTools).GetMethod("GetUnscaledDeltaTime", BindingFlags.Static | BindingFlags.NonPublic)!;
        var unscaledDeltaTimeDetour = new NativeDetour(methodInfoUnscaledDeltaTime, targetUnscaledDeltaTime);

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