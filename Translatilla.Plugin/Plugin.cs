using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Translatilla.Plugin;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
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

        if (glInfo.Version != PluginInfo.GLVersion)
        {
            Logger.LogError("GorillaLibrary has not been patched for the latest version; initialization has been cancelled.");
            Logger.LogError($"GL patch version: {PluginInfo.GLVersion} ; GL version: {glInfo.Version}");
            cancelPatch = true;
        }

        if (utInfo.Version != PluginInfo.UtillaVersion)
        {
            Logger.LogError("Utilla has not been patched for the latest version; initialization has been cancelled.");
            Logger.LogError($"Utilla patch version: {PluginInfo.UtillaVersion} ; Utilla version: {utInfo.Version}");
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
}
