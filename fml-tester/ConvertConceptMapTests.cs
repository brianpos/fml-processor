using fml_processor;
using fml_processor.Models;
using Hl7.Fhir.Serialization;

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
