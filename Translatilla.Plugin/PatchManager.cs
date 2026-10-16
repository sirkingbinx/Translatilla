using HarmonyLib;
using System;
using System.Reflection;
using System.Linq;

namespace Translatilla.Plugin;

public static class PatchManager
{
    public static readonly Harmony harmony = new Harmony(PluginInfo.Guid);

    public static bool ApplyPatch(Type classType, string methodName, Type[] argumentTypes = null!, MethodInfo prefix = null!, MethodInfo postfix = null!, MethodInfo finalizer = null!)
    {
        MethodInfo originalMethod = AccessTools.Method(classType, methodName, argumentTypes);

        if (originalMethod == null)
        {
            Plugin.Logger.LogError($"Attempted to patch method {classType.Name}.{methodName} but it does not exist");
            return false;
        }

        harmony.Patch(originalMethod,
            prefix: prefix is not null ? new HarmonyMethod(prefix) : null,
            postfix: postfix is not null ? new HarmonyMethod(postfix) : null,
            finalizer: finalizer is not null ? new HarmonyMethod(finalizer) : null
        );

        return true;
    }

    public static bool ApplyPatch(MethodInfo originalMethod, MethodInfo prefix = null!, MethodInfo postfix = null!, MethodInfo finalizer = null!)
    {
        if (originalMethod == null)
        {
            Plugin.Logger.LogError("Attempted to patch a method that does not exist");
            return false;
        }

        harmony.Patch(originalMethod,
            prefix: prefix is not null ? new HarmonyMethod(prefix) : null,
            postfix: postfix is not null ? new HarmonyMethod(postfix) : null,
            finalizer: finalizer is not null ? new HarmonyMethod(finalizer) : null);

        return true;
    }

    public static bool ApplyConstructorPatch(Type classType, MethodInfo prefix = null!, MethodInfo postfix = null!, MethodInfo finalizer = null!)
    {
        ConstructorInfo originalCtor = AccessTools.Constructor(classType, new System.Type[] { });

        if (originalCtor == null)
        {
            Plugin.Logger.LogError($"Attempted to patch constructor of {classType.Name} but it does not exist");
            return false;
        }

        harmony.Patch(originalCtor,
            prefix: prefix is not null ? new HarmonyMethod(prefix) : null,
            postfix: postfix is not null ? new HarmonyMethod(postfix) : null,
            finalizer: finalizer is not null ? new HarmonyMethod(finalizer) : null);

        return true;
    }

    public static bool KillMethod(Type classType, string methodName)
    {
        MethodInfo originalMethod = AccessTools.Method(classType, methodName);

        if (originalMethod == null)
        {
            Plugin.Logger.LogError($"Attempted to patch method {classType.Name}.{methodName} but it does not exist");
            return false;
        }

        harmony.Patch(originalMethod,
            prefix: new HarmonyMethod(StopExecution),
            postfix: null);

        return true;
    }

    public static void RemoveLobbyPatches(Assembly assembly)
    {
        foreach (var original in Harmony.GetAllPatchedMethods().ToArray())
        {
            var patches = Harmony.GetPatchInfo(original);
            var methods = patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers).Concat(patches.Finalizers)
                .Select(patch => patch.PatchMethod).Distinct().ToArray();

            foreach (var method in methods)
            {
                if (method.DeclaringType?.Assembly != assembly)
                    continue;

                string typeName = method.DeclaringType.Name;
                if (typeName == "PostInitializedPatch")
                    continue;

                if (assembly == typeof(GorillaLibrary.Plugin).Assembly)
                {
                    if (typeName == "CosmeticControllerPatch" || typeName == "CosmeticWardrobePatches" || typeName == "RigContainerPatches" || typeName == "GameOverlayPatch")
                        continue;
                    if (typeName == "GameManagerPatches" && method.Name != "GameTypeNamePatch")
                        continue;
                }

                harmony.Unpatch(original, method);
            }
        }
    }

    public static MethodInfo GetMethodInfo(Delegate method) => method.Method;
    public static PropertyInfo GetPropertyInfo(Type classType, string propName) => AccessTools.Property(classType, propName);

    public static MethodInfo GetGetter(Type classType, string propName) => GetPropertyInfo(classType, propName).GetGetMethod(true);
    public static MethodInfo GetSetter(Type classType, string propName) => GetPropertyInfo(classType, propName).GetSetMethod(true);

    public static MethodInfo StopExecution => AccessTools.Method(typeof(PatchManager), nameof(StopExecutionPrefixMethod));

    private static bool StopExecutionPrefixMethod() => false;
}
