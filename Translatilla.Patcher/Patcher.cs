using BepInEx;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Translatilla.Patcher;

/*
 * What this does:
 * 
 * GorillaLibrary declares Utilla as an incompatability and refuses to load it at all. This will strip that
 * incompatability attribute from all types in the assembly for both Utilla and GorillaLibrary since
 * Translatilla forces them to load together.
 * 
 * Only removing the incompatability instead of mimicking all of the class names allows multiple things
 * to keep flowing smoothly on both:
 * 
 * - Non-modded calls: You can still access GorillaLibrary's other features if your master library is
 *                     Utilla.
 * - Assembly loading: Assemblies that check to make sure that GorillaLibrary's GUID is loaded still
 *                     operate as normal.
 * - Smaller footprint: The removed extra bloatware make Translatilla easier to maintain.
 */

public static class Patcher
{
    public static IEnumerable<string> TargetDLLs { get; } = new string[0];

    public static void Initialize()
    {
        string[] filePaths = Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories);
        foreach (string filepath in filePaths)
        {
            using var assembly = AssemblyDefinition.ReadAssembly(filepath, new ReaderParameters { ReadWrite = true });

            Console.WriteLine("Reading: " + assembly.Name.Name);

            var mainModule = assembly.MainModule;

            if (assembly.Name.Name != "GorillaLibrary")
                continue;

            Console.WriteLine("Patching: " + assembly.Name.Name);

            foreach (var type in mainModule.Types)
            {
                var attributesToRemove = type.CustomAttributes
                    .Where(attr => attr.AttributeType.Name == "BepInIncompatibility")
                    .ToList();
                foreach (var attr in attributesToRemove)
                {
                    type.CustomAttributes.Remove(attr);
                }
            }

            assembly.Write();
        }
    }
    
    // Dummy method
    public static void Patch(AssemblyDefinition assembly) { }
}
