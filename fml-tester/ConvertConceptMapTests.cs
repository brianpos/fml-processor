using fml_processor;
using fml_processor.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Microsoft.Health.Fhir.CodeGen.Tests;
using Microsoft.Health.Fhir.MappingLanguage;
using Task = System.Threading.Tasks.Task;

namespace fml_tester
{
    [TestClass]
    public sealed class ConvertConceptMapTests
	{
        [TestMethod]
        public void ConvertAccount()
        {
			var filename = "C:\\temp\\fhir-cross-version-source\\R5_R6\\maps\\Resources\\ConceptMap-R5-Account-R6-Account.json";
			var cmText = File.ReadAllText(filename);
			var conceptMap = new FhirJsonParser().Parse<Hl7.Fhir.Model.ConceptMap>(cmText);

			ConceptMapConverter converter = new ConceptMapConverter();
			var newCM = converter.Convert(conceptMap);

			var cmText2 = new FhirJsonSerializer(new SerializerSettings() { Pretty = true }).SerializeToString(newCM);
			Console.WriteLine(cmText2);

			var filename2 = "C:\\temp\\fhir-cross-version-source\\R5_R6\\maps\\Resources\\ConceptMap-R5-Account-R6-Account2.json";
			File.WriteAllText(filename2, cmText2);
		}

		[TestMethod]
		public void ConvertAccountToFml()
		{
			var filename = "C:\\temp\\fhir-cross-version-source\\R5_R6\\maps\\Resources\\ConceptMap-R5-Account-R6-Account.json";
			var cmText = File.ReadAllText(filename);
			var conceptMap = new FhirJsonParser().Parse<Hl7.Fhir.Model.ConceptMap>(cmText);

			ConceptMapConverter converter = new ConceptMapConverter();
			var newCM = converter.Convert(conceptMap);

			// now convert this to FML
			var fml = converter.ConvertToFml(newCM);

			var fmlText = FmlSerializer.Serialize(fml);
			Console.WriteLine(fmlText);

			var filename2 = "C:\\temp\\fhir-cross-version-source\\R5_R6\\maps\\Resources\\ConceptMap-R5-Account-R6-Account.fml";
			File.WriteAllText(filename2, fmlText);
		}

		[TestMethod]
		public async Task ValidateAccountFml()
        {
            var filename = "C:\\temp\\fhir-cross-version-source\\R5_R6\\maps\\Resources\\ConceptMap-R5-Account-R6-Account.json";
            var cmText = File.ReadAllText(filename);
            var conceptMap = new FhirJsonParser().Parse<Hl7.Fhir.Model.ConceptMap>(cmText);

            ConceptMapConverter converter = new ConceptMapConverter();
            var newCM = converter.Convert(conceptMap);

            // now convert this to FML
            var fml = converter.ConvertToFml(newCM);

            // Specific Source and Target FHIR Version StructureDefinition Resolvers
            string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            userPath = Path.Combine(userPath, ".fhir", "packages");
            var dirSettings = new DirectorySourceSettings() { Includes = ["StructureDefinition-*.*"] };
            IAsyncResourceResolver sourceResolver = new CachedResolver(
                new VersionFilterResolver("5.0",
                new Hl7.Fhir.Specification.Source.DirectorySource(
                        Path.Combine(userPath, "hl7.fhir.r5.core#5.0.0", "package"), dirSettings)
                    )
                );
            IAsyncResourceResolver targetResolver = new CachedResolver(
                new VersionFilterResolver("6.0",
                    new Hl7.Fhir.Specification.Source.DirectorySource(
                        Path.Combine(userPath, "hl7.fhir.r6.core#current", "package"), dirSettings)
                    )
                );
            var source = new MultiResolver(sourceResolver, targetResolver);
            await ValidateMap(fml, sourceResolver, targetResolver);

            var fmlText = FmlSerializer.Serialize(fml);
            Console.WriteLine(fmlText);

            var filename2 = "C:\\temp\\fhir-cross-version-source\\R5_R6\\maps\\Resources\\ConceptMap-R5-Account-R6-Account.fml";
            File.WriteAllText(filename2, fmlText);
        }

        private static async Task ValidateMap(FmlStructureMap fml, IAsyncResourceResolver sourceResolver, IAsyncResourceResolver targetResolver)
        {
            // Prepare a cache of the TYPE based map groups
            Dictionary<string, GroupDeclaration> namedGroups = new Dictionary<string, GroupDeclaration>();
            Dictionary<string, GroupDeclaration?> typedGroups = new Dictionary<string, GroupDeclaration?>();

            // With a default set of maps from the fhir types to fhirpath primitives
            typedGroups.Add("http://hl7.org/fhirpath/System.Date -> http://hl7.org/fhirpath/System.DateTime", null);
            typedGroups.Add("http://hl7.org/fhirpath/System.DateTime -> http://hl7.org/fhirpath/System.Date", null);
            typedGroups.Add("http://hl7.org/fhirpath/System.String -> http://hl7.org/fhirpath/System.Integer", null);
            typedGroups.Add("http://hl7.org/fhirpath/System.Integer -> http://hl7.org/fhirpath/System.String", null);
            typedGroups.Add("http://hl7.org/fhirpath/System.Integer -> http://hl7.org/fhirpath/System.Decimal", null);

            // Fake some entries for primitive types
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/base64Binary|5.0.0 -> http://hl7.org/fhir/StructureDefinition/base64Binary|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/boolean|5.0.0 -> http://hl7.org/fhir/StructureDefinition/boolean|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/canonical|5.0.0 -> http://hl7.org/fhir/StructureDefinition/canonical|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/code|5.0.0 -> http://hl7.org/fhir/StructureDefinition/code|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/decimal|5.0.0 -> http://hl7.org/fhir/StructureDefinition/decimal|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/date|5.0.0 -> http://hl7.org/fhir/StructureDefinition/date|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/dateTime|5.0.0 -> http://hl7.org/fhir/StructureDefinition/dateTime|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/id|5.0.0 -> http://hl7.org/fhir/StructureDefinition/id|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/instant|5.0.0 -> http://hl7.org/fhir/StructureDefinition/instant|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/integer|5.0.0 -> http://hl7.org/fhir/StructureDefinition/integer|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/integer64|5.0.0 -> http://hl7.org/fhir/StructureDefinition/integer64|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/markdown|5.0.0 -> http://hl7.org/fhir/StructureDefinition/markdown|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/positiveInt|5.0.0 -> http://hl7.org/fhir/StructureDefinition/positiveInt|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/string|5.0.0 -> http://hl7.org/fhir/StructureDefinition/string|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/time|5.0.0 -> http://hl7.org/fhir/StructureDefinition/time|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/oid|5.0.0 -> http://hl7.org/fhir/StructureDefinition/oid|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/unsignedInt|5.0.0 -> http://hl7.org/fhir/StructureDefinition/unsignedInt|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/uuid|5.0.0 -> http://hl7.org/fhir/StructureDefinition/uuid|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/uri|5.0.0 -> http://hl7.org/fhir/StructureDefinition/uri|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/url|5.0.0 -> http://hl7.org/fhir/StructureDefinition/url|6.0.0-ballot3", null);

            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Age|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Age|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Address|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Address|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Annotation|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Annotation|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Attachment|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Attachment|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Availability|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Availability|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/CodeableConcept|5.0.0 -> http://hl7.org/fhir/StructureDefinition/CodeableConcept|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/CodeableReference|5.0.0 -> http://hl7.org/fhir/StructureDefinition/CodeableReference|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Coding|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Coding|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Count|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Count|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/ContactDetail|5.0.0 -> http://hl7.org/fhir/StructureDefinition/ContactDetail|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/ContactPoint|5.0.0 -> http://hl7.org/fhir/StructureDefinition/ContactPoint|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/DataRequirement|5.0.0 -> http://hl7.org/fhir/StructureDefinition/DataRequirement|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Dosage|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Dosage|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Distance|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Distance|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Duration|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Duration|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/ElementDefinition|5.0.0 -> http://hl7.org/fhir/StructureDefinition/ElementDefinition|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Expression|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Expression|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Extension|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Extension|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/ExtendedContactDetail|5.0.0 -> http://hl7.org/fhir/StructureDefinition/ExtendedContactDetail|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/HumanName|5.0.0 -> http://hl7.org/fhir/StructureDefinition/HumanName|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Identifier|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Identifier|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/MonetaryComponent|5.0.0 -> http://hl7.org/fhir/StructureDefinition/MonetaryComponent|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Meta|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Meta|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Money|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Money|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Narrative|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Narrative|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/ParameterDefinition|5.0.0 -> http://hl7.org/fhir/StructureDefinition/ParameterDefinition|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Period|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Period|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Quantity|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Quantity|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Range|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Range|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Ratio|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Ratio|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/RatioRange|5.0.0 -> http://hl7.org/fhir/StructureDefinition/RatioRange|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Reference|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Reference|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/RelatedArtifact|5.0.0 -> http://hl7.org/fhir/StructureDefinition/RelatedArtifact|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/SampledData|5.0.0 -> http://hl7.org/fhir/StructureDefinition/SampledData|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Signature|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Signature|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Timing|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Timing|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/TriggerDefinition|5.0.0 -> http://hl7.org/fhir/StructureDefinition/TriggerDefinition|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/UsageContext|5.0.0 -> http://hl7.org/fhir/StructureDefinition/UsageContext|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/VirtualServiceDetail|5.0.0 -> http://hl7.org/fhir/StructureDefinition/VirtualServiceDetail|6.0.0-ballot3", null);

            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/ProductShelfLife|5.0.0 -> http://hl7.org/fhir/StructureDefinition/ProductShelfLife|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/MarketingStatus|5.0.0 -> http://hl7.org/fhir/StructureDefinition/MarketingStatus|6.0.0-ballot3", null);

            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/date|5.0.0 -> http://hl7.org/fhir/StructureDefinition/dateTime|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Reference|5.0.0 -> http://hl7.org/fhir/StructureDefinition/CodeableReference|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Coding|5.0.0 -> http://hl7.org/fhir/StructureDefinition/CodeableConcept|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/string|5.0.0 -> http://hl7.org/fhir/StructureDefinition/markdown|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/CodeableConcept|5.0.0 -> http://hl7.org/fhir/StructureDefinition/CodeableReference|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/instant|5.0.0 -> http://hl7.org/fhir/StructureDefinition/dateTime|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/id|5.0.0 -> http://hl7.org/fhir/StructureDefinition/string|6.0.0-ballot3", null);
            typedGroups.Add("http://hl7.org/fhir/StructureDefinition/Resource|5.0.0 -> http://hl7.org/fhir/StructureDefinition/Resource|6.0.0-ballot3", null);

            namedGroups.Add("Resource", null);
            namedGroups.Add("DomainResource", null);
            namedGroups.Add("BackboneElement", null);

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
                    Console.Write($"{group.TypeMode} {group.Name}");

                    // Check that all the parameters have type declarations
                    Dictionary<string, StructureDefinition?> aliasedTypes = new();
                    foreach (var use in fml.Structures)
                    {
                        // Console.WriteLine($"Use {use.Key} as {use.Value?.Alias}");
                        var sd = await resolveMapUseCrossVersionType(use.Url.Trim('\"'), use.Alias);
                        if (use.Alias != null)
                            aliasedTypes.Add(use.Alias, sd);
                        else if (sd != null && sd.Name != null)
                            aliasedTypes.Add(use.Alias ?? sd.Name, sd);
                    }
                    string? typeMapping = null;
                    foreach (var gp in group.Parameters)
                    {
                        if (string.IsNullOrEmpty(gp.Type))
                        {
                            Console.WriteLine($"\n    * No type provided for parameter `{gp.Name}`");
                            //errorCount++;
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
                                    //errorCount++;
                                }
                            }
                            if (!string.IsNullOrEmpty(typeMapping))
                            {
                                if (group.TypeMode == GroupTypeMode.TypePlus)
                                {
                                    Console.Write($"\t\t{typeMapping}");
                                    if (typedGroups.ContainsKey(typeMapping))
                                    {
                                        GroupDeclaration? existingGroup = typedGroups[typeMapping];
                                        Console.WriteLine($"    Error: Group {group.Name} @{group.Position?.StartLine}:{group.Position?.StartColumn} duplicates the default type mappings declared in group `{existingGroup?.Name}` @{existingGroup?.Position?.StartLine}:{existingGroup?.Position?.StartColumn}");
                                        //errorCount++;
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

                    Console.Write($"\t\t{typeMapping}");
                    Console.Write("\n");

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

            // now validate the FML too
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
            var outcome = await FmlValidator.VerifyFmlDataTypes(fml, options);
            FmlValidator.ReorderGroupRules(fml.Groups, options);
        }

        [TestMethod]
		public void ConvertAllR5_R6ToFml()
		{
            var outputPath = "C:\\temp\\fhir-cross-version-source\\R5_R6\\maps\\StructureMaps";
            var files = System.IO.Directory.EnumerateFiles("C:\\temp\\fhir-cross-version-source\\R5_R6\\maps\\Resources", "*.json");
			ConceptMapConverter converter = new ConceptMapConverter();
			var jsonParser = new FhirJsonParser();
			foreach (var filename in files)
			{
				var fi = new FileInfo(filename);
				if (fi.Name.EndsWith("2"))
					continue;
				var cmText = File.ReadAllText(filename);
				var conceptMap = jsonParser.Parse<Hl7.Fhir.Model.ConceptMap>(cmText);

				var newCM = converter.Convert(conceptMap);

				// now convert this to FML
				var fml = converter.ConvertToFml(newCM);

				var fmlText = FmlSerializer.Serialize(fml);
				Console.WriteLine(fmlText);

				var filenameOut = Path.Combine(outputPath, $"{fml.Groups[0].Name}_5to6.fml");
				File.WriteAllText(filenameOut, fmlText);
			}
		}

        [TestMethod]
        public async Task ValidateAllR5_R6ToFml()
        {
            var files = System.IO.Directory.EnumerateFiles("C:\\temp\\fhir-cross-version-source\\R5_R6\\maps\\Resources", "*.json");
            var outputPath = "C:\\temp\\fhir-cross-version-source\\R5_R6\\maps\\StructureMaps";
            ConceptMapConverter converter = new ConceptMapConverter();
            var jsonParser = new FhirJsonParser();

            // Specific Source and Target FHIR Version StructureDefinition Resolvers
            string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            userPath = Path.Combine(userPath, ".fhir", "packages");
            var dirSettings = new DirectorySourceSettings() { Includes = ["StructureDefinition-*.*"] };
            IAsyncResourceResolver sourceResolver = new CachedResolver(
                new VersionFilterResolver("5.0",
                new Hl7.Fhir.Specification.Source.DirectorySource(
                        Path.Combine(userPath, "hl7.fhir.r5.core#5.0.0", "package"), dirSettings)
                    )
                );
            IAsyncResourceResolver targetResolver = new CachedResolver(
                new VersionFilterResolver("6.0",
                    new Hl7.Fhir.Specification.Source.DirectorySource(
                        Path.Combine(userPath, "hl7.fhir.r6.core#current", "package"), dirSettings)
                    )
                );
            var source = new MultiResolver(sourceResolver, targetResolver);

            foreach (var filename in files)
            {
                var fi = new FileInfo(filename);
                if (fi.Name.EndsWith("2"))
                    continue;
                var cmText = File.ReadAllText(filename);
                var conceptMap = jsonParser.Parse<Hl7.Fhir.Model.ConceptMap>(cmText);

                var newCM = converter.Convert(conceptMap);

                // now convert this to FML
                var fml = converter.ConvertToFml(newCM);
                await ValidateMap(fml, sourceResolver, targetResolver);

                var fmlText = FmlSerializer.Serialize(fml);
                Console.WriteLine(fmlText);

                var filenameOut = Path.Combine(outputPath, $"{fml.Groups[0].Name}_5to6.fml");
                File.WriteAllText(filenameOut, fmlText);
            }
        }
    }
}
