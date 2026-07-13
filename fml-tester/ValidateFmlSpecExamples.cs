using fml_processor;
using fml_processor.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using Microsoft.Health.Fhir.MappingLanguage;
using Task = System.Threading.Tasks.Task;

namespace fml_tester
{
    [TestClass]
    public class ValidateFmlSpecExamples
    {
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
            BaseFhirJsonPocoDeserializer js = new BaseFhirJsonPocoDeserializer(Hl7.Fhir.Model.ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings() { Validator = null, AnnotateLineInfo = false, AnnotateResourceParseExceptions = false, DisableBase64Decoding = true });

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
                        // check if this propname/type is in the list of sourceProps still
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
                            // properties might allready have been removed as they are read multiple times
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
                        // check if this propname/type is in the list of targetProps still
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

        [TestMethod]
        public async Task ValidateFml_NewSpecExamples()
        {
            await ValidateFml("C:\\git\\hl7-incubators\\fml-incubator\\input\\examples\\", "R5", "R5");
        }

        private async Task ValidateFml(string sourceDirectory, string sourceVer, string targetVer)
        {
            var files = System.IO.Directory.EnumerateFiles(sourceDirectory, "*.fml").Where(n => !n.EndsWith("sm.fml") && !n.EndsWith("endpoint.fml"));

            // Specific Source and Target FHIR Version StructureDefinition Resolvers
            IAsyncResourceResolver sourceResolver = GetFhirVersionResolver(sourceVer);
            IAsyncResourceResolver targetResolver = GetFhirVersionResolver(targetVer);
            var source = new MultiResolver(sourceResolver, targetResolver);

            List<FmlStructureMap> fmlMaps = new List<FmlStructureMap>();

            bool parsingFailed = false;
            foreach (var filename in files)
            {
                try
                {
                    var fmlText = File.ReadAllText(filename);
                    var fml = FmlParser.ParseOrThrow(fmlText);
                    fml.SetAnnotation(new FileInfo(filename));
                    fmlMaps.Add(fml);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse FML file '{filename}': {ex.Message}");
                    parsingFailed = true;
                }
            }
            if (parsingFailed)
            {
                Assert.Fail("One or more FML files failed to parse. See test output for details.");
            }

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

            string filterToGroup = ""; // "AdverseEvent";
            int issues = 0;
            foreach (var fml in fmlMaps)
            {
                if (!string.IsNullOrEmpty(filterToGroup) && fml.Groups[0].Name != filterToGroup)
                    continue;
                // now validate the FML too
                var outcome = await FmlValidator.VerifyFmlDataTypes(fml, options);
                issues += outcome.Issue.Count;
            }

            Assert.AreEqual(0, issues, "There were FML validation issues detected");
        }
    }
}
