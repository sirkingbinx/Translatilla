using BepInEx.Logging;
using GorillaLibrary.Behaviours;
using GorillaLibrary.Models;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
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

        Harmony.UnpatchID("org.legoandmars.gorillatag.utilla");

        if (stopConductBoard)
        {
            PatchManager.KillMethod(typeof(VersionCheckManager), nameof(VersionCheckManager.Start));
            PatchManager.KillMethod(typeof(VersionCheckManager), nameof(VersionCheckManager.CheckVersion));
        }

        // Surprisingly, this is the only patch we need to completely fix GameModeUtils to GL.
        // Basically all the helper funcs rely on GetGamemode() internally so just translating GL
        // types to Utilla types completely resolves the issues.
        PatchManager.ApplyPatch(
            typeof(Utilla.Utils.GameModeUtils), nameof(Utilla.Utils.GameModeUtils.GetGamemode),
            prefix: PatchManager.GetMethodInfo(GetGamemodePrefix)
        );

        // Killing the GamemodeManager since there is no need for its modded functionality under GL.
        PatchManager.ApplyPatch(
            typeof(GamemodeManager), nameof(GamemodeManager.Awake),
            prefix: PatchManager.GetMethodInfo(GamemodeManagerAwakePatch)
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

    // Utilla/Behaviours/GamemodeManager.cs
    static bool GamemodeManagerAwakePatch(GamemodeManager __instance)
    {
        GamemodeManager.Instance = __instance;

        Events.RoomJoined += __instance.OnRoomJoin;
        Events.RoomLeft += __instance.OnRoomLeft;

        // Translating Custom Game Modes
        /*
         * Under the hood, GL's modded lobby management is just Utilla's GamemodeManager so the process of feeding
         * custom gamemodes into GL is the exact same as it would be for Utilla.
         * 
         * All we have to do is translate the types from Utilla.Example.Type to GorillaLibrary.Example.Type since
         * .NET doesn't allow for mixing and matching functionally the same type across two namespaces that define
         * them.
         * 
         * Good news, it's stupid easy.
         * Bad news, we have to do it at all.
         */

        // PluginInfo also happens to hold metadata for Utilla itself, but this shouldn't be much of an issue since we
        // just ignore those values in reconstruction.

        // Utilla conveniently has a helper function for getting our plugin infos
        List<PluginInfo> utillaPluginInfos = __instance.GetPluginInfos();
        List<GorillaLibrary.PluginInfo> gorillaLibraryPluginInfos = new();

        foreach (var pluginInfo in utillaPluginInfos)
        {
            var glPluginInfo = new GorillaLibrary.PluginInfo
            {
                Gamemodes = pluginInfo.Gamemodes.Select(g => CreateGameModeWrapper(g)).ToArray(),
                Plugin = pluginInfo.Plugin,
                // None of the gamemode events take namespace-specific types, they take strings so no translation required
                OnGamemodeJoin = pluginInfo.OnGamemodeJoin,
                OnGamemodeLeave = pluginInfo.OnGamemodeLeave
            };

            gorillaLibraryPluginInfos.Add(glPluginInfo);
        }

        // Now just inject them into GorillaLibrary's gamemode mgr
        var gamemodes = GameModeManager.Instance.GetGamemodes(gorillaLibraryPluginInfos);
        gamemodes.ForEach(GameModeManager.Instance.AddGamemodeToPrefabPool);

        Plugin.Logger.Log($"Added {gamemodes.Count} gamemodes to GorillaLibrary game selector");

        // We don't need to kill the network controller since it's room update events are completely
        // harmless and rely on NetworkSystem. It also updates GameModeUtils but mostly irrelevant since it's
        // already patched.
        // 
        // Sorta redundant to let you know that the patches don't do something but felt like it'd be useful
        // to mention since a name like "UtillaNetworkController" implies that it is controlling networking
        // operations.

        Plugin.Logger.LogMessage($"Utilla {UtillaInstance.Info.Metadata.Version} should be patched and ready to work with GorillaLibrary.");
        Plugin.Logger.LogMessage($"If any crashes, bugs, or issues occur with either library, please fill out an issue on GitHub:");
        Plugin.Logger.LogMessage($"https://github.com/sirkingbinx/Translatilla");

        return false;
    }

    // Easy setup steps for GL. Just pass the arguments Utilla already created.
    private static GameModeWrapper CreateGameModeWrapper(Gamemode utillaGamemode) =>
        new GameModeWrapper(utillaGamemode.ID, utillaGamemode.DisplayName, utillaGamemode.GameManager);
}
