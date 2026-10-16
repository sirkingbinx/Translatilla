using BepInEx;
using BepInEx.Bootstrap;
using GorillaLibrary.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Utilla.Models;

namespace Translatilla.Plugin;

internal static class GameModeBridge
{
    public static Gamemode ToUtilla(GameModeWrapper mode) => mode.GameManager is null
        ? new Gamemode(mode.ID, mode.DisplayName, mode.BaseGameMode)
        : new Gamemode(mode.ID, mode.DisplayName, mode.GameManager);

    public static GameModeWrapper ToGorillaLibrary(Gamemode mode) => mode.GameManager is null
        ? new GameModeWrapper(mode.ID, mode.DisplayName, mode.BaseGamemode)
        : new GameModeWrapper(mode.ID, mode.DisplayName, mode.GameManager);

    public static Gamemode FindUtillaGamemode(string gmString, IEnumerable<Gamemode> gamemodes)
    {
        if (string.IsNullOrEmpty(gmString))
            return null!;

        var modes = gamemodes.OrderByDescending(mode => mode.ID.Length).ToArray();
        string room = ";" + gmString + ";";
        return modes.FirstOrDefault(mode => room.Contains(";" + mode.ID + ";")) ??
            (gmString.Contains(";") ? null! : modes.FirstOrDefault(mode => gmString.Contains(mode.ID))!);
    }

    internal class PluginModes<T>
    {
        public BaseUnityPlugin Plugin = null!;
        public T[] Gamemodes = null!;
        public Action<string> OnGamemodeJoin = null!;
        public Action<string> OnGamemodeLeave = null!;
    }

    public static List<PluginModes<TMode>> GetPluginInfos<TAttribute, TMode>(
        Func<TAttribute, TMode> getGamemode, TMode[] moddedGamemodes, Type joinAttribute, Type leaveAttribute)
        where TAttribute : Attribute
        where TMode : class
    {
        List<PluginModes<TMode>> infos = new();
        foreach (var pluginInfo in Chainloader.PluginInfos.Values)
        {
            var plugin = pluginInfo.Instance;
            if (plugin is null)
                continue;

            try
            {
                var type = plugin.GetType();
                List<TMode> gamemodes = new();
                foreach (var attribute in type.GetCustomAttributes<TAttribute>())
                {
                    var gamemode = getGamemode(attribute);
                    if (gamemode is null)
                        gamemodes.AddRange(moddedGamemodes);
                    else
                        gamemodes.Add(gamemode);
                }

                if (gamemodes.Count == 0)
                    continue;

                infos.Add(new PluginModes<TMode>
                {
                    Plugin = plugin,
                    Gamemodes = gamemodes.Distinct().ToArray(),
                    OnGamemodeJoin = CreateJoinLeaveAction(plugin, joinAttribute),
                    OnGamemodeLeave = CreateJoinLeaveAction(plugin, leaveAttribute)
                });
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Could not translate game modes for {pluginInfo.Metadata.GUID}");
                Plugin.Logger.LogError(ex);
            }
        }

        return infos;
    }

    static Action<string> CreateJoinLeaveAction(BaseUnityPlugin plugin, Type attribute)
    {
        Action<string> action = null!;
        var methods = plugin.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var method in methods.Where(method => method.IsDefined(attribute, true)))
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                var callback = (Action)Delegate.CreateDelegate(typeof(Action), plugin, method);
                action += _ => callback();
            }
            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
            {
                action += (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), plugin, method);
            }
        }

        return action;
    }
    
    public static void Canonicalize(List<Utilla.PluginInfo> infos, List<Gamemode> moddedGamemodes)
    {
        var modes = moddedGamemodes.ToDictionary(mode => mode.ID);
        foreach (var info in infos)
        {
            info.Gamemodes = info.Gamemodes.Select(mode =>
            {
                if (!modes.TryGetValue(mode.ID, out var existing))
                    modes.Add(mode.ID, existing = mode);
                return existing;
            }).Distinct().ToArray();
        }
    }

    public static void Canonicalize(List<GorillaLibrary.PluginInfo> infos, List<GameModeWrapper> moddedGamemodes)
    {
        var modes = moddedGamemodes.ToDictionary(mode => mode.ID);
        foreach (var info in infos)
        {
            info.Gamemodes = info.Gamemodes.Select(mode =>
            {
                if (!modes.TryGetValue(mode.ID, out var existing))
                    modes.Add(mode.ID, existing = mode);
                return existing;
            }).Distinct().ToArray();
        }
    }
}
