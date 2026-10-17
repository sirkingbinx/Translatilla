using GorillaLibrary.Behaviours;
using GorillaLibrary.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Utilla.Behaviours;
using Utilla.Models;

namespace Translatilla.Plugin;

public static class UtillaPatches
{
    public static Utilla.Plugin UtillaInstance = null!;

    public static void Apply(bool isMasterLibrary, bool stopConductBoard = true)
    {
        UtillaInstance = (Utilla.Plugin)BepInEx.Bootstrap.Chainloader.PluginInfos["org.legoandmars.gorillatag.utilla"].Instance;

        if (isMasterLibrary)
        {
            PatchManager.ApplyPatch(typeof(Utilla.Utils.GameModeUtils), nameof(Utilla.Utils.GameModeUtils.FindGamemodeInString),
                prefix: PatchManager.GetMethodInfo(FindMasterGamemodeInStringPrefix));
            return;
        }

        // The dependencies have already started by the time Translatilla loads. Keep their initialization
        // hooks, and only remove the patches that would compete with the master library.
        PatchManager.RemoveLobbyPatches(typeof(Utilla.Plugin).Assembly);

        if (stopConductBoard)
        {
            PatchManager.KillMethod(typeof(VersionCheckManager), nameof(VersionCheckManager.Start));
            PatchManager.KillMethod(typeof(VersionCheckManager), nameof(VersionCheckManager.CheckVersion));
        }

        PatchManager.ApplyPatch(typeof(GamemodeManager), nameof(GamemodeManager.Awake),
            prefix: PatchManager.GetMethodInfo(GamemodeManagerAwakePatch));
        PatchManager.ApplyPatch(typeof(GameModeManager), "GetPluginInfos",
            postfix: PatchManager.GetMethodInfo(GetPluginInfosPostfix));
        PatchManager.ApplyPatch(typeof(GameModeManager), nameof(GameModeManager.Awake),
            postfix: PatchManager.GetMethodInfo(GameModeManagerAwakePostfix));
        PatchManager.ApplyPatch(typeof(Utilla.Utils.GameModeUtils), nameof(Utilla.Utils.GameModeUtils.GetGamemode),
            prefix: PatchManager.GetMethodInfo(GetGamemodePrefix));
        PatchManager.ApplyPatch(typeof(Utilla.Utils.GameModeUtils), nameof(Utilla.Utils.GameModeUtils.FindGamemodeInString),
            prefix: PatchManager.GetMethodInfo(FindGamemodeInStringPrefix));
        PatchManager.ApplyPatch(typeof(ConductBoardManager), nameof(ConductBoardManager.Start),
            postfix: PatchManager.GetMethodInfo(ConductBoardManagerStartPatch));
    }

    // Utilla/Behaviours/GamemodeManager.cs
    static bool GamemodeManagerAwakePatch(GamemodeManager __instance)
    {
        if (GamemodeManager.HasInstance && GamemodeManager.Instance != __instance)
        {
            UnityEngine.Object.Destroy(__instance);
            return false;
        }

        GamemodeManager.Instance = __instance;
        Plugin.UtillaGameObject = __instance.gameObject;
        __instance.Gamemodes = new();
        __instance.ModdedGamemodes = new();
        __instance.CustomGameModes = new();
        __instance.pluginInfos = new();

        GameModeManagerAwakePostfix();
        return false;
    }

    // Feed Utilla plugins into GL before it registers prefabs or releases its selector initialization task.
    static void GetPluginInfosPostfix(GameModeManager __instance, ref List<GorillaLibrary.PluginInfo> __result)
    {
        var moddedGamemodes = __instance.ModdedGamemodes.Select(GameModeBridge.ToUtilla).ToArray();
        var infos = GameModeBridge.GetPluginInfos<Utilla.Attributes.ModdedGamemodeAttribute, Gamemode>(
            attribute => attribute.gamemode, moddedGamemodes,
            typeof(Utilla.Attributes.ModdedGamemodeJoinAttribute), typeof(Utilla.Attributes.ModdedGamemodeLeaveAttribute));

        foreach (var info in infos)
        {
            __result.Add(new GorillaLibrary.PluginInfo
            {
                Plugin = info.Plugin,
                Gamemodes = info.Gamemodes.Select(GameModeBridge.ToGorillaLibrary).ToArray(),
                OnGamemodeJoin = info.OnGamemodeJoin,
                OnGamemodeLeave = info.OnGamemodeLeave
            });
        }

        GameModeBridge.Canonicalize(__result, __instance.ModdedGamemodes);
    }

    static void GameModeManagerAwakePostfix()
    {
        if (!GamemodeManager.HasInstance || !GameModeManager.Initialization.Task.IsCompleted)
            return;

        var master = GameModeManager.Instance;
        var slave = GamemodeManager.Instance;
        slave.Gamemodes = master.Gamemodes.Select(GameModeBridge.ToUtilla).ToList();
        slave.ModdedGamemodes = master.ModdedGamemodes.Select(GameModeBridge.ToUtilla).ToList();
        slave.CustomGameModes = master.CustomGameModes.Select(GameModeBridge.ToUtilla).ToList();
        foreach (var mode in master.DefaultGameModesPerMode)
            slave.DefaultGameModesPerMode[mode.Key] = GameModeBridge.ToUtilla(mode.Value);
        foreach (var mode in master.ModdedGamemodesPerMode)
            slave.ModdedGamemodesPerMode[mode.Key] = GameModeBridge.ToUtilla(mode.Value);

        GamemodeManager.Initialization.TrySetResult(slave);
    }

    // All of Utilla's lookups go through these two methods. GL also understands the current room format.
    static bool GetGamemodePrefix(Func<Gamemode, bool> predicate, ref Gamemode __result)
    {
        var result = GorillaLibrary.Utilities.GameModeUtility.GetGameMode(mode => predicate(GameModeBridge.ToUtilla(mode)));
        __result = result is null ? null! : GameModeBridge.ToUtilla(result);
        return false;
    }

    static bool FindGamemodeInStringPrefix(string gmString, ref Gamemode __result)
    {
        var result = GorillaLibrary.Utilities.GameModeUtility.FindGameModeInString(gmString);
        __result = result is null ? null! : GameModeBridge.ToUtilla(result);
        return false;
    }

    // Utilla's default modded IDs contain a semicolon. Splitting the room string first loses half the ID.
    static bool FindMasterGamemodeInStringPrefix(string gmString, ref Gamemode __result)
    {
        __result = GamemodeManager.HasInstance
            ? GameModeBridge.FindUtillaGamemode(gmString, GamemodeManager.Instance.Gamemodes)
            : null!;
        return false;
    }

    static void ConductBoardManagerStartPatch(ConductBoardManager __instance)
    {
        if (__instance.footerText == null)
            return;

        if (__instance.footerText.text.Contains("<color=red>"))
            __instance.footerText.text = $"<color=red>TRANSLATILLA {PluginInfo.Version}</color> <color=grey>(U:{PluginInfo.UtillaVersion} / GL:{PluginInfo.GLVersion})</color>";
        else
            __instance.footerText.text = $"TRANSLATILLA {PluginInfo.Version} <color=grey>(U:{PluginInfo.UtillaVersion} / GL:{PluginInfo.GLVersion})</color>";
    }
}
