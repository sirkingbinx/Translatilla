using BepInEx;
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
        masterLibrary = Config.Bind(
            "Patching", "MasterLibrary",
            MasterLibrary.Utilla,
            "The library that handles modded functions."
        );

        Logger = base.Logger;
    }
}
