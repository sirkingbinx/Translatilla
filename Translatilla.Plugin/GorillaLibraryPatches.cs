using BepInEx.Logging;
using GorillaGameModes;
using GorillaLibrary.Behaviours;
using GorillaLibrary.Models;
using GorillaLibrary.Patches;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Utilla.Behaviours;
using Utilla.Models;

namespace Translatilla.Plugin;

public static class GorillaLibraryPatches
{
    public static GorillaLibrary.Plugin GLInstance = null!;

    public static void Apply(bool isMasterLibrary, bool stopConductBoard = true)
    {
        // We leave it completely unpatched when it's the master library.
        if (isMasterLibrary)
            return;
        
        Harmony.UnpatchID("dev.gorillalibrary");

        if (stopConductBoard)
        {
            PatchManager.KillMethod(typeof(ConductBoardManager), nameof(ConductBoardManager.Start));
            PatchManager.KillMethod(typeof(ConductBoardManager), nameof(ConductBoardManager.CheckVersion));
            PatchManager.KillMethod(typeof(ConductBoardManager), nameof(ConductBoardManager.DownloadEntries));
        }

        PatchManager.ApplyPatch(
            typeof(GorillaLibrary.Plugin), nameof(GorillaLibrary.Plugin.Awake),
            prefix: PatchManager.GetMethodInfo(AwakePrefix)
        );

        PatchManager.ApplyPatch(
            typeof(GorillaLibrary.Plugin), nameof(GorillaLibrary.Plugin.OnGameInitialized),
            finalizer: PatchManager.GetMethodInfo(OnGameInitializedFinalizer)
        );

        /*
         * Unlike Utilla, GL does more than just modded libs so we still apply some of the patches manually to make sure that
         * the other features still operate independently.
         */

        // GorillaLibrary/Patches/CosmeticControllerPatch.cs
        PatchManager.ApplyPatch(
            typeof(CosmeticsController), nameof(CosmeticsController.UpdateWornCosmetics),
            postfix: PatchManager.GetMethodInfo((bool sync, bool playfx) => CosmeticControllerPatch.WornCosmeticsUpdatePatch())
        );

        // GorillaLibrary/Patches/GameManagerPatches.cs
        PatchManager.ApplyPatch(
            typeof(GameMode), nameof(GameMode.BroadcastTag),
            postfix: PatchManager.GetMethodInfo(GameManagerPatches.MasterTagPatch)
        );

        PatchManager.ApplyPatch(
            typeof(GameMode), nameof(GameMode.BroadcastRoundComplete),
            postfix: PatchManager.GetMethodInfo(GameManagerPatches.MasterRoundCompletePatch)
        );

        PatchManager.ApplyPatch(
            typeof(GameMode), nameof(GameMode.BroadcastRoundComplete),
            postfix: PatchManager.GetMethodInfo(GameManagerPatches.MasterRoundCompletePatch)
        );

        // GorillaLibrary/Patches/PostInitializedPatch.cs
        PatchManager.ApplyPatch(
            typeof(GorillaTagger), "Start",
            postfix: PatchManager.GetMethodInfo(PostInitializedPatch.Postfix)
        );

        // GorillaLibrary/Patches/RigContainerPatches.cs
        PatchManager.ApplyPatch(
            PatchManager.GetSetter(typeof(RigContainer), nameof(RigContainer.Creator)),
            postfix: PatchManager.GetMethodInfo(PostInitializedPatch.Postfix)
        );

        PatchManager.ApplyPatch(
            typeof(RigContainer), nameof(RigContainer.OnDisable),
            postfix: PatchManager.GetMethodInfo(PostInitializedPatch.Postfix)
        );

        // GorillaLibrary/Utilities/GameModeUtility.cs
        // Simular to Utilla, you really only gotta patch GetGamemode() to make GMUtils work.
        PatchManager.ApplyPatch(
            typeof(GorillaLibrary.Utilities.GameModeUtility), nameof(GorillaLibrary.Utilities.GameModeUtility.GetGameMode),
            prefix: PatchManager.GetMethodInfo(GetGameModePrefix)
        );

        // Hook onto Utilla version checker
        PatchManager.ApplyPatch(
            typeof(VersionCheckManager), nameof(VersionCheckManager.Start),
            postfix: PatchManager.GetMethodInfo(VersionCheckManagerStartPatch)
        );
    }

    // GorillaLibrary/Plugin.cs
    static bool AwakePrefix(GorillaLibrary.Plugin __instance)
    {
        GLInstance = __instance;

        GorillaLibrary.Plugin.Instance = __instance;
        GorillaLibrary.Plugin.Logger = Logger.CreateLogSource("GorillaLibrary/Translatilla");

        RuntimeHelpers.RunClassConstructor(typeof(GorillaLibrary.Events).TypeHandle);

/*
        MothershipClientApiUnity.OnMessageNotificationSocket += 
            (notif, _) => GorillaLibrary.Events.Server.OnMothershipMessageRecieved.Invoke(notif.Title, notif.Body);
*/

        Assembly gtAssembly = typeof(GorillaGameManager).Assembly;
        Type gtModeSerializeType = gtAssembly.GetType("GameModeSerializer");

        if (gtModeSerializeType != null)
        {
            Harmony glibHarmony = new Harmony("translatilla." + __instance.Info.Metadata.GUID);

            // If it ain't broke don't reformat it
            glibHarmony.Patch(AccessTools.Method(gtModeSerializeType, "BroadcastTag", parameters: [typeof(NetPlayer), typeof(NetPlayer), typeof(PhotonMessageInfo)]), postfix: new(AccessTools.Method(typeof(GameManagerPatches), nameof(GameManagerPatches.ClientTagPatch))));
            glibHarmony.Patch(AccessTools.Method(gtModeSerializeType, "BroadcastRoundComplete", parameters: [typeof(PhotonMessageInfoWrapped)]), postfix: new(AccessTools.Method(typeof(GameManagerPatches), nameof(GameManagerPatches.ClientRoundCompletePatch))));
        }

        return false;
    }

    static void OnGameInitializedFinalizer()
    {
        Plugin.GorillaLibraryGameObject = GLInstance.sharedObject;
        Plugin.GorillaLibraryGameObject.name = $"GorillaLibrary/Translatilla {GLInstance.Info.Metadata.Version}";
    }

    // GorillaLibrary/Utilities/GameModeUtility.cs
    static bool GetGameModePrefix(Func<GameModeWrapper, bool> predicate, ref GameModeWrapper __result)
    {
        var gmWrapperResult = Utilla.Utils.GameModeUtils.GetGamemode(gm =>
        {
            var glGamemode = new GameModeWrapper(
                gm.ID,
                gm.DisplayName,
                gm.BaseGamemode
            );

            return predicate(glGamemode);
        });

        if (gmWrapperResult is null)
        {
            __result = null!;
            return false;
        }

        var glGamemode = new GameModeWrapper(
            gmWrapperResult.ID,
            gmWrapperResult.DisplayName,
            gmWrapperResult.BaseGamemode
        );

        __result = glGamemode;

        return false;
    }

    // GorillaLibrary/Behaviours/GameModeManager.cs
    static bool GameModeManagerAwakePatch(GameModeManager __instance)
    {
        // This is almost a 1:1 copy of the simularly named method in UtillaPatches.cs

        GameModeManager.Instance = __instance;

        // Translating game modes is simular to Utilla. They are the same codebase after all.
        // Even both the gameobjects are named "Utilla Custom Game Modes"

        // Utilla conveniently has a helper function for getting our plugin infos
        List<GorillaLibrary.PluginInfo> gorillaLibraryPluginInfos = __instance.GetPluginInfos();
        List<Utilla.PluginInfo> utillaPluginInfos = new();

        List<Gamemode> gamemodes = new();

        Gamemode CreateGameMode(GameModeWrapper glGamemode) =>
            new Gamemode(glGamemode.ID, glGamemode.DisplayName, glGamemode.GameManager.GetType());

        // Get gamemodes via GL for conversion
        foreach (var pluginInfo in gorillaLibraryPluginInfos)
        {
            Gamemode[] ourGamemodes = pluginInfo.Gamemodes.Select(g => CreateGameMode(g)).ToArray();
            
            var utillaPluginInfo = new Utilla.PluginInfo
            {
                Gamemodes = ourGamemodes,
                Plugin = pluginInfo.Plugin,
                // None of the gamemode events take namespace-specific types, they take strings so no translation required
                OnGamemodeJoin = pluginInfo.OnGamemodeJoin,
                OnGamemodeLeave = pluginInfo.OnGamemodeLeave
            };

            gamemodes.AddRange(ourGamemodes);
            utillaPluginInfos.Add(utillaPluginInfo);
        }

        GamemodeManager gmmInstance = GamemodeManager.Instance;

        // Now just inject them into Utilla's gamemode mgr
        gmmInstance.Gamemodes.AddRange(gamemodes);
        gamemodes.ForEach(gmmInstance.AddGamemodeToPrefabPool);

        // properly insert our pluginInfos just in case
        gmmInstance.pluginInfos.AddRange(utillaPluginInfos);

        Plugin.Logger.Log($"Added {gamemodes.Count} gamemodes to Utilla game selector");

        // We don't need to kill the network controller since it's room update events are completely
        // harmless and rely on NetworkSystem. It also updates GameModeUtils but mostly irrelevant since it's
        // already patched.
        // 
        // Sorta redundant to let you know that the patches don't do something but felt like it'd be useful
        // to mention since a name like "UtillaNetworkController" implies that it is controlling networking
        // operations.

        Plugin.Logger.LogMessage($"GorillaLibrary {GLInstance.Info.Metadata.Version} should be patched and ready to work with Utilla.");
        Plugin.Logger.LogMessage($"If any crashes, bugs, or issues occur with either library, please fill out an issue on GitHub:");
        Plugin.Logger.LogMessage($"https://github.com/sirkingbinx/Translatilla");

        return false;
    }

    // GorillaLibrary/Behaviours/ConductBoardManager.cs
    static void VersionCheckManagerStartPatch(VersionCheckManager __instance)
    {
        __instance.footerText.text += $" <color=grey>/ Translatilla {PluginInfo.Version}</color>";
    }
}
