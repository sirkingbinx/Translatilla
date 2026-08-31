using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
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

    private void Awake()
    {
        Logger = base.Logger;

        masterLibrary = Config.Bind(
            "Patching", "MasterLibrary",
            MasterLibrary.Utilla,
            "The library that handles modded functions."
        );

        var glInfo = Chainloader.PluginInfos["dev.gorillalibrary"].Metadata;
        var utInfo = Chainloader.PluginInfos["org.legoandmars.gorillatag.utilla"].Metadata;

        if (glInfo.Version != Constants.GLVersion) {
            Logger.LogError("GorillaLibrary has not been patched for the latest version; initialization has been cancelled.");
            Logger.LogError($"GL patch version: {Constants.GLVersion} ; GL version: {glInfo.Version}");
            return;
        }

        if (utInfo.Version != Constants.UtillaVersion) {
            Logger.LogError("Utilla has not been patched for the latest version; initialization has been cancelled.");
            Logger.LogError($"Utilla patch version: {Constants.UtillaVersion} ; Utilla version: {utInfo.Version}");
            return;
        }

        bool glMaster = masterLibrary.Value == MasterLibrary.GorillaLibrary;
        bool utMaster = masterLibrary.Value == MasterLibrary.Utilla;

        GorillaLibraryPatches.Apply(glMaster);
        UtillaPatches.Apply(utMaster);
    }
}
