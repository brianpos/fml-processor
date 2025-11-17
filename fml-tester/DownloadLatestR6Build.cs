using fml_processor;
using fml_processor.Models;
using Hl7.Fhir.Model;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace fml_tester
{
    [TestClass]
    public sealed class DownloadLatestR6BuildSource
    {
        /// <summary>
        /// Download the latest structure definitions and examples.zip for the specified FHIR version
        /// </summary>
        /// <param name="fhirVersion">e.g. R4, R5, R6</param>
        /// <returns></returns>
        public async Task DownloadStructureDefinitionsAndExamples(string sourceBaseUrl, string fhirVersion)
        {
            string targetFolder = Path.Combine(Path.GetTempPath(), "FML", $"{fhirVersion}-SD");
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);
            Console.Write($"Downloading to {targetFolder}");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("fml-generator", "0.10.0"));

                // Load the type profiles
                string typesUrl = sourceBaseUrl + "profiles-types.json";
                string typesFile = Path.Combine(targetFolder, "profiles-types.json");
                if (!File.Exists(typesFile))
                {
                    var typesData = await client.GetByteArrayAsync(typesUrl);
                    await File.WriteAllBytesAsync(typesFile, typesData);
                }

                // Load the resource profiles
                string resourcesFile = Path.Combine(targetFolder, "profiles-resources.json");
                string resourcesUrl = sourceBaseUrl + "profiles-resources.json";
                if (!File.Exists(resourcesFile))
                {
                    var resourcesData = await client.GetByteArrayAsync(resourcesUrl);
                    await File.WriteAllBytesAsync(resourcesFile, resourcesData);
                }

                // Load the examples ZIP
                string examplesFile = Path.Combine(targetFolder, "examples-json.zip");
                string examplesUrl = sourceBaseUrl + "examples-json.zip";
                if (!File.Exists(examplesFile))
                {
                    var stream = await client.GetStreamAsync(examplesUrl);
                    var outStream = File.OpenWrite(examplesFile);
                    await stream.CopyToAsync(outStream);
                    await outStream.FlushAsync();
                }
            }
        }

        [TestMethod]
        public async Task DownloadR4StructureDefinitions()
        {
            await DownloadStructureDefinitionsAndExamples("https://hl7.org/fhir/R4/", "R4");
        }

        [TestMethod]
        public async Task DownloadR5StructureDefinitions()
        {
            await DownloadStructureDefinitionsAndExamples("https://hl7.org/fhir/R5/", "R5");
        }

        [TestMethod]
        public async Task DownloadR6StructureDefinitions()
        {
            await DownloadStructureDefinitionsAndExamples("https://build.fhir.org/", "R6");
        }

        public void ExtractStructureDefinitions()
        {
            string targetFolder = Path.Combine(Path.GetTempPath(), "FML", "R6-SD");
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);
            Console.WriteLine($"Processing files in {targetFolder}");

            Hl7.Fhir.Serialization.FhirJsonPocoDeserializer ds = new Hl7.Fhir.Serialization.FhirJsonPocoDeserializer(new Hl7.Fhir.Serialization.FhirJsonPocoDeserializerSettings()
            {
                AnnotateResourceParseExceptions = true,
                Validator = null
            });
            var jsonWriter = new Hl7.Fhir.Serialization.FhirJsonSerializer(new Hl7.Fhir.Serialization.SerializerSettings() { Pretty = true });

            // read the type profiles
            string typesFile = Path.Combine(targetFolder, "profiles-types.json");
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
                            Console.WriteLine($"  {sd.Name}\t{sd.Url}");
                            File.WriteAllText(Path.Combine(targetFolder, $"StructureDefinition-{sd.Name}.json"), jsonWriter.SerializeToString(sd));
                        }
                    }
                }
            }

            Console.WriteLine();

            // read the resource profiles
            string resourcesFile = Path.Combine(targetFolder, "profiles-resources.json");
            ts = File.ReadAllBytes(resourcesFile);
            js = new Utf8JsonReader(ts);
            if (ds.TryDeserializeResource(ref js, out resource, out issues))
            {
                // read all the resources in the bundle
                if (resource is Bundle bun)
                {
                    foreach (var entry in bun.Entry)
                    {
                        if (entry.Resource is StructureDefinition sd)
                        {
                            Console.WriteLine($"  {sd.Name}\t{sd.Url}");
                            File.WriteAllText(Path.Combine(targetFolder, $"StructureDefinition-{sd.Name}.json"), jsonWriter.SerializeToString(sd));
                        }
                    }
                }
            }

        }
    }
}
