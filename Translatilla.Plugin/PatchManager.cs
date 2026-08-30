using HarmonyLib;
using System;
using System.Reflection;

namespace Translatilla.Plugin;

public static class PatchManager
{
    public static readonly Harmony harmony = new Harmony(Constants.Guid);

    public static bool ApplyPatch(Type classType, string methodName, Type[] argumentTypes = null!, MethodInfo prefix = null!, MethodInfo postfix = null!, MethodInfo finalizer = null!)
    {
        MethodInfo originalMethod = AccessTools.Method(classType, methodName);

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
        harmony.Patch(originalMethod,
            prefix: prefix is not null ? new HarmonyMethod(prefix) : null,
            postfix: postfix is not null ? new HarmonyMethod(postfix) : null,
            finalizer: finalizer is not null ? new HarmonyMethod(finalizer) : null);

        return true;
    }

    public static bool ApplyConstructorPatch(Type classType, string methodName, MethodInfo prefix = null!, MethodInfo postfix = null!, MethodInfo finalizer = null!)
    {
        MethodInfo originalMethod = AccessTools.Method(classType, methodName);

        if (originalMethod == null)
        {
            Plugin.Logger.LogError($"Attempted to patch method {classType.Name}.{methodName} but it does not exist");
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

    public static MethodInfo GetMethodInfo(Delegate method) => method.Method;
    public static PropertyInfo GetPropertyInfo(Type classType, string propName) => classType.GetProperty(propName);

    public static MethodInfo GetGetter(Type classType, string propName) => GetPropertyInfo(classType, propName).GetGetMethod();
    public static MethodInfo GetSetter(Type classType, string propName) => GetPropertyInfo(classType, propName).GetSetMethod();

    public static MethodInfo StopExecution => AccessTools.Method(typeof(PatchManager), nameof(StopExecutionPrefixMethod));

    private static bool StopExecutionPrefixMethod() => false;
}
