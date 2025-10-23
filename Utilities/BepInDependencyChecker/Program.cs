using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CecilThing
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            // Parameter parsing --------------  

            var showUnnecessaryDeps = args.Contains("-u"); // Show asses with no missing but some unnecessary dependency attributes  
            var showMatchingDeps = args.Contains("-m"); // Show asses with no missing but some correctly matched attributes  

            var asses = new List<string>();
            foreach (var arg in args)
            {
                if (arg.EndsWith(".dll"))
                {
                    var fi = new FileInfo(arg);
                    if (!fi.Exists)
                    {
                        Console.WriteLine("Skipping non-existent file: " + arg);
                        continue;
                    }
                    Console.WriteLine("Adding " + fi.FullName);
                    asses.Add(fi.FullName);
                }
                else if (Directory.Exists(arg))
                {
                    asses.AddRange(Directory.GetFiles(arg, "*.dll", SearchOption.AllDirectories));
                }
                else
                {
                    Console.WriteLine("Skipping unknown argument: " + arg);
                }
            }

            if (asses.Count == 0)
            {
                Console.WriteLine("No assemblies found in arguments");
                return 1;
            }

            // Gathering data from asses --------------  

            var assemblyPluginLookup = new Dictionary<string, List<string>>();
            var loadedAssemblies = new List<Mono.Cecil.AssemblyDefinition>();
            var assemblyResults = new List<(string AssemblyName, List<string> OwnGuids, List<string> MissingDependencies, List<string> UnnecessaryDependencies, List<string> MatchingDependencies)>();

            foreach (var assPath in asses)
            {
                try
                {
                    var ass = Mono.Cecil.AssemblyDefinition.ReadAssembly(assPath);

                    var assName = ass.Name;

                    var attribs = ass.MainModule.Types.SelectMany(x => x.CustomAttributes).ToList();
                    var pluginGuids = attribs.Where(x => x.AttributeType.FullName == "BepInEx.BepInPlugin").Select(x => x.ConstructorArguments[0].Value.ToString()).Distinct().OrderBy(x => x).ToList();

                    assemblyPluginLookup[assName.Name] = pluginGuids;

                    loadedAssemblies.Add(ass);
                }
                catch (BadImageFormatException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load assembly: {assPath} Error: {ex.Message}");
                }
            }

            var exactNameMatches = new HashSet<string> { "mscorlib", "UniRx", "0Harmony", "BepInEx" };
            var excludedPrefixes = new List<string> { "Assembly-CSharp", "System.", "UnityEngine.", "Unity." };
            foreach (var ass in loadedAssemblies)
            {
                var attribs = ass.MainModule.Types.SelectMany(x => x.CustomAttributes).ToList();
                var dependencyGuids = attribs.Where(x => x.AttributeType.FullName == "BepInEx.BepInDependency").Select(x => x.ConstructorArguments[0].Value.ToString()).Distinct().OrderBy(x => x).ToList();

                var refNames = ass.MainModule.AssemblyReferences.Select(x => x.Name).Where(x => !exactNameMatches.Contains(x) && !excludedPrefixes.Any(x.StartsWith)).ToList();

                var refGuids = refNames
                                   .Where(name => assemblyPluginLookup.ContainsKey(name))
                                   .SelectMany(name => assemblyPluginLookup[name])
                                   .Distinct()
                                   .ToList();
                var selfGuids = assemblyPluginLookup[ass.Name.Name];
                refGuids.AddRange(selfGuids);

                var missingDependencies = refGuids.Except(dependencyGuids).Except(selfGuids).OrderBy(guid => guid).ToList();
                var matchingDependencies = dependencyGuids.Intersect(refGuids).OrderBy(guid => guid).ToList();
                var unnecessaryDependencies = dependencyGuids.Except(refGuids).OrderBy(guid => guid).ToList();

                // Deal with plugins that have multiple guids - only require one of the guids to be referenced then  
                CleanUpMissingFromMultiPlugins(matchingDependencies.Concat(unnecessaryDependencies), assemblyPluginLookup, missingDependencies);

                assemblyResults.Add((ass.Name.Name, selfGuids, missingDependencies, unnecessaryDependencies, matchingDependencies));
            }

            // Do an extra pass to deal with dependency chains  
            foreach (var assemblyResult in assemblyResults)
            {
                if (assemblyResult.MissingDependencies.Count == 0) continue;

                respin:
                var depsOneUp = assemblyResults.Where(x => x.OwnGuids.Any(g => assemblyResult.MatchingDependencies.Concat(assemblyResult.UnnecessaryDependencies).Contains(g))).SelectMany(x => x.MatchingDependencies.Concat(x.UnnecessaryDependencies)).ToList();
                var needsRespin = false;
                depsOneUp.ForEach(s =>
                {
                    if (assemblyResult.MissingDependencies.Remove(s))
                        needsRespin = true;
                });
                CleanUpMissingFromMultiPlugins(depsOneUp, assemblyPluginLookup, assemblyResult.MissingDependencies);
                if (needsRespin)
                    goto respin;
            }

            // Writing results --------------  
            foreach (var result in assemblyResults)
            {
                // Skip non-plugins  
                if (result.OwnGuids.Count == 0) continue;

                if (result.MissingDependencies.Count == 0 && (!showUnnecessaryDeps || result.UnnecessaryDependencies.Count == 0) && (!showMatchingDeps || result.MatchingDependencies.Count == 0))
                {
                    continue;
                }

                Console.WriteLine($"Assembly: {result.AssemblyName}");

                if (result.MissingDependencies.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("- Missing BepInDependencies:");
                    foreach (var missing in result.MissingDependencies)
                    {
                        Console.WriteLine($"-   {missing}");
                    }
                    Console.ResetColor();
                }

                if (result.UnnecessaryDependencies.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("= Unnecessary BepInDependencies:");
                    foreach (var unnecessary in result.UnnecessaryDependencies)
                    {
                        Console.WriteLine($"=   {unnecessary}");
                    }
                    Console.ResetColor();
                }

                if (result.MatchingDependencies.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("+ Matching BepInDependencies:");
                    foreach (var matching in result.MatchingDependencies)
                    {
                        Console.WriteLine($"+   {matching}");
                    }
                    Console.ResetColor();
                }
                Console.WriteLine();
            }

            return 0;
        }

        private static void CleanUpMissingFromMultiPlugins(IEnumerable<string> matchingDependencies, IDictionary<string, List<string>> assemblyPluginLookup, List<string> missingDependencies)
        {
            foreach (var matchingDependency in matchingDependencies)
            {
                foreach (var apl in assemblyPluginLookup)
                {
                    if (apl.Value.Contains(matchingDependency))
                    {
                        apl.Value.ForEach(s => missingDependencies.Remove(s));
                        break;
                    }
                }
            }
        }
    }
}
