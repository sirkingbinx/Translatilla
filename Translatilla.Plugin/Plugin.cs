using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Diagnostics;
using UnityEngine;

namespace Translatilla.Plugin;

[BepInPlugin(Constants.Name, Constants.Guid, Constants.Version)]
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

    private void Start()
    {
        Logger = base.Logger;

        masterLibrary = Config.Bind(
            "Patching", "MasterLibrary",
            MasterLibrary.Utilla,
            "The library that handles modded functions."
        );

        var glInfo = Chainloader.PluginInfos["dev.gorillalibrary"].Metadata;
        var utInfo = Chainloader.PluginInfos["org.legoandmars.gorillatag.utilla"].Metadata;

        // Just to be safe; patching versions we haven't looked at could be risky and trigger AC if done wrong
        bool cancelPatch = false;

        if (glInfo.Version != Constants.GLVersion) {
            Logger.LogError("GorillaLibrary has not been patched for the latest version; initialization has been cancelled.");
            Logger.LogError($"GL patch version: {Constants.GLVersion} ; GL version: {glInfo.Version}");
            cancelPatch = true;
        }

        if (utInfo.Version != Constants.UtillaVersion) {
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

        Plugin.Logger.LogMessage($"Translatilla v{Info.Metadata.Version}");
        Plugin.Logger.LogMessage($"Master Library: {masterLibrary.Value}");

        var stopwatch = Stopwatch.StartNew();

        GorillaLibraryPatches.Apply(glMaster);
        UtillaPatches.Apply(utMaster);

        stopwatch.Stop();

        Plugin.Logger.LogMessage($"Patches complete in {stopwatch.ElapsedMilliseconds}ms");
    }
}
