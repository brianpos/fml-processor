using fml_processor;
using fml_processor.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Health.Fhir.CodeGen.Tests;
using Microsoft.Health.Fhir.MappingLanguage;
using System.ComponentModel.Design;
using System.Data;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Task = System.Threading.Tasks.Task;

namespace fml_tester
{
    [TestClass]
    public sealed class RecreateFmlTest
    {
        public IEnumerable<FmlStructureMap> GenerateCrossVersionMaps(string sourceVersion, string targetVersion, bool writeMaps = true)
        {
            string sourceFolder = Path.Combine(Path.GetTempPath(), "FML", $"{sourceVersion}-SD");
            if (!Directory.Exists(sourceFolder))
                Directory.CreateDirectory(sourceFolder);
            Console.WriteLine($"Processing source structures in {sourceFolder}");
            string targetFolder = Path.Combine(Path.GetTempPath(), "FML", $"{targetVersion}-SD");
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);
            Console.WriteLine($"Processing target structures in {targetFolder}");

            FhirJsonPocoDeserializer ds = new FhirJsonPocoDeserializer(new FhirJsonPocoDeserializerSettings()
            {
                AnnotateResourceParseExceptions = true,
                Validator = null
            });

            FmlCreator fmlCreator = new FmlCreator();

            // Load Target Structures
            string typesFile = Path.Combine(targetFolder, "profiles-types.json");
            LoadStructures(ds, fmlCreator.Target, typesFile);
            string resourcesFile = Path.Combine(targetFolder, "profiles-resources.json");
            LoadStructures(ds, fmlCreator.Target, resourcesFile);

            // Load Source Structures
            typesFile = Path.Combine(sourceFolder, "profiles-types.json");
            LoadStructures(ds, fmlCreator.Source, typesFile);
            resourcesFile = Path.Combine(sourceFolder, "profiles-resources.json");
            LoadStructures(ds, fmlCreator.Source, resourcesFile);

            Console.WriteLine();
            var maps = fmlCreator.GenerateMaps();
            if (writeMaps)
                WriteMapsAndReviewNotes(sourceVersion, targetVersion, maps);
            return maps;
        }

        private static void WriteMapsAndReviewNotes(string sourceVersion, string targetVersion, List<FmlStructureMap> maps)
        {

            // Write all the maps to the output folder
            string outputFolder = Path.Combine("c:", "temp", "fhir-cross-version-source",
                $"{sourceVersion}_{targetVersion}",
                "maps", "StructureMaps");
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            Console.WriteLine();
            string? workGroup = null;
            foreach (var map in maps.OrderBy((a) => a.Annotation<WorkgroupAnnotation>()?.name)
                .ThenBy((a) => a.Metadata.FirstOrDefault(m => m.Path == "name")?.Value))
            {
                if (workGroup != map.Annotation<WorkgroupAnnotation>()?.name)
                {
                    workGroup = map.Annotation<WorkgroupAnnotation>()?.name;
                    Console.WriteLine($"# {workGroup}");
                }
                var fmlText = FmlSerializer.Serialize(map);
                if (map.HasAnnotation<NeedsReviewAnnotation>())
                {
                    // Console.WriteLine(fmlText);
                    LogMapReviewNotes(map, sourceVersion, targetVersion);
                }

                string outputFilename = Path.Combine(outputFolder, $"{map.Groups[0].Name}_{sourceVersion.Replace("R", "")}to{targetVersion.Replace("R", "")}.fml");
                File.WriteAllText(outputFilename, fmlText);
            }
        }

        private static void LogMapReviewNotes(FmlStructureMap map, string sourceVersion, string targetVersion)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## {map.Metadata.FirstOrDefault(m => m.Path == "name")?.Value}");
            sb.AppendLine($"> Proposed StructureMap: {map.Metadata.FirstOrDefault(m => m.Path == "url")?.Value}<br/>");
            var sourceUrl = map.Structures.First()?.Url;
            if (sourceUrl.StartsWith("http://hl7.org/fhir/6.0/StructureDefinition/"))
                sourceUrl = sourceUrl.Replace("http://hl7.org/fhir/6.0/StructureDefinition/", "https://build.fhir.org/").ToLower();
            var targetUrl = map.Structures.Skip(1).FirstOrDefault()?.Url;
            if (targetUrl.StartsWith("http://hl7.org/fhir/6.0/StructureDefinition/"))
                targetUrl = targetUrl.Replace("http://hl7.org/fhir/6.0/StructureDefinition/", "https://build.fhir.org/").ToLower();

            sb.AppendLine($"> Source: {sourceUrl}<br/>");
            sb.AppendLine($"> Target: {targetUrl}");
            sb.AppendLine();

            // Missing property reports
            var missingSourceProperties = map.Annotations<NotReadElementAnnotation>();
            if (missingSourceProperties.Any())
            {
                sb.AppendLine("#### Source properties with no target");
                foreach (var prop in missingSourceProperties)
                {
                    var targetTypes = String.Join(",", prop.definition.Type?.Select(t => t.Code));
                    sb.Append($" - {prop.definition.Path} `{targetTypes} [{prop.definition.Min}..{prop.definition.Max}]`");
                    IEnumerable<string> targetProfiles = prop.definition.Type?.SelectMany(t => t.TargetProfile.Select(tp => tp.Replace("http://hl7.org/fhir/StructureDefinition/", ""))) ?? Enumerable.Empty<string>();
                    if (targetProfiles.Any())
                        sb.Append($" _({String.Join(",", targetProfiles)})_");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
            var missingTargetProperties = map.Annotations<NotPopulatedElementAnnotation>();
            if (missingTargetProperties.Any())
            {
                sb.AppendLine($"#### New properties added to {targetVersion}");
                foreach (var prop in missingTargetProperties)
                {
                    var targetTypes = String.Join(",", prop.definition.Type?.Select(t => t.Code));
                    sb.Append($" - {prop.definition.Path} `{targetTypes} [{prop.definition.Min}..{prop.definition.Max}]`");
                    IEnumerable<string> targetProfiles = prop.definition.Type?.SelectMany(t => t.TargetProfile.Select(tp => tp.Replace("http://hl7.org/fhir/StructureDefinition/", ""))) ?? Enumerable.Empty<string>();
                    if (targetProfiles.Any())
                        sb.Append($" _({String.Join(",", targetProfiles)})_");
                    IEnumerable<string> profiles = prop.definition.Type?.SelectMany(t => t.Profile.Select(tp => tp.Replace("http://hl7.org/fhir/StructureDefinition/", ""))) ?? Enumerable.Empty<string>();
                    if (profiles.Any())
                        sb.Append($" _({String.Join(",", profiles)})_");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            // Group declarations
            for (int i = 0; i < map.Groups.Count; i++)
            {
                var group = map.Groups[i];
                if (group.HasAnnotation<NeedsReviewAnnotation>())
                {
                    sb.AppendLine($"#### {group.Name} mapping review");
                    if (!string.IsNullOrEmpty(group.Parameters[0].Type))
                        sb.AppendLine($"> {group.Parameters[0].Type} -> {group.Parameters[1].Type}");
                    sb.AppendLine("```");

                    var sbInner = new StringBuilder();
                    foreach (var rule in group.Rules)
                    {
                        if (rule.HasAnnotation<NeedsReviewAnnotation>())
                        {
                            // FmlSerializer.SerializeRule(sbInner, rule, 1);

                            // Break this out so that dependencies don't get rendered
                            sbInner.Append("  ");
                            // Sources
                            for (int iSource = 0; iSource < rule.Sources.Count; iSource++)
                            {
                                if (iSource > 0)
                                {
                                    sbInner.Append(", ");
                                }
                                var alias = rule.Sources[iSource].Variable;
                                rule.Sources[iSource].Variable = null;
                                FmlSerializer.SerializeRuleSource(sbInner, rule.Sources[iSource]);
                                rule.Sources[iSource].Variable = alias;
                            }

                            // Targets
                            if (rule.Targets.Any())
                            {
                                sbInner.Append(" -> ");

                                for (int iTarget = 0; iTarget < rule.Targets.Count; iTarget++)
                                {
                                    if (iTarget > 0)
                                    {
                                        sbInner.Append(", ");
                                    }
                                    var alias = rule.Targets[iTarget].Variable;
                                    rule.Targets[iTarget].Variable = null;
                                    FmlSerializer.SerializeRuleTarget(sbInner, rule.Targets[iTarget]);
                                    rule.Targets[iTarget].Variable = alias;
                                }
                            }

                            sbInner.Append(";");

                            // Output trailing hidden tokens (inline comments)
                            FmlSerializer.OutputTrailingHiddenTokens(sbInner, rule);

                            sbInner.AppendLine();

                        }
                    }
                    if (group.Rules.LastOrDefault()?.HasAnnotation<NeedsReviewAnnotation>() == false)
                        FmlSerializer.OutputTrailingHiddenTokens(sbInner, group.Rules.Last());
                    sbInner.AppendLine();
                    sb.AppendLine("  " + sbInner.ToString().Replace("\n\n", "\n").Trim());
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }

            // Output trailing hidden tokens (end-of-file comments)
            FmlSerializer.OutputTrailingHiddenTokens(sb, map);

            Console.WriteLine(sb);
        }

        [TestMethod]
        public void RecreateR5toR6maps()
        {
            var maps = GenerateCrossVersionMaps("R5", "R6");
        }

        [TestMethod]
        public void RecreateR6toR5maps()
        {
            var maps = GenerateCrossVersionMaps("R6", "R5");
        }

        [TestMethod]
        public void RecreateR4toR6maps()
        {
            var maps = GenerateCrossVersionMaps("R4", "R6");
        }


        [TestMethod]
        public void RecreateR4toR5maps()
        {
            var maps = GenerateCrossVersionMaps("R4", "R5");
        }

        private static void LoadStructures(FhirJsonPocoDeserializer ds, Dictionary<string, StructureDefinition> col, string typesFile)
        {
            var ts = File.ReadAllBytes(typesFile);
            var js = new Utf8JsonReader(ts);
            ds.TryDeserializeResource(ref js, out var resource, out var issues);
            if (resource != null)
            {
                // read all the resources in the bundle
                if (resource is Bundle bun)
                {
                    foreach (var entry in bun.Entry)
                    {
                        if (entry.Resource is StructureDefinition sd)
                        {
                            col.Add(sd.Url, sd);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void SortFhirIni()
        {
            var result = SortIniFile(@"c:/git/hl7/fhir-core-build/source/fhir.ini", "r4-r6-changes");
            Console.WriteLine(result);
            File.WriteAllText(@"c:/git/hl7/fhir-core-build/source/fhir.ini", result);
        }

        /// <summary>
        /// Sort the keys in an ini file section alphabetically
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="section"></param>
        public string SortIniFile(string filename, string section)
        {
            string[] lines = File.ReadAllLines(filename);

            List<string> sectionLines = null;
            StringBuilder sb = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    var sectionName = line[1..^1];
                    if (sectionName == section)
                    {
                        sectionLines = new List<string>();
                    }
                    else if (sectionLines != null)
                    {
                        // sort the section lines (ignoring a leading ';' or '#')
                        sectionLines.Sort((a, b) =>
                        {
                            string ka = a.Length > 0 && (a[0] == ';' || a[0] == '#') ? a[1..] : a;
                            string kb = b.Length > 0 && (b[0] == ';' || b[0] == '#') ? b[1..] : b;
                            return string.Compare(ka, kb, StringComparison.OrdinalIgnoreCase);
                        });

                        // and write them out (skipping empty lines)
                        foreach (var line2 in sectionLines.Where(l => !string.IsNullOrWhiteSpace(l)))
                        {
                            sb.AppendLine(line2);
                        }
                        sb.AppendLine(); // add a buffer line after the section
                        sectionLines = null;
                    }
                    sb.AppendLine(line);
                }
                else
                {
                    if (sectionLines != null)
                        sectionLines.Add(line);
                    else
                        sb.AppendLine(line);
                }
            }
            return sb.ToString();
        }

        public List<string> ReadSection(string filename, string section)
        {
            string[] lines = File.ReadAllLines(filename);

            List<string> sectionLines = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    var sectionName = line[1..^1];
                    if (sectionName == section)
                    {
                        sectionLines = new List<string>();
                    }
                    else if (sectionLines != null)
                    {
                        // we have reached the end of the section, return the lines
                        break;
                    }
                }
                else
                {
                    if (sectionLines != null)
                        sectionLines.Add(line);
                }
            }
            return sectionLines ?? new List<string>();
        }

        [TestMethod]
        public void RecreateR4toR6DiffsForCoreSpec()
        {
            var maps = GenerateCrossVersionMaps("R4", "R6", false);
            Console.WriteLine("[r4-r6-changes]");
            var existingMaps = ReadSection(@"c:/git/hl7/fhir-core-build/source/fhir.ini", "r4-r6-changes");
            TraceIniContentForMaps(maps, existingMaps);
        }

        [TestMethod]
        public void RecreateR4BtoR6DiffsForCoreSpec()
        {
            var maps = GenerateCrossVersionMaps("R4B", "R6", false);
            Console.WriteLine("[r4-r6-changes]");
            var existingMaps = ReadSection(@"c:/git/hl7/fhir-core-build/source/fhir.ini", "r4-r6-changes");
            TraceIniContentForMaps(maps, existingMaps);
        }

        [TestMethod]
        public void RecreateR5toR6DiffsForCoreSpec()
        {
            var maps = GenerateCrossVersionMaps("R5", "R6", false);
            Console.WriteLine("[r5-r6-changes]");
            var existingMaps = ReadSection(@"c:/git/hl7/fhir-core-build/source/fhir.ini", "r5-r6-changes");
            TraceIniContentForMaps(maps, existingMaps);
        }

        private static void TraceIniContentForMaps(IEnumerable<FmlStructureMap> maps, List<string> exitingMaps)
        {
            List<string> diffs = new List<string>();
            // we didn't write the diffs, but we should prepare the details for the core spec fhir.ini file
            /*
               x=note // this format just pots the content
               e.g. 
            
            Device.statusReason=-> reason was removed. DeviceAssociation can be used
            Device.distinctIdentifier=@biologicalSourceEvent
            Coverage.payor=
             */



            foreach (var map in maps.OrderBy((a) => a.Metadata.FirstOrDefault(m => m.Path == "name")?.Value))
            {
                if (map.HasAnnotation<NeedsReviewAnnotation>())
                {
                    // Missing property reports (if there are possible maps available)
                    var missingSourceProperties = map.Annotations<NotReadElementAnnotation>();
                    var missingTargetProperties = map.Annotations<NotPopulatedElementAnnotation>();
                    if (missingSourceProperties.Any() && missingTargetProperties.Any())
                    {
                        foreach (var prop in missingSourceProperties)
                        {
                            var iniRule = $"{prop.definition.Path}=";
                            var em = exitingMaps.Where(r => r.StartsWith(iniRule));
                            if (em.Count() == 0)
                            {
                                diffs.Add(iniRule);
                            }
                            else if (em.Count() == 1)
                            {
                                diffs.Add($"{em.First()}");
                                exitingMaps.Remove(em.First());
                            }
                            else
                            {
                                diffs.Add(iniRule + ", ** multiple **");
                            }
                        }

                        foreach (var prop in missingTargetProperties)
                        {
                            var iniRule = $"{prop.definition.Path}=+";
                            var em = exitingMaps.Where(r => r.StartsWith(iniRule));
                            if (em.Count() == 0)
                            {
                                diffs.Add(iniRule);
                            }
                            else if (em.Count() == 1)
                            {
                                diffs.Add($"{em.First()}");
                                exitingMaps.Remove(em.First());
                            }
                            else
                            {
                                diffs.Add(iniRule + ", ** multiple **");
                            }
                        }
                    }

                    for (int i = 0; i < map.Groups.Count; i++)
                    {
                        var group = map.Groups[i];
                        if (group.HasAnnotation<NeedsReviewAnnotation>())
                        {
                            foreach (var rule in group.Rules)
                            {
                                if (rule.HasAnnotation<ElementRenamedAnnotation>())
                                {
                                    var iniRule = $"{rule.Sources[0].Annotation<ElementDefAnnotation>()?.definition.Path}=@{rule.Targets[0].Annotation<ElementDefAnnotation>()?.definition.Path}";
                                    // var iniRule = $"{rule.Targets[0].Annotation<ElementDefAnnotation>()?.definition.Path}=@{rule.Sources[0].Annotation<ElementDefAnnotation>()?.definition.Path}";
                                    if (exitingMaps.Contains(iniRule))
                                    {
                                        diffs.Add(iniRule);
                                        exitingMaps.Remove(iniRule);
                                    }
                                    else if(exitingMaps.Exists(em => em.StartsWith(iniRule + ",")))
                                    {
                                        var em = exitingMaps.Where(r => r.StartsWith(iniRule));
                                        if (em.Count() == 0)
                                        {
                                            diffs.Add(iniRule);
                                        }
                                        else if (em.Count() == 1)
                                        {
                                            diffs.Add($"{em.First()}");
                                            exitingMaps.Remove(em.First());
                                        }
                                        else
                                        {
                                            diffs.Add($"{em.First()}, ** multiple **");
                                            exitingMaps.Remove(em.First());
                                        }
                                    }
                                    else
                                    {
                                        // is this a mapping that goes from 2 properties to 1 (hence there's a deletion comment mapping for it)
                                        if (exitingMaps.Contains(iniRule.Replace("=@", "=-> ")))
                                        {
                                            iniRule = iniRule.Replace("=@", "=-> ");
                                            diffs.Add(iniRule);
                                            exitingMaps.Remove(iniRule);
                                        }
                                        else
                                        {
                                            var revRule = $"{rule.Targets[0].Annotation<ElementDefAnnotation>()?.definition.Path}=@{rule.Sources[0].Annotation<ElementDefAnnotation>()?.definition.Path}";
                                            if (exitingMaps.Contains(revRule))
                                            {
                                                diffs.Add(iniRule + ", **NEW**  !!! reverse map found");
                                            }
                                            else
                                            {
                                                // This is a regular new mapping
                                                diffs.Add(iniRule + ", **NEW**");
                                            }
                                        }

                                        // also check if there is a comments node already here for the 
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (var change in diffs.OrderBy(s => s))
            {
                Console.WriteLine(change);
            }

            // Prune out known good map content
            foreach (var entry in exitingMaps.ToArray())
            {
                // remove content that should be ignored
                if (entry.Length == 0 || entry.StartsWith("#") || entry.StartsWith(";"))
                {
                    exitingMaps.Remove(entry);
                    continue;
                }

                var iniRule = entry.Split('=')[0];
                var iniValue = entry.Split('=')[1];

                // remove the reason pairs
                if (iniRule.EndsWith(".reasonCode") && iniValue == "-> " + iniRule.Substring(0, iniRule.Length - "Code".Length))
                {
                    exitingMaps.Remove(entry);
                }
                if (iniRule.EndsWith(".reasonReference") && iniValue == "-> " + iniRule.Substring(0, iniRule.Length - "Reference".Length))
                {
                    exitingMaps.Remove(entry);
                }
                if (iniRule.EndsWith(".reason") && iniValue.StartsWith("+ Merged both"))
                {
                    exitingMaps.Remove(entry);
                }

                // remove resource level mappings (as we don't need to report these)
                if (!iniRule.Contains("."))
                {
                    exitingMaps.Remove(entry);
                }
            }


            Console.WriteLine();
            Console.WriteLine("---------------------------");
            Console.WriteLine("Unmatched ini entries:");
            Console.WriteLine("---------------------------");
            foreach (var entry in exitingMaps)
            {
                Console.WriteLine(entry);
            }
        }
    }
}
