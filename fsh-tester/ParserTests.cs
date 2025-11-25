using System.Text;
using fsh_processor.Models;

namespace fsh_tester
{
    [TestClass]
    public sealed class ParserTests
    {
        [TestMethod]
        public void ParseSDCIgSourceFiles()
        {
            string testDataDir = Path.Combine("C:\\git\\hl7\\sdc", "input", "fsh");
            string[] fshFiles = Directory.GetFiles(testDataDir, "*.fsh", SearchOption.AllDirectories);
            
            int totalFiles = 0;
            int totalEntities = 0;
            int failedFiles = 0;
            var entityCounts = new Dictionary<string, int>();
            var parseErrors = new List<string>();
            
            foreach (string fshFile in fshFiles)
            {
                string fshContent = File.ReadAllText(fshFile);
                try
                {
                    var result = fsh_processor.FshParser.Parse(fshContent);
                    Assert.IsNotNull(result, $"Parsed result should not be null for file: {fshFile}");
                    
                    // Check if parse was successful
                    if (result is ParseResult.Success success)
                    {
                        totalFiles++;
                        totalEntities += success.Document.Entities.Count;
                        
                        // Count entity types
                        foreach (var entity in success.Document.Entities)
                        {
                            var entityType = entity.GetType().Name;
                            if (!entityCounts.ContainsKey(entityType))
                                entityCounts[entityType] = 0;
                            entityCounts[entityType]++;
                        }
                    }
                    else if (result is ParseResult.Failure failure)
                    {
                        failedFiles++;
                        var firstError = failure.Errors.FirstOrDefault();
                        var errorMsg = firstError != null 
                            ? $"{Path.GetFileName(fshFile)}: {firstError.Message} at line {firstError.Line}"
                            : $"{Path.GetFileName(fshFile)}: Unknown error";
                        parseErrors.Add(errorMsg);
                    }
                }
                catch (Exception ex)
                {
                    failedFiles++;
                    parseErrors.Add($"{Path.GetFileName(fshFile)}: Exception - {ex.Message}");
                }
            }
            
            
            // Print summary
            Console.WriteLine($"\nParsing Summary:");
            Console.WriteLine($"  Files processed: {totalFiles + failedFiles}");
            Console.WriteLine($"  Successfully parsed: {totalFiles}");
            Console.WriteLine($"  Failed to parse: {failedFiles}");
            Console.WriteLine($"  Total entities: {totalEntities}");
            Console.WriteLine($"\nEntity breakdown:");
            foreach (var kvp in entityCounts.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            if (parseErrors.Count > 0)
            {
                Console.WriteLine($"\nParse errors ({parseErrors.Count}):");
                foreach (var error in parseErrors.Take(10)) // Show first 10 errors
                {
                    Console.WriteLine($"  {error}");
                }
                if (parseErrors.Count > 10)
                {
                    Console.WriteLine($"  ... and {parseErrors.Count - 10} more");
                }
            }
            
            // Assert that we successfully parsed at least some files
            Assert.IsTrue(totalFiles > 0, "Should have successfully parsed at least one file");
        }

        [TestMethod]
        public void ParseSingleFile_SDCTaskQuestionnaire()
        {
            string fshFile = Path.Combine("C:\\git\\hl7\\sdc", "input", "fsh", "profiles", "SDCTaskQuestionnaire.fsh");
            Assert.IsTrue(File.Exists(fshFile), $"Test file not found: {fshFile}");
            
            string fshContent = File.ReadAllText(fshFile);
            var result = fsh_processor.FshParser.Parse(fshContent);
            
            Assert.IsNotNull(result);
            
            if (result is ParseResult.Success success)
            {
                Console.WriteLine($"\nParsed {success.Document.Entities.Count} entities from SDCTaskQuestionnaire.fsh:");
                
                foreach (var entity in success.Document.Entities)
                {
                    var entityType = entity.GetType().Name;
                    Console.WriteLine($"  {entityType}: {entity.Name}");
                    
                    // Show some details for profiles
                    if (entity is Profile profile)
                    {
                        Console.WriteLine($"    Parent: {profile.Parent}");
                        Console.WriteLine($"    Id: {profile.Id}");
                        Console.WriteLine($"    Rules: {profile.Rules.Count}");
                    }
                    else if (entity is Invariant inv)
                    {
                        Console.WriteLine($"    Severity: {inv.Severity}");
                    }
                }
                
                Assert.IsTrue(success.Document.Entities.Count > 0, "Should have parsed at least one entity");
            }
            else if (result is ParseResult.Failure failure)
            {
                var errorDetails = new StringBuilder();
                errorDetails.AppendLine("Parse errors:");
                foreach (var error in failure.Errors)
                {
                    errorDetails.AppendLine($"  Line {error.Line}:{error.Column} - {error.Message}");
                }
                Assert.Fail(errorDetails.ToString());
            }
        }
    }
}
