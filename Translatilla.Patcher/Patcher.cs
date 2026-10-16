using BepInEx;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Translatilla.Patcher;

/*
 * GorillaLibrary declares Utilla as an incompatability. Remove just that incompatability (and the
 * reverse, if present) before BepInEx discovers plugins, so both libraries can load together.
 *
 * BepInEx only supplies game assemblies to Patch(), so plugin assemblies are handled in Initialize().
 */
public static class Patcher
{
    public static IEnumerable<string> TargetDLLs { get; } = new string[0];

    public static void Initialize()
    {
        if (!Directory.Exists(Paths.PluginPath))
            return;

        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);
        resolver.AddSearchDirectory(Paths.ManagedPath);

        foreach (string directory in Directory.GetDirectories(Paths.PluginPath, "*", SearchOption.AllDirectories))
            resolver.AddSearchDirectory(directory);

        resolver.AddSearchDirectory(Paths.PluginPath);

        foreach (string filepath in Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories))
        {
            string temporaryPath = filepath + ".translatilla.tmp";
            try
            {
                using var stream = new MemoryStream(File.ReadAllBytes(filepath));
                using var assembly = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters { AssemblyResolver = resolver });
                if (assembly.Name.Name != "GorillaLibrary" && assembly.Name.Name != "Utilla")
                    continue;

                int attributeCount = assembly.MainModule.GetTypes().Sum(type => type.CustomAttributes.Count);
                Patch(assembly);
                
                if (attributeCount == assembly.MainModule.GetTypes().Sum(type => type.CustomAttributes.Count))
                    continue;

                assembly.Write(temporaryPath);
                File.Replace(temporaryPath, filepath, null);
                Console.WriteLine("[Translatilla.Patcher] Patched " + filepath);
            }
            catch (BadImageFormatException)
            { }
            catch (Exception ex)
            {
                Console.WriteLine("[Translatilla.Patcher] Could not patch " + filepath + ": " + ex);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }

    public static void Patch(AssemblyDefinition assembly)
    {
        string otherGuid;
        if (assembly.Name.Name == "GorillaLibrary")
            otherGuid = "org.legoandmars.gorillatag.utilla";
        else if (assembly.Name.Name == "Utilla")
            otherGuid = "dev.gorillalibrary";
        else
            return;

        foreach (var type in assembly.MainModule.GetTypes())
        {
            var attributesToRemove = type.CustomAttributes.Where(attribute =>
                attribute.AttributeType.FullName == "BepInEx.BepInIncompatibility" &&
                attribute.ConstructorArguments.Count == 1 &&
                attribute.ConstructorArguments[0].Value is string guid && guid == otherGuid).ToList();

            foreach (var attribute in attributesToRemove)
                type.CustomAttributes.Remove(attribute);
        }
    }
}
