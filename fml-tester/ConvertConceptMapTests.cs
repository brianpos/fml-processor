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

			// Prepare a cache of the TYPE based map groups
			Dictionary<string, GroupDeclaration> namedGroups = new Dictionary<string, GroupDeclaration>();
			Dictionary<string, GroupDeclaration?> typedGroups = new Dictionary<string, GroupDeclaration?>();

			// With a default set of maps from the fhir types to fhirpath primitives
			typedGroups.Add("http://hl7.org/fhirpath/System.Date -> http://hl7.org/fhirpath/System.DateTime", null);
			typedGroups.Add("http://hl7.org/fhirpath/System.DateTime -> http://hl7.org/fhirpath/System.Date", null);
			typedGroups.Add("http://hl7.org/fhirpath/System.String -> http://hl7.org/fhirpath/System.Integer", null);
			typedGroups.Add("http://hl7.org/fhirpath/System.Integer -> http://hl7.org/fhirpath/System.String", null);
			typedGroups.Add("http://hl7.org/fhirpath/System.Integer -> http://hl7.org/fhirpath/System.Decimal", null);

			// now validate the FML too
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

			var fmlText = FmlSerializer.Serialize(fml);
			Console.WriteLine(fmlText);

			var filename2 = "C:\\temp\\fhir-cross-version-source\\R5_R6\\maps\\Resources\\ConceptMap-R5-Account-R6-Account.fml";
			File.WriteAllText(filename2, fmlText);
		}

		[TestMethod]
		public void ConvertAllR5_R6ToFml()
		{
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

				var filename2 = filename.Replace("json", "fml");
				File.WriteAllText(filename2, fmlText);
			}
		}
	}
}
