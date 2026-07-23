using Microsoft.CodeAnalysis;
using System.Data;
using System.Text;

namespace fml_tester
{
    [TestClass]
    public sealed class RecreateFmlTest
    {
        [TestMethod]
        public void RecreateR5toR6maps()
        {
            var engine = new GenerateFmlEngine("R5", "R6");
            var maps = engine.GenerateCrossVersionMaps(OutputPath("R5", "R6"), Path.Combine("testdata", "r5-r6-renames.txt"), Path.Combine("testdata", "r5-r6-custom-rules.fml"));
        }

        [TestMethod]
        public void RecreateR6toR5maps()
        {
            var engine = new GenerateFmlEngine("R6", "R5");
            var maps = engine.GenerateCrossVersionMaps(OutputPath("R6", "R5"), null, null);
        }

        [TestMethod]
        public void RecreateR4toR6maps()
        {
            var engine = new GenerateFmlEngine("R4", "R6");
            var maps = engine.GenerateCrossVersionMaps(OutputPath("R4", "R6"), Path.Combine("testdata", "r4-r6-renames.txt"), Path.Combine("testdata", "r4-r6-custom-rules.fml"));
        }


        [TestMethod]
        public void RecreateR4toR5maps()
        {
            var engine = new GenerateFmlEngine("R4", "R5");
            var maps = engine.GenerateCrossVersionMaps(OutputPath("R4", "R5"), null, null);
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

        [TestMethod]
        public void RecreateR4toR6DiffsForCoreSpec()
        {
            var engine = new GenerateFmlEngine("R4", "R6");
            var maps = engine.GenerateCrossVersionMaps(OutputPath("R4", "R6"), Path.Combine("testdata", "r4-r6-renames.txt"), null);
            Console.WriteLine("[r4-r6-changes]");
            var existingMaps = engine.ReadSection(@"c:/git/hl7/fhir-core-build/source/fhir.ini", "r4-r6-changes");
            engine.TraceIniContentForMaps(maps, existingMaps);
        }

        private string OutputPath(string sourceVersion, string targetVersion)
        {
            string outputFolder = Path.Combine("c:", "temp", "fhir-cross-version-source",
                $"{sourceVersion}_{targetVersion}",
                "maps", "StructureMaps");
            return outputFolder;
        }

        [TestMethod]
        public void RecreateR4BtoR6DiffsForCoreSpec()
        {
            var engine = new GenerateFmlEngine("R4B", "R6");
            var maps = engine.GenerateCrossVersionMaps(OutputPath("R4B", "R6"), null, null);
            Console.WriteLine("[r4-r6-changes]");
            var existingMaps = engine.ReadSection(@"c:/git/hl7/fhir-core-build/source/fhir.ini", "r4-r6-changes");
            engine.TraceIniContentForMaps(maps, existingMaps);
        }

        [TestMethod]
        public void RecreateR5toR6DiffsForCoreSpec()
        {
            var engine = new GenerateFmlEngine("R5", "R6");
            var maps = engine.GenerateCrossVersionMaps(OutputPath("R5", "R6"), Path.Combine("testdata", "r5-r6-renames.txt"), null);
            Console.WriteLine("[r5-r6-changes]");
            var existingMaps = engine.ReadSection(@"c:/git/hl7/fhir-core-build/source/fhir.ini", "r5-r6-changes");
            engine.TraceIniContentForMaps(maps, existingMaps);
        }
    }
}
