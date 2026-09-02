using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Translatilla.Plugin;

[BepInPlugin(Constants.Guid, Constants.Name, Constants.Version)]
[BepInDependency("org.legoandmars.gorillatag.utilla", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("dev.gorillalibrary", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
    internal static ConfigEntry<MasterLibrary> masterLibrary = null!;

    internal static new ManualLogSource Logger = null!;

    public static GameObject UtillaGameObject = null!;
    public static GameObject GorillaLibraryGameObject = null!;

    public enum MasterLibrary
    {
        Utilla,
        GorillaLibrary
    }

    public static Stopwatch stopwatch = null!;

    public Plugin()
    {
        Logger = base.Logger;

        var cfg = new ConfigFile(Path.Combine(Paths.ConfigPath, "Translatilla.cfg"), false);

        masterLibrary = cfg.Bind(
            "Patching", "MasterLibrary",
            MasterLibrary.GorillaLibrary,
            "The library that handles modded functions."
        );
        cfg.Save();

        var glInfo = Chainloader.PluginInfos["dev.gorillalibrary"].Metadata;
        var utInfo = Chainloader.PluginInfos["org.legoandmars.gorillatag.utilla"].Metadata;

        // Just to be safe; patching versions we haven't looked at could be risky and trigger AC if done wrong
        bool cancelPatch = false;

        if (glInfo.Version != Constants.GLVersion)
        {
            Logger.LogError("GorillaLibrary has not been patched for the latest version; initialization has been cancelled.");
            Logger.LogError($"GL patch version: {Constants.GLVersion} ; GL version: {glInfo.Version}");
            cancelPatch = true;
        }

        if (utInfo.Version != Constants.UtillaVersion)
        {
            Logger.LogError("Utilla has not been patched for the latest version; initialization has been cancelled.");
            Logger.LogError($"Utilla patch version: {Constants.UtillaVersion} ; Utilla version: {utInfo.Version}");
            cancelPatch = true;
        }

        if (cancelPatch)
        {
            Logger.LogError("Please create an issue on GitHub so we know that Utilla or GorillaLibrary has updated.");
            Logger.LogError("https://github.com/sirkingbinx/Translatilla");

            return;
        }

        bool glMaster = masterLibrary.Value == MasterLibrary.GorillaLibrary;
        bool utMaster = masterLibrary.Value == MasterLibrary.Utilla;

        Logger.LogMessage($"Master library: {masterLibrary.Value}");

        stopwatch = Stopwatch.StartNew();

        // stop either of the two from patching themselves (we will do it for you)
        GorillaLibraryPatches.Apply(glMaster);
        UtillaPatches.Apply(utMaster);

        stopwatch.Stop();

        Logger.LogMessage($"Patches complete in {stopwatch.ElapsedMilliseconds}ms");
    }

#if DEBUG
    /**
     * helpful Debug GUI for testing features
     */

    private bool showDebug = true;

    private readonly Queue<string> logQueue = new Queue<string>();
    private const int logMessageCount = 30;

    private static Rect windowRect = new Rect(10, 10, 600, 500);

    private static Vector2 logMessagesScrollPosition = Vector2.zero;

    void HandleLogMessage(string logString, string stackTrace, LogType type)
    {
        logQueue.Enqueue($"[{type}] {logString}");

        if (logQueue.Count > logMessageCount)
            logQueue.Dequeue();
    }


    private void Start()
    {
        Application.logMessageReceived += HandleLogMessage;
    }

    private void Update()
    {
        if (Keyboard.current.f8Key.wasPressedThisFrame)
            showDebug = !showDebug;
    }

    private void OnGUI()
    {
        if (!showDebug) return;
        windowRect = GUI.Window(93, windowRect, DrawWindow, "Translatilla", GUI.skin.box);
    }

    private void DrawWindow(int _)
    {
        GUI.Label(new Rect(0, 0, windowRect.width, 20), $"Translatilla Debug {Constants.Version} [F8]");

        // log messages
        logMessagesScrollPosition = GUI.BeginScrollView(
            new Rect(10, 30, windowRect.width - 20, 300),
            logMessagesScrollPosition,
            new Rect(0, 0, windowRect.width - 20, 16 * logMessageCount)
        );

        foreach (string logMessage in logQueue)
            GUILayout.Label(logMessage, GUILayout.ExpandWidth(true));

        GUI.EndScrollView(); 

        GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));
    }
#endif
}
