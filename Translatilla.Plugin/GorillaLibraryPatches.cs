using BepInEx.Logging;
using GorillaGameModes;
using GorillaLibrary;
using GorillaLibrary.Behaviours;
using GorillaLibrary.Models;
using GorillaLibrary.Patches;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Translatilla.Plugin;

public static class GorillaLibraryPatches
{
    public static GorillaLibrary.Plugin GLInstance = null!;

    public static void Apply(bool isMasterLibrary, bool stopConductBoard = true)
    {
        // We leave it completely unpatched when it's the master library.
        if (isMasterLibrary)
            return;

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
            typeof(RigContainer), "OnDisable", // maybe exists???
            postfix: PatchManager.GetMethodInfo(PostInitializedPatch.Postfix)
        );

        // GorillaLibrary/Utilities/GameModeUtility.cs
        // Simular to Utilla, you really only gotta patch GetGamemode() to make GMUtils work.
        PatchManager.ApplyPatch(
            typeof(GorillaLibrary.Utilities.GameModeUtility), nameof(GorillaLibrary.Utilities.GameModeUtility.GetGameMode),
            prefix: PatchManager.GetMethodInfo(GetGameModePrefix)
        );
    }

    // GorillaLibrary/Plugin.cs
    static bool AwakePrefix(GorillaLibrary.Plugin __instance)
    {
        GLInstance = __instance;

        GorillaLibrary.Plugin.Instance = __instance;
        GorillaLibrary.Plugin.Logger = Logger.CreateLogSource("GorillaLibrary/Translatilla");

        RuntimeHelpers.RunClassConstructor(typeof(GorillaLibrary.Events).TypeHandle);

        MothershipClientApiUnity.OnMessageNotificationSocket += 
            (notif, _) => GorillaLibrary.Events.Server.OnMothershipMessageRecieved.Invoke(notif.Title, notif.Body);

        Assembly gtAssembly = typeof(GorillaGameManager).Assembly;
        Type gtModeSerializeType = gtAssembly.GetType("GameModeSerializer");

        if (gtModeSerializeType != null)
        {
            Harmony glibHarmony = new Harmony("translatilla.dev.gorillalibrary");

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
}
