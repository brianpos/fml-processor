using fml_processor;
using fml_processor.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Microsoft.Health.Fhir.CodeGen.Tests;
using Microsoft.Health.Fhir.MappingLanguage;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace fml_tester
{
    [TestClass]
    public sealed class RecreateFmlTest
    {
        [TestMethod]
        public void RecreateSampleResource()
        {
            string targetFolder = Path.Combine(Path.GetTempPath(), "FML", "R6-SD");
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);
            Console.WriteLine($"Processing target structures in {targetFolder}");
            string sourceFolder = Path.Combine(Path.GetTempPath(), "FML", "R5-SD");
            if (!Directory.Exists(sourceFolder))
                Directory.CreateDirectory(sourceFolder);
            Console.WriteLine($"Processing source structures in {sourceFolder}");

            FhirJsonPocoDeserializer ds = new FhirJsonPocoDeserializer(new FhirJsonPocoDeserializerSettings()
            {
                AnnotateResourceParseExceptions = true,
                Validator = null
            });

            FmlCreator fmlCreator = new FmlCreator();

            // Load Target Structures
            string typesFile = Path.Combine(targetFolder, "profiles-types.json");
            LoadStructures(ds, fmlCreator.Target, typesFile);
            // Console.WriteLine();
            string resourcesFile = Path.Combine(targetFolder, "profiles-resources.json");
            LoadStructures(ds, fmlCreator.Target, resourcesFile);
            LoadStructures(ds, fmlCreator.TargetResources, resourcesFile);

            // Load Source Structures
            // Console.WriteLine();
            typesFile = Path.Combine(sourceFolder, "profiles-types.json");
            LoadStructures(ds, fmlCreator.Source, typesFile);
            // Console.WriteLine();
            resourcesFile = Path.Combine(sourceFolder, "profiles-resources.json");
            LoadStructures(ds, fmlCreator.Source, resourcesFile);
            LoadStructures(ds, fmlCreator.SourceResources, resourcesFile);

            Console.WriteLine();
            var maps = fmlCreator.GenerateMaps();

            Console.WriteLine();
            foreach (var map in maps)
            {
                var fmlText = FmlSerializer.Serialize(map);
                Console.WriteLine(fmlText);
            }
        }

        private static void LoadStructures(FhirJsonPocoDeserializer ds, Dictionary<string, StructureDefinition> col, string typesFile)
        {
            var ts = File.ReadAllBytes(typesFile);
            var js = new Utf8JsonReader(ts);
            if (ds.TryDeserializeResource(ref js, out var resource, out var issues))
            {
                // read all the resources in the bundle
                if (resource is Bundle bun)
                {
                    foreach (var entry in bun.Entry)
                    {
                        if (entry.Resource is StructureDefinition sd)
                        {
                            // Console.WriteLine($"  {sd.Name}\t{sd.Url}");
                            col.Add(sd.Url, sd);
                        }
                    }
                }
            }
        }
    }
}
