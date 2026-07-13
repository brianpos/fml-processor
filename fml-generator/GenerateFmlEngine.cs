using fml_processor;
using fml_processor.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using Microsoft.Health.Fhir.MappingLanguage;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

public class GenerateFmlEngine
{
    public GenerateFmlEngine(string sourceVersion, string targetVersion)
    {
        SourceVersion = sourceVersion;
        TargetVersion = targetVersion;
    }
    string SourceVersion;
    string TargetVersion;

    // Canonical indexed
    public Dictionary<string, StructureDefinition> Source = new Dictionary<string, StructureDefinition>();
    public Dictionary<string, StructureDefinition> Target = new Dictionary<string, StructureDefinition>();

    public IEnumerable<FmlStructureMap> GenerateCrossVersionMaps(bool writeMaps = true, IEnumerable<string> filterTypes = null)
    {
        string sourceFolder = Path.Combine(Path.GetTempPath(), "FML", $"{SourceVersion}-SD");
        if (!Directory.Exists(sourceFolder))
            Directory.CreateDirectory(sourceFolder);
        Console.WriteLine($"Processing source structures in {sourceFolder}");
        string targetFolder = Path.Combine(Path.GetTempPath(), "FML", $"{TargetVersion}-SD");
        if (!Directory.Exists(targetFolder))
            Directory.CreateDirectory(targetFolder);
        Console.WriteLine($"Processing target structures in {targetFolder}");

        FhirJsonPocoDeserializer ds = new FhirJsonPocoDeserializer(new FhirJsonPocoDeserializerSettings()
        {
            AnnotateResourceParseExceptions = true,
            DisableBase64Decoding = true,
            AnnotateLineInfo = false,
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

        // stash the StructureDefinitions loaded to be able to re-use during validation
        Source = fmlCreator.Source;
        Target = fmlCreator.Target;

        Console.WriteLine();
        Console.WriteLine("Structures loaded");
        Console.WriteLine();
        var maps = fmlCreator.GenerateMaps(filterTypes);
        Console.WriteLine("Maps generated");
        Console.WriteLine();
        if (writeMaps)
        {
            WriteMapsAndReviewNotes(maps);
            Console.WriteLine("Maps written");
            Console.WriteLine();
        }
        return maps;
    }

    public void WriteMapsAndReviewNotes(List<FmlStructureMap> maps)
    {

        // Write all the maps to the output folder
        string outputFolder = Path.Combine("c:", "temp", "fhir-cross-version-source",
            $"{SourceVersion}_{TargetVersion}",
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
                LogMapReviewNotes(map, SourceVersion, TargetVersion);
            }

            string outputFilename = Path.Combine(outputFolder, $"{map.Groups[0].Name}_{SourceVersion.Replace("R", "")}to{TargetVersion.Replace("R", "")}.fml");
            File.WriteAllText(outputFilename, fmlText);
        }
    }

    public static void LogMapReviewNotes(FmlStructureMap map, string sourceVersion, string targetVersion)
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

    //private void RecreateR4toR6maps()
    //{
    //    var maps = GenerateCrossVersionMaps("R4", "R6");
    //}


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

    //private void RecreateR4toR6DiffsForCoreSpec()
    //{
    //    var maps = GenerateCrossVersionMaps("R4", "R6", false);
    //    Console.WriteLine("[r4-r6-changes]");
    //    var existingMaps = ReadSection(@"c:/git/hl7/fhir-core-build/source/fhir.ini", "r4-r6-changes");
    //    TraceIniContentForMaps(maps, existingMaps);
    //}

    public void TraceIniContentForMaps(IEnumerable<FmlStructureMap> maps, List<string> exitingMaps)
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
                                else if (exitingMaps.Exists(em => em.StartsWith(iniRule + ",")))
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

    public async Task ValidateFml(IEnumerable<FmlStructureMap> fmlMaps, string sourceVer, string targetVer, IEnumerable<string> filterTypes)
    {
        // Specific Source and Target FHIR Version StructureDefinition Resolvers
        IAsyncResourceResolver sourceResolver = GetFhirVersionResolver(sourceVer);
        IAsyncResourceResolver targetResolver = GetFhirVersionResolver(targetVer);
        var source = new MultiResolver(sourceResolver, targetResolver);

        // Prepare a cache of the TYPE based map groups
        Dictionary<string, GroupDeclaration> namedGroups = new Dictionary<string, GroupDeclaration>();
        Dictionary<string, GroupDeclaration?> typedGroups = new Dictionary<string, GroupDeclaration?>();

        RegisterFhirPathGroups(typedGroups);
        await RegisterTypeMapGroups(fmlMaps, typedGroups, namedGroups, sourceResolver, targetResolver);

        async Task<StructureDefinition?> resolveMapUseCrossVersionType(string url, string? alias)
        {
            if (await sourceResolver.ResolveByCanonicalUriAsync(url) is StructureDefinition sd)
            {
                // Console.WriteLine(" - yup");
                return sd;
            }
            if (await targetResolver.ResolveByCanonicalUriAsync(url) is StructureDefinition sd2)
            {
                // Console.WriteLine(" - yup");
                return sd2;
            }
            Console.WriteLine($"\nError: Resolving Type {url} as {alias} was not found");
            // errorCount++;
            return null;
        }
        IEnumerable<FmlStructureMap> resolveMaps(string url)
        {
            Console.WriteLine($"Resolving Maps {url}");
            return new List<FmlStructureMap>();
        }
        var options = new ValidateMapOptions()
        {
            resolveMapUseCrossVersionType = resolveMapUseCrossVersionType,
            resolveMaps = resolveMaps,
            source = sourceResolver,
            target = targetResolver,
            namedGroups = namedGroups,
            typedGroups = typedGroups,
        };

        Console.WriteLine("\n==========================================================");
        Console.WriteLine("Validate the FML files");
        Console.WriteLine("==========================================================");
        Console.WriteLine();

        string filterToGroup = ""; // "AdverseEvent";
        int issues = 0;
        foreach (var fml in fmlMaps)
        {
            if (!string.IsNullOrEmpty(filterToGroup) && fml.Groups[0].Name != filterToGroup)
                continue;
            if (filterTypes.Any() && !filterTypes.Contains(fml.Groups[0].Name))
                continue;
            // now validate the FML too
            var outcome = await FmlValidator.VerifyFmlDataTypes(fml, options);
            issues += outcome.Issue.Count;

            // FmlValidator.ReorderGroupRules(fml.Groups, options);
        }

        Console.WriteLine("\n==========================================================");
        Console.WriteLine("Prop read/write checks");
        Console.WriteLine("==========================================================");
        Console.WriteLine();

        foreach (var fml in fmlMaps)
        {
            if (!string.IsNullOrEmpty(filterToGroup) && fml.Groups[0].Name != filterToGroup)
                continue;
            if (filterTypes.Any() && !filterTypes.Contains(fml.Groups[0].Name))
                continue;
            DetectMissingProps(options, fml);

            var fmlText = FmlSerializer.Serialize(fml);
            // Console.WriteLine(fmlText);
        }

        // Assert.AreEqual(0, issues, "There were FML validation issues detected");
    }

    public void DetectMissingProps(ValidateMapOptions options, FmlStructureMap fml)
    {
        // walk through the all the properties in the first groups in/out types
        // and check that all are referenced at some point.
        Dictionary<string, ElementDefinition> sourceProps = new Dictionary<string, ElementDefinition>();
        Dictionary<string, ElementDefinition> targetProps = new Dictionary<string, ElementDefinition>();
        List<string> calledGroups = new List<string>();
        var firstGroup = fml.Groups[0];
        if (firstGroup.Parameters.Count >= 2)
        {
            var sourceParam = firstGroup.Parameters[0];
            var targetParam = firstGroup.Parameters[1];
            if (sourceParam.ParameterElementDefinition != null)
            {
                foreach (var ed in sourceParam.ParameterElementDefinition.StructureDefinition.Differential.Element)
                {
                    // the dictionary will have the path of each property, followed by its type(s) seperately.
                    foreach (var type in ed.Type)
                    {
                        string typeKey = $"{ed.Path.Replace("[x]", "")} : {type.Code}";
                        if (!sourceProps.ContainsKey(typeKey))
                            sourceProps.Add(typeKey, ed);
                    }
                }
            }
            if (targetParam.ParameterElementDefinition != null)
            {
                foreach (var ed in targetParam.ParameterElementDefinition.StructureDefinition.Differential.Element)
                {
                    // the dictionary will have the path of each property, followed by its type(s) seperately.
                    foreach (var type in ed.Type)
                    {
                        string typeKey = $"{ed.Path.Replace("[x]", "")} : {type.Code}";
                        if (!targetProps.ContainsKey(typeKey))
                            targetProps.Add(typeKey, ed);
                    }
                }
            }
        }

        // Now walk through all the rules and remove any properties that are mapped
        RemovePropertiesReferenced(firstGroup, sourceProps, targetProps, options, calledGroups);

        if (sourceProps.Any())
        {
            Console.WriteLine("-----------------------------------------------");
            if (sourceProps.Any())
            {
                Console.WriteLine($"The following source properties were not read: {firstGroup.Name}");
                foreach (var sp in sourceProps)
                {
                    Console.WriteLine($"    {sp.Key}[{sp.Value.Min}..{sp.Value.Max}]");
                }
                Console.WriteLine();
            }
            if (targetProps.Any())
            {
                Console.WriteLine($"The following target properties were not populated: {firstGroup.Name}");
                foreach (var sp in targetProps)
                {
                    Console.WriteLine($"    {sp.Key}[{sp.Value.Min}..{sp.Value.Max}]");
                }
                Console.WriteLine();
            }
        }
    }

    private void RemovePropertiesReferenced(GroupDeclaration group, Dictionary<string, ElementDefinition> sourceProps, Dictionary<string, ElementDefinition> targetProps, ValidateMapOptions options, List<string> calledGroups)
    {
        if (calledGroups.Contains(group.Name))
            return;
        calledGroups.Add(group.Name);

        // walk all the rules
        foreach (var rule in group.Rules)
        {
            RemovePropertiesReferenced(group, rule, sourceProps, targetProps, options, calledGroups);
        }

        // call any extends group
        if (!string.IsNullOrEmpty(group.Extends))
        {
            if (options.namedGroups.TryGetValue(group.Extends!, out GroupDeclaration? extGroup))
            {
                if (extGroup != null)
                {
                    RemovePropertiesReferenced(extGroup, sourceProps, targetProps, options, calledGroups);
                }
            }
        }
    }

    private void RemovePropertiesReferenced(GroupDeclaration group, Rule rule, Dictionary<string, ElementDefinition> sourceProps, Dictionary<string, ElementDefinition> targetProps, ValidateMapOptions options, List<string> calledGroups)
    {
        // Check the sources
        if (rule.Sources?.Any() == true)
        {
            foreach (var source in rule.Sources)
            {
                var pta = source.Annotation<PropertyOrTypeDetails>();
                if (pta != null)
                {
                    // check if this prop name/type is in the list of sourceProps still
                    if (pta.Element.Current.Type.Any())
                    {
                        foreach (var t in pta.Element.Current.Type)
                        {
                            var key = $"{pta.PropertyPath} : {t.Code}";
                            if (sourceProps.ContainsKey(key))
                                sourceProps.Remove(key);
                        }
                    }
                    else
                    {
                        // no type listed, maybe this IS the type element itself
                        var key = $"{pta.PropertyPath} : {pta.Element.Current.Path}";
                        if (sourceProps.ContainsKey(key))
                            sourceProps.Remove(key);
                        // properties might already have been removed as they are read multiple times
                        //else
                        //{
                        //    Console.WriteLine($"Unknown type detected - {key}");
                        //}
                    }
                }
            }
        }

        // Check the targets
        if (rule.Targets?.Any() == true)
        {
            foreach (var target in rule.Targets)
            {
                var pta = target.Annotation<PropertyOrTypeDetails>();
                if (pta != null)
                {
                    // check if this prop name/type is in the list of targetProps still
                    if (pta.Element.Current.Type.Any())
                    {
                        foreach (var t in pta.Element.Current.Type)
                        {
                            var key = $"{pta.PropertyPath} : {t.Code}";
                            if (targetProps.ContainsKey(key))
                                targetProps.Remove(key);
                        }
                    }
                    else
                    {
                        // no type listed, maybe this IS the type element itself
                        var key = $"{pta.PropertyPath} : {pta.Element.Current.Path}";
                        if (targetProps.ContainsKey(key))
                            targetProps.Remove(key);
                        else
                        {
                            Console.WriteLine($"Unknown type detected - {key}");
                        }
                    }
                }
            }
        }

        // dependent rules
        if (rule.Dependent?.Rules?.Any() == true)
        {
            foreach (var cr in rule.Dependent.Rules)
            {
                RemovePropertiesReferenced(group, cr, sourceProps, targetProps, options, calledGroups);
            }
        }

        // dependent invocations
        if (rule.Dependent?.Invocations?.Any() == true)
        {
            // this is calling other groups?
            foreach (var dg in rule.Dependent.Invocations)
            {
                if (options.namedGroups.TryGetValue(dg.Name, out GroupDeclaration? depGroup))
                {
                    if (depGroup != null)
                    {
                        RemovePropertiesReferenced(depGroup, sourceProps, targetProps, options, calledGroups);
                    }
                }
            }
        }
    }
    private static void RegisterFhirPathGroups(Dictionary<string, GroupDeclaration?> typedGroups)
    {
        // With a default set of maps from the fhir types to fhirpath primitives
        typedGroups.Add("http://hl7.org/fhirpath/System.Date -> http://hl7.org/fhirpath/System.DateTime", null);
        typedGroups.Add("http://hl7.org/fhirpath/System.DateTime -> http://hl7.org/fhirpath/System.Date", null);
        typedGroups.Add("http://hl7.org/fhirpath/System.String -> http://hl7.org/fhirpath/System.Integer", null);
        typedGroups.Add("http://hl7.org/fhirpath/System.Integer -> http://hl7.org/fhirpath/System.String", null);
        typedGroups.Add("http://hl7.org/fhirpath/System.Integer -> http://hl7.org/fhirpath/System.Decimal", null);
    }

    private static async Task RegisterTypeMapGroups(IEnumerable<FmlStructureMap> maps, Dictionary<string, GroupDeclaration?> typedGroups, Dictionary<string, GroupDeclaration> namedGroups, IAsyncResourceResolver sourceResolver, IAsyncResourceResolver targetResolver)
    {
        async Task<StructureDefinition?> resolveMapUseCrossVersionType(string url, string? alias)
        {
            if (await sourceResolver.ResolveByCanonicalUriAsync(url) is StructureDefinition sd)
            {
                // Console.WriteLine(" - yup");
                return sd;
            }
            if (await targetResolver.ResolveByCanonicalUriAsync(url) is StructureDefinition sd2)
            {
                // Console.WriteLine(" - yup");
                return sd2;
            }
            Console.WriteLine($"\nError: Resolving Type {url} as {alias} was not found");
            // errorCount++;
            return null;
        }

        foreach (var fml in maps)
        {
            foreach (var group in fml.Groups)
            {
                // Console.WriteLine($"{group.TypeMode} {group.Name}");
                if (!namedGroups.ContainsKey(group.Name))
                    namedGroups.Add(group.Name, group);
                else
                {
                    Console.WriteLine($"Error: Duplicate group name: {group.Name}");
                    // errorCount++;
                }

                if (group.TypeMode == GroupTypeMode.TypePlus
                    || group.TypeMode == GroupTypeMode.Types)
                {
                    // Console.Write($"{group.TypeMode} {group.Name}");

                    // Check that all the parameters have type declarations
                    Dictionary<string, StructureDefinition?> aliasedTypes = new();
                    foreach (var use in fml.Structures)
                    {
                        // Console.WriteLine($"Use {use.Key} as {use.Value?.Alias}");
                        var sd = await resolveMapUseCrossVersionType(use.Url.Trim('\"'), use.Alias);
                        if (use.Alias != null)
                            aliasedTypes.Add(use.Alias, sd);
                        else if (sd != null && sd.Name != null)
                        {
                            // before adding it, check to see if this specific one is already registered
                            if (!aliasedTypes.ContainsKey(sd.Name))
                            {
                                aliasedTypes.Add(use.Alias ?? sd.Name, sd);
                            }
                            else
                            {
                                var existingRegisteredSd = aliasedTypes[sd.Name];
                                if (existingRegisteredSd?.Url == sd.Url
                                    && existingRegisteredSd?.Version == sd.Version
                                    && existingRegisteredSd?.FhirVersion == sd.FhirVersion)
                                {
                                    // The same structure definition is already registered
                                }
                                else
                                {
                                    // Duplicate name encountered
                                    Console.WriteLine($"\nError: Duplicate alias detected {sd.Name} at @{use.Position?.StartLine}:{use.Position?.StartColumn}");
                                }
                            }
                        }
                    }
                    string? typeMapping = null;
                    foreach (var gp in group.Parameters)
                    {
                        if (string.IsNullOrEmpty(gp.Type))
                        {
                            Console.WriteLine($"\n    * No type provided for parameter `{gp.Name}`");
                            // errorCount++;
                        }
                        else
                        {
                            string? type = gp.Type;
                            // lookup the type in the aliases
                            var resolver = gp.Mode == ParameterMode.Source ? sourceResolver : targetResolver;
                            if (type != null)
                            {
                                if (!type.Contains('/') && aliasedTypes.ContainsKey(type))
                                {
                                    var sd = aliasedTypes[type];
                                    if (sd != null)
                                    {
                                        var sw = new FmlStructureDefinitionWalker(sd, resolver);
                                        type = $"{sd.Url}|{sd.Version}";
                                        gp.ParameterElementDefinition = sw.Current;
                                    }
                                }
                                else if (type != "string")
                                {
                                    Console.WriteLine($"\nError: Group {group.Name} parameter {gp.Name} at @{gp.Position?.StartLine}:{gp.Position?.StartColumn} has no type `{gp.Type}`");
                                    // errorCount++;
                                }
                            }
                            if (!string.IsNullOrEmpty(typeMapping))
                            {
                                if (group.TypeMode == GroupTypeMode.TypePlus)
                                {
                                    // Console.Write($"\t\t{typeMapping}");
                                    if (typedGroups.ContainsKey(typeMapping))
                                    {
                                        GroupDeclaration? existingGroup = typedGroups[typeMapping];
                                        Console.WriteLine($"    Error: Group {group.Name} @{group.Position?.StartLine}:{group.Position?.StartColumn} duplicates the default type mappings declared in group `{existingGroup?.Name}` @{existingGroup?.Position?.StartLine}:{existingGroup?.Position?.StartColumn}");
                                        // errorCount++;
                                    }
                                    else
                                    {
                                        typedGroups.Add(typeMapping, group);
                                    }
                                }
                                typeMapping += " -> ";
                            }
                            typeMapping += type;
                        }
                    }

                    if (typeMapping == null)
                    {
                        // TODO: @brianpos - is this correct to throw?  cannot have null value in typeMapping for the dictionary calls after this
                        throw new Exception($"    Error: Group {group.Name} has no type mapping!");
                    }

                    // Console.Write($"\t\t{typeMapping}");
                    // Console.Write("\n");

                    if (typedGroups.TryGetValue(typeMapping, out GroupDeclaration? eg))
                    {
                        Console.WriteLine($"    Error: Group {group.Name} duplicates the type mappings declared in group {eg?.Name}");
                        // errorCount++;
                    }
                    else
                    {
                        typedGroups.Add(typeMapping, group);
                    }
                }
                else
                {
                    // Console.WriteLine($"skipping {group.TypeMode} {group.Name}");
                }
            }
        }
    }

    private static void PatchPrimitiveTypes(StructureDefinition sd, string patchCanonical, string patchPath, string patchFhirType, string patchFhirPathType)
    {
        if (sd.Url == patchCanonical)
        {
            // patch the datatype in 
            // patch the differential
            var valueNode = sd.Differential.Element.FirstOrDefault(e => e.Path == patchPath);
            if (valueNode != null)
            {
                if (valueNode.Type.Count == 0)
                {
                    valueNode.Type.Add(new ElementDefinition.TypeRefComponent() { Code = patchFhirPathType });
                    valueNode.Type[0].SetStringExtension("http://hl7.org/fhir/StructureDefinition/structuredefinition-fhir-type", patchFhirType);
                }
                if (String.IsNullOrEmpty(valueNode.Type[0].Code))
                {
                    valueNode.Type[0].Code = patchFhirPathType;
                    valueNode.Type[0].SetStringExtension("http://hl7.org/fhir/StructureDefinition/structuredefinition-fhir-type", patchFhirType);
                }
            }
            // also patch the snapshot
            valueNode = sd.Snapshot.Element.FirstOrDefault(e => e.Path == patchPath);
            if (valueNode != null)
            {
                if (valueNode.Type.Count == 0)
                {
                    valueNode.Type.Add(new ElementDefinition.TypeRefComponent() { Code = patchFhirPathType });
                    valueNode.Type[0].SetStringExtension("http://hl7.org/fhir/StructureDefinition/structuredefinition-fhir-type", patchFhirType);
                }
                if (String.IsNullOrEmpty(valueNode.Type[0].Code))
                {
                    valueNode.Type[0].Code = patchFhirPathType;
                    valueNode.Type[0].SetStringExtension("http://hl7.org/fhir/StructureDefinition/structuredefinition-fhir-type", patchFhirType);
                }
            }
        }
    }


    /// <summary>
    /// </summary>
    /// <param name="fhirVersion">R4/R4B/R5 - The FHIR version identifier used to determine the resource directory. Cannot be null or empty.</param>
    /// <returns>An <see cref="IAsyncResourceResolver"/> instance that resolves resources for the specified FHIR version.</returns>
    public IAsyncResourceResolver GetFhirVersionResolver(string fhirVersion)
    {
        var dirSettings = new DirectorySourceSettings(); // { Includes = ["StructureDefinition-*.*"] };
        dirSettings.ParserSettings.PermissiveParsing = true;
        dirSettings.ParserSettings.AllowUnrecognizedEnums = true;
        dirSettings.ParserSettings.AcceptUnknownMembers = true;
        dirSettings.ParserSettings.ExceptionHandler = (source, args) =>
        {
            // System.Diagnostics.Trace.WriteLine($"Parse error: {args.Message}"); 
        };

        string folder = Path.Combine(Path.GetTempPath(), "FML", $"{fhirVersion}-SD");
        BaseFhirJsonPocoDeserializer js = new BaseFhirJsonPocoDeserializer(Hl7.Fhir.Model.ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings() 
        {
            Validator = null,
            AnnotateLineInfo = false,
            AnnotateResourceParseExceptions = false,
            DisableBase64Decoding = true 
        });

        string jsonTypes = File.ReadAllText(Path.Combine(folder, "profiles-types.json"));
        js.TryDeserializeResource(jsonTypes, out Resource? bundleTypes, out var issues);

        string jsonResources = File.ReadAllText(Path.Combine(folder, "profiles-resources.json"));
        js.TryDeserializeResource(jsonResources, out Resource? bundleResources, out issues);

        var imr = new InMemoryResourceResolver();
        var types = (bundleTypes as Bundle).Entry.Select(e => e.Resource).OfType<StructureDefinition>().Select(e =>
        {
            if (fhirVersion == "R3")
            {
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/boolean", "boolean.value", "boolean", "http://hl7.org/fhirpath/System.Boolean");

                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/string", "string.value", "string", "http://hl7.org/fhirpath/System.String");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/uri", "uri.value", "uri", "http://hl7.org/fhirpath/System.String");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/code", "code.value", "code", "http://hl7.org/fhirpath/System.String");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/oid", "oid.value", "oid", "http://hl7.org/fhirpath/System.String");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/id", "id.value", "id", "http://hl7.org/fhirpath/System.String");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/uuid", "uuid.value", "uuid", "http://hl7.org/fhirpath/System.String");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/markdown", "markdown.value", "markdown", "http://hl7.org/fhirpath/System.String");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/base64Binary", "base64Binary.value", "base64Binary", "http://hl7.org/fhirpath/System.String");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/xhtml", "xhtml.value", "xhtml", "http://hl7.org/fhirpath/System.String");

                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/integer", "integer.value", "integer", "http://hl7.org/fhirpath/System.Integer");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/unsignedInt", "unsignedInt.value", "unsignedInt", "http://hl7.org/fhirpath/System.Integer");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/positiveInt", "positiveInt.value", "positiveInt", "http://hl7.org/fhirpath/System.Integer");

                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/decimal", "decimal.value", "decimal", "http://hl7.org/fhirpath/System.Decimal");

                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/date", "date.value", "date", "http://hl7.org/fhirpath/System.Date");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/dateTime", "dateTime.value", "dateTime", "http://hl7.org/fhirpath/System.DateTime");
                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/instant", "instant.value", "instant", "http://hl7.org/fhirpath/System.DateTime");

                PatchPrimitiveTypes(e, "http://hl7.org/fhir/StructureDefinition/time", "time.value", "time", "http://hl7.org/fhirpath/System.Time");

            }
            return e;
        });
        imr.Add(types);
        var resources = (bundleResources as Bundle).Entry.Select(e => e.Resource).OfType<StructureDefinition>();
        imr.Add(resources);

        //var ds = new Hl7.Fhir.Specification.Source.DirectorySource(folder, dirSettings);
        //ds.Refresh(true);
        string numVersion = fhirVersion switch
        {
            "R3" => "3.0",
            "R4" => "4.0",
            "R4B" => "4.3",
            "R5" => "5.0",
            "R6" => "6.0",
            _ => throw new ArgumentException($"Unsupported FHIR version: {fhirVersion}", nameof(fhirVersion))
        };
        IAsyncResourceResolver resolver = new CachedResolver(
                new VersionFilterResolver(numVersion, imr /*ds*/));
        return resolver;
    }
}
