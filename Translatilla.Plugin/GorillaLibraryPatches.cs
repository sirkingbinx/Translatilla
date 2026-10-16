using BepInEx.Bootstrap;
using GorillaLibrary.Behaviours;
using GorillaLibrary.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Utilla.Behaviours;

namespace Translatilla.Plugin;

public static class GorillaLibraryPatches
{
    public static GorillaLibrary.Plugin GLInstance = null!;

    public static void Apply(bool isMasterLibrary, bool stopConductBoard = true)
    {
        GLInstance = (GorillaLibrary.Plugin)Chainloader.PluginInfos["dev.gorillalibrary"].Instance;

        PatchManager.ApplyPatch(typeof(NetworkController), nameof(NetworkController.OnRoomPropertiesUpdate),
            prefix: PatchManager.GetMethodInfo(OnRoomPropertiesUpdatePrefix));

        if (isMasterLibrary)
            return;

        PatchManager.RemoveLobbyPatches(typeof(GorillaLibrary.Plugin).Assembly);

        if (stopConductBoard)
        {
            PatchManager.KillMethod(typeof(ConductBoardManager), nameof(ConductBoardManager.Start));
            PatchManager.KillMethod(typeof(ConductBoardManager), nameof(ConductBoardManager.CheckVersion));
            PatchManager.KillMethod(typeof(ConductBoardManager), nameof(ConductBoardManager.DownloadEntries));
        }

        PatchManager.ApplyPatch(typeof(GameModeManager), nameof(GameModeManager.Awake),
            prefix: PatchManager.GetMethodInfo(GameModeManagerAwakePatch));
        PatchManager.ApplyPatch(typeof(GamemodeManager), "GetPluginInfos",
            postfix: PatchManager.GetMethodInfo(GetPluginInfosPostfix));
        PatchManager.ApplyPatch(typeof(GamemodeManager), nameof(GamemodeManager.Awake),
            postfix: PatchManager.GetMethodInfo(GamemodeManagerAwakePostfix));
        PatchManager.ApplyPatch(typeof(GorillaLibrary.Utilities.GameModeUtility), nameof(GorillaLibrary.Utilities.GameModeUtility.GetGameMode),
            prefix: PatchManager.GetMethodInfo(GetGameModePrefix));
        PatchManager.ApplyPatch(typeof(GorillaLibrary.Utilities.GameModeUtility), nameof(GorillaLibrary.Utilities.GameModeUtility.FindGameModeInString),
            prefix: PatchManager.GetMethodInfo(FindGameModeInStringPrefix));
        PatchManager.ApplyPatch(typeof(VersionCheckManager), nameof(VersionCheckManager.Start),
            postfix: PatchManager.GetMethodInfo(VersionCheckManagerStartPatch));
    }

    // GorillaLibrary/Behaviours/GameModeManager.cs
    static bool GameModeManagerAwakePatch(GameModeManager __instance)
    {
        if (GameModeManager.HasInstance && GameModeManager.Instance != __instance)
        {
            UnityEngine.Object.Destroy(__instance);
            return false;
        }

        GameModeManager.Instance = __instance;
        Plugin.GorillaLibraryGameObject = __instance.gameObject;

        __instance.Gamemodes = new();
        __instance.ModdedGamemodes = new();
        __instance.CustomGameModes = new();
        __instance.pluginInfos = new();

        GamemodeManagerAwakePostfix();
        return false;
    }

    static void GetPluginInfosPostfix(GamemodeManager __instance, ref List<Utilla.PluginInfo> __result)
    {
        var moddedGamemodes = __instance.ModdedGamemodes.Select(GameModeBridge.ToGorillaLibrary).ToArray();
        var infos = GameModeBridge.GetPluginInfos<GorillaLibrary.Attributes.ModdedGamemodeAttribute, GameModeWrapper>(
            attribute => attribute.gamemode, moddedGamemodes,
            typeof(GorillaLibrary.Attributes.ModdedGamemodeJoinAttribute), typeof(GorillaLibrary.Attributes.ModdedGamemodeLeaveAttribute));

        foreach (var info in infos)
        {
            __result.Add(new Utilla.PluginInfo
            {
                Plugin = info.Plugin,
                Gamemodes = info.Gamemodes.Select(GameModeBridge.ToUtilla).ToArray(),
                OnGamemodeJoin = info.OnGamemodeJoin,
                OnGamemodeLeave = info.OnGamemodeLeave
            });
        }

        GameModeBridge.Canonicalize(__result, __instance.ModdedGamemodes);
    }

    static void GamemodeManagerAwakePostfix()
    {
        if (!GameModeManager.HasInstance || !GamemodeManager.Initialization.Task.IsCompleted)
            return;

        var utillaGamemodes = GamemodeManager.Instance;
        var glGamemodes = GameModeManager.Instance;

        glGamemodes.Gamemodes = utillaGamemodes.Gamemodes.Select(GameModeBridge.ToGorillaLibrary).ToList();
        glGamemodes.ModdedGamemodes = utillaGamemodes.ModdedGamemodes.Select(GameModeBridge.ToGorillaLibrary).ToList();
        glGamemodes.CustomGameModes = utillaGamemodes.CustomGameModes.Select(GameModeBridge.ToGorillaLibrary).ToList();
        
        foreach (var mode in utillaGamemodes.DefaultGameModesPerMode)
            glGamemodes.DefaultGameModesPerMode[mode.Key] = GameModeBridge.ToGorillaLibrary(mode.Value);
        foreach (var mode in utillaGamemodes.ModdedGamemodesPerMode)
            glGamemodes.ModdedGamemodesPerMode[mode.Key] = GameModeBridge.ToGorillaLibrary(mode.Value);

        GameModeManager.Initialization.TrySetResult(glGamemodes);
    }

    static bool GetGameModePrefix(Func<GameModeWrapper, bool> predicate, ref GameModeWrapper __result)
    {
        var result = Utilla.Utils.GameModeUtils.GetGamemode(mode => predicate(GameModeBridge.ToGorillaLibrary(mode)));
        __result = result is null ? null! : GameModeBridge.ToGorillaLibrary(result);
        return false;
    }

    static bool FindGameModeInStringPrefix(string gmString, ref GameModeWrapper __result)
    {
        var result = Utilla.Utils.GameModeUtils.FindGamemodeInString(gmString);
        __result = result is null ? null! : GameModeBridge.ToGorillaLibrary(result);
        return false;
    }

    // Photon can send a room update before GL has recorded its first room
    static bool OnRoomPropertiesUpdatePrefix(NetworkController __instance)
    {
        if (ApplicationQuittingState.IsQuitting || NetworkSystem.Instance == null || !NetworkSystem.Instance.InRoom)
            return false;

        if (__instance.lastRoom is null)
        {
            __instance.OnJoinedRoom();
            return false;
        }

        return true;
    }

    static void VersionCheckManagerStartPatch(VersionCheckManager __instance)
    {
        if (__instance.footerText == null)
            return;

        if (__instance.footerText.text.Contains("<color=red>"))
            __instance.footerText.text += $"<color=red>TRANSLATILLA {PluginInfo.Version}</color> <color=grey>(U:{PluginInfo.UtillaVersion} / GL:{PluginInfo.GLVersion})</color>";
        else
            __instance.footerText.text += $"TRANSLATILLA {PluginInfo.Version} <color=grey>(U:{PluginInfo.UtillaVersion} / GL:{PluginInfo.GLVersion})</color>";
    }
}
