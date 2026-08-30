using BepInEx.Logging;
using System;
using Utilla;
using Utilla.Behaviours;
using Utilla.Models;

namespace Translatilla.Plugin;

public static class UtillaPatches
{
    public static Utilla.Plugin UtillaInstance = null!;

    public static void Apply(bool isMasterLibrary, bool stopConductBoard = true)
    {
        // We leave it completely unpatched when it's the master library.
        if (isMasterLibrary)
            return;

        if (stopConductBoard)
        {
            PatchManager.KillMethod(typeof(VersionCheckManager), nameof(VersionCheckManager.Start));
            PatchManager.KillMethod(typeof(VersionCheckManager), nameof(VersionCheckManager.CheckVersion));
        }

        PatchManager.ApplyConstructorPatch(typeof(Utilla.Plugin),
            prefix: PatchManager.StopExecution,
            finalizer: PatchManager.GetMethodInfo(PluginConstructorFinalizer)
        );

        // Surprisingly, this is the only patch we need to completely fix GameModeUtils to GL.
        // Basically all the helper funcs rely on GetGamemode() internally so just translating GL
        // types to Utilla types completely resolves the issues.
        PatchManager.ApplyPatch(
            typeof(Utilla.Utils.GameModeUtils), nameof(Utilla.Utils.GameModeUtils.GetGamemode),
            prefix: PatchManager.GetMethodInfo(GetGamemodePrefix)
        );
    }

    // Utilla/Plugin.cs
    static void PluginConstructorFinalizer(Utilla.Plugin __instance)
    {
        var instanceType = __instance.GetType();

        Utilla.Plugin.Logger = Logger.CreateLogSource("Utilla/Translatilla");
        Utilla.Events.GameInitialized += OnGameInitialized;

        UtillaInstance = __instance;
    }

    static void OnGameInitialized(object sender, EventArgs args)
    {
        Plugin.UtillaGameObject = new UnityEngine.GameObject(
            $"Utilla/Translatilla {Utilla.PluginInfo.Version}",
            typeof(UtillaNetworkController),
            typeof(GamemodeManager),
            typeof(VersionCheckManager)
        );

        Plugin.DontDestroyOnLoad(Plugin.UtillaGameObject);
    }

    // Utilla/Utils/GameModeUtils.cs
    static bool GetGamemodePrefix(Func<Gamemode, bool> predicate, ref Gamemode __result)
    {
        var gmWrapperResult = GorillaLibrary.Utilities.GameModeUtility.GetGameMode(gm =>
        {
            var utillaGamemode = new Gamemode(
                gm.ID,
                gm.DisplayName,
                gm.BaseGameMode
            );

            return predicate(utillaGamemode);
        });

        if (gmWrapperResult is null)
        {
            __result = null!;
            return false;
        }

        var utillaGamemode = new Gamemode(
            gmWrapperResult.ID,
            gmWrapperResult.DisplayName,
            gmWrapperResult.BaseGameMode
        );

        __result = utillaGamemode;

        return false;
    }
}
