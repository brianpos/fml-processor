using System.Text;
using fsh_processor;
using fsh_processor.Models;

namespace fsh_tester
{
    [TestClass]
    public sealed class ParserTests
    {

        [TestMethod]
        public void TestParseOperationParameters()
        {
            var fshText = @"Instance: Questionnaire-populatelink
InstanceOf: OperationDefinition
Usage: #definition
* parameter[+]
  * insert parameter(#identifier, #in, 0, ""1"", #Identifier, ""A logical questionnaire identifier (i.e. `Questionnaire.identifier`\). The server must know the questionnaire or be able to retrieve it from other known repositories."")
";

            var result = FshParser.Parse(fshText);
            Assert.IsInstanceOfType<ParseResult.Success>(result);

            var doc = ((ParseResult.Success)result).Document;
            var instance = doc.Entities[0] as Instance;
            Assert.IsNotNull(instance);
            Assert.AreEqual("Questionnaire-populatelink", instance.Name);
            Assert.AreEqual("OperationDefinition", instance.InstanceOf);
        }

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

        [TestMethod]
        public void ParseSingleFile_shared()
        {
            string fshFile = Path.Combine("C:\\git\\hl7\\sdc", "input", "fsh", "shared.fsh");
            Assert.IsTrue(File.Exists(fshFile), $"Test file not found: {fshFile}");

            string fshContent = File.ReadAllText(fshFile);
            var result = fsh_processor.FshParser.Parse(fshContent);

            Assert.IsNotNull(result);

            if (result is ParseResult.Success success)
            {
                Console.WriteLine($"\nParsed {success.Document.Entities.Count} entities from shared.fsh:");

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
                    else if (entity is RuleSet rs)
                    {
                        Console.WriteLine($"    Parameters: {String.Join(", ", rs.Parameters?.Select(p => p.Value) ?? [])}");
                        if (rs.UnparsedContent != null)
                        {
                            Console.Write($"    Unparsed Content Length: {rs.UnparsedContent.Length}");
                            Console.WriteLine($"        {String.Join("        ", rs.UnparsedContent.Split("\n"))}");
                            Console.WriteLine();
                        }
                        else
                        {
                            Console.WriteLine($"    * Rules: {rs.Rules.Count}");
                        }
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

        [TestMethod]
        public void ParseSingleFile_SDCLibrary()
        {
            string fshFile = Path.Combine("C:\\git\\hl7\\sdc", "input", "fsh", "profiles", "SDCLibrary.fsh");
            Assert.IsTrue(File.Exists(fshFile), $"Test file not found: {fshFile}");

            string fshContent = File.ReadAllText(fshFile);
            var result = fsh_processor.FshParser.Parse(fshContent);

            Console.WriteLine("=== INPUT ===");
            Console.WriteLine(fshContent);
            Console.WriteLine("=== END INPUT ===");

            Assert.IsNotNull(result);

            if (result is ParseResult.Success success)
            {
                Console.WriteLine($"\nParsed {success.Document.Entities.Count} entities from SDCLibrary.fsh:");

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

                var reserialized = FshSerializer.Serialize(success.Document);

                // DEBUG: Output what was reserialized
                Console.WriteLine("=== RE-SERIALIZED OUTPUT ===");
                Console.WriteLine(reserialized);
                Console.WriteLine("=== END RE-SERIALIZED OUTPUT ===");
                // And test writing it back out again too!


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

        [TestMethod]
        public void ParseSingleFile_populatelink()
        {
            string fshFile = Path.Combine("C:\\git\\hl7\\sdc", "input", "fsh", "operations", "Questionnaire-populatelink.fsh");
            Assert.IsTrue(File.Exists(fshFile), $"Test file not found: {fshFile}");

            string fshContent = File.ReadAllText(fshFile);
            var result = fsh_processor.FshParser.Parse(fshContent);

            Assert.IsNotNull(result);

            if (result is ParseResult.Success success)
            {
                Console.WriteLine($"\nParsed {success.Document.Entities.Count} entities from Questionnaire-populatelink.fsh:");

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

        [TestMethod]
        public void ParseSingleFile_demographics()
        {
            string fshFile = Path.Combine("C:\\git\\hl7\\sdc", "input", "fsh", "examples", "demographics.fsh");
            Assert.IsTrue(File.Exists(fshFile), $"Test file not found: {fshFile}");

            string fshContent = File.ReadAllText(fshFile);
            var result = fsh_processor.FshParser.Parse(fshContent);

            Assert.IsNotNull(result);

            if (result is ParseResult.Success success)
            {
                Console.WriteLine($"\nParsed {success.Document.Entities.Count} entities from demographics.fsh:");

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
                    else if (entity is Instance ins)
                    {
                        Console.WriteLine($"    Instance of: {ins.InstanceOf}");
                        Console.WriteLine($"    Usage: {ins.Usage}");
                        Console.WriteLine($"    Rules: {ins.Rules.Count}");
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

        [TestMethod]
        public void ParseSingleFile_phq9_start()
        {
            string fshFile = Path.Combine("C:\\git\\hl7\\sdc", "input", "fsh", "examples", "adaptive-questionnaireresponse-sdc-example-phq9-start.fsh");
            Assert.IsTrue(File.Exists(fshFile), $"Test file not found: {fshFile}");

            string fshContent = File.ReadAllText(fshFile);
            var result = fsh_processor.FshParser.Parse(fshContent);

            Assert.IsNotNull(result);

            if (result is ParseResult.Success success)
            {
                Console.WriteLine($"\nParsed {success.Document.Entities.Count} entities from adaptive-questionnaireresponse-sdc-example-phq9-start.fsh:");

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

        [TestMethod]
        public void ParseSingleFile_SDCExample_WithStandaloneComments()
        {
            // This file previously failed due to standalone comment lines
            string fshFile = Path.Combine("C:\\git\\hl7\\sdc", "input", "fsh", "logicals", "SDCExample.fsh");
            Assert.IsTrue(File.Exists(fshFile), $"Test file not found: {fshFile}");
            
            string fshContent = File.ReadAllText(fshFile);
            Console.WriteLine("=== SOURCE CONTENT ===");
            Console.WriteLine(fshContent);
            Console.WriteLine("=== END SOURCE CONTENT ===");
            Console.WriteLine($"Source has {fshContent.Split('\n').Length} lines");
            var result = fsh_processor.FshParser.Parse(fshContent);
            
            Assert.IsNotNull(result);
            
            if (result is ParseResult.Success success)
            {
                Console.WriteLine($"\nParsed {success.Document.Entities.Count} entities from SDCExample.fsh:");
                
                foreach (var entity in success.Document.Entities)
                {
                    var entityType = entity.GetType().Name;
                    Console.WriteLine($"  {entityType}: {entity.Name}");
                    
                    if (entity is Logical logical)
                    {
                        Console.WriteLine($"    Parent: {logical.Parent}");
                        Console.WriteLine($"    Id: {logical.Id}");
                        Console.WriteLine($"    Title: {logical.Title}");
                        Console.WriteLine($"    Description: {logical.Description}");
                        Console.WriteLine($"    Rules: {logical.Rules.Count}");
                        
                        // Show all rules
                        foreach (var rule in logical.Rules)
                        {
                            var ruleType = rule.GetType().Name;
                            Console.WriteLine($"      {ruleType}");
                        }
                        
                        // Check for AddElementRule
                        var addElementRules = logical.Rules.OfType<AddElementRule>().ToList();
                        if (addElementRules.Any())
                        {
                            Console.WriteLine($"    AddElementRules: {addElementRules.Count}");
                            foreach (var rule in addElementRules)
                            {
                                Console.WriteLine($"      * {rule.Path} {rule.Cardinality} {string.Join(" or ", rule.TargetTypes)} \"{rule.ShortDescription}\"");
                            }
                        }
                    }
                }
                
                Assert.IsTrue(success.Document.Entities.Count > 0, "Should have parsed at least one entity");
                
                // Verify we got the Logical entity
                var logicalEntity = success.Document.Entities.OfType<Logical>().FirstOrDefault();
                Assert.IsNotNull(logicalEntity, "Should have parsed a Logical entity");
                Assert.AreEqual("SDCExample", logicalEntity.Name);
                
                // Verify we parsed the AddElementRule for the gender field
                var genderRule = logicalEntity.Rules.OfType<AddElementRule>()
                    .FirstOrDefault(r => r.Path == "gender");
                Assert.IsNotNull(genderRule, "Should have parsed AddElementRule for gender field");
                Assert.AreEqual("0..1", genderRule.Cardinality);
                Assert.IsTrue(genderRule.TargetTypes.Contains("CodeableConcept"), "Should have CodeableConcept as target type");
                Assert.IsTrue(genderRule.ShortDescription?.Contains("gender") == true, "Should have short description");
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

        [TestMethod]
        public void ParseParameterizedRuleSet()
        {
            // Test parsing of a parameterized rule set from the SDC IG shared.fsh
            string fshContent = @"
RuleSet: parameter(name, use, min, max, type, documentation)
* parameter[+].name = #{name}
* parameter[=].use = #{use}
* parameter[=].min = {min}
* parameter[=].max = ""{max}""
* parameter[=].type = #{type}
* parameter[=].documentation = ""{documentation}""

RuleSet: item([[linkId]], [[text]], [[type]])
* item[+].linkId = ""{linkId}""
* item[=].text = ""{text}""
* item[=].type = #{type}
";

            var result = fsh_processor.FshParser.Parse(fshContent);
            Assert.IsNotNull(result);
            
            if (result is ParseResult.Success success)
            {
                Console.WriteLine($"\nParsed {success.Document.Entities.Count} parameterized rule sets:");
                
                var ruleSets = success.Document.Entities.OfType<RuleSet>().ToList();
                Assert.AreEqual(2, ruleSets.Count, "Should have parsed 2 RuleSets");
                
                // Check first parameterized rule set
                var parameterRuleSet = ruleSets[0];
                Assert.IsTrue(parameterRuleSet.IsParameterized, "First RuleSet should be parameterized");
                Assert.AreEqual("parameter", parameterRuleSet.Name, "First RuleSet name should be 'parameter'");
                Assert.AreEqual(6, parameterRuleSet.Parameters.Count, "First RuleSet should have 6 parameters");
                
                Console.WriteLine($"  RuleSet: {parameterRuleSet.Name}");
                Console.WriteLine($"    IsParameterized: {parameterRuleSet.IsParameterized}");
                Console.WriteLine($"    Parameters ({parameterRuleSet.Parameters.Count}):");
                foreach (var param in parameterRuleSet.Parameters)
                {
                    Console.WriteLine($"      - {param.Value} (Bracketed: {param.IsBracketed})");
                }
                
                // Verify parameter values
                Assert.AreEqual("name", parameterRuleSet.Parameters[0].Value);
                Assert.IsFalse(parameterRuleSet.Parameters[0].IsBracketed, "Plain parameters should not be bracketed");
                Assert.AreEqual("use", parameterRuleSet.Parameters[1].Value);
                Assert.AreEqual("min", parameterRuleSet.Parameters[2].Value);
                Assert.AreEqual("max", parameterRuleSet.Parameters[3].Value);
                Assert.AreEqual("type", parameterRuleSet.Parameters[4].Value);
                Assert.AreEqual("documentation", parameterRuleSet.Parameters[5].Value);
                
                // Check second parameterized rule set
                var itemRuleSet = ruleSets[1];
                Assert.IsTrue(itemRuleSet.IsParameterized, "Second RuleSet should be parameterized");
                Assert.AreEqual("item", itemRuleSet.Name, "Second RuleSet name should be 'item'");
                Assert.AreEqual(3, itemRuleSet.Parameters.Count, "Second RuleSet should have 3 parameters");
                
                Console.WriteLine($"\n  RuleSet: {itemRuleSet.Name}");
                Console.WriteLine($"    IsParameterized: {itemRuleSet.IsParameterized}");
                Console.WriteLine($"    Parameters ({itemRuleSet.Parameters.Count}):");
                foreach (var param in itemRuleSet.Parameters)
                {
                    Console.WriteLine($"      - {param.Value} (Bracketed: {param.IsBracketed})");
                }
                
                // Verify parameter values
                Assert.AreEqual("linkId", itemRuleSet.Parameters[0].Value);
                Assert.IsTrue(itemRuleSet.Parameters[0].IsBracketed, "Bracketed parameters should be marked as bracketed");
                Assert.AreEqual("text", itemRuleSet.Parameters[1].Value);
                Assert.IsTrue(itemRuleSet.Parameters[1].IsBracketed);
                Assert.AreEqual("type", itemRuleSet.Parameters[2].Value);
                Assert.IsTrue(itemRuleSet.Parameters[2].IsBracketed);
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

        [TestMethod]
        public void ParseRealParameterizedRuleSets_FromSDCIG()
        {
            // Test parsing of real parameterized rule sets from the SDC IG shared.fsh file
            string fshFile = Path.Combine("C:\\git\\hl7\\sdc", "input", "fsh", "shared.fsh");
            Assert.IsTrue(File.Exists(fshFile), $"Test file not found: {fshFile}");
            
            string fshContent = File.ReadAllText(fshFile);
            var result = fsh_processor.FshParser.Parse(fshContent);
            
            Assert.IsNotNull(result);
            
            if (result is ParseResult.Success success)
            {
                var ruleSets = success.Document.Entities.OfType<RuleSet>().ToList();
                var parameterizedRuleSets = ruleSets.Where(rs => rs.IsParameterized).ToList();
                
                Console.WriteLine($"\nParsed {ruleSets.Count} total RuleSets from shared.fsh:");
                Console.WriteLine($"  Regular RuleSets: {ruleSets.Count - parameterizedRuleSets.Count}");
                Console.WriteLine($"  Parameterized RuleSets: {parameterizedRuleSets.Count}");
                
                Assert.IsTrue(parameterizedRuleSets.Count > 0, "Should have at least one parameterized RuleSet");
                
                // Check the 'parameter' ruleset
                var parameterRuleSet = parameterizedRuleSets.FirstOrDefault(rs => rs.Name == "parameter");
                Assert.IsNotNull(parameterRuleSet, "Should have found 'parameter' RuleSet");
                Assert.AreEqual(6, parameterRuleSet.Parameters.Count, "parameter RuleSet should have 6 parameters");
                
                Console.WriteLine($"\n  RuleSet: {parameterRuleSet.Name}");
                Console.WriteLine($"    Parameters: {string.Join(", ", parameterRuleSet.Parameters.Select(p => p.Value))}");
                
                // Verify parameters
                var expectedParams = new[] { "name", "use", "min", "max", "type", "documentation" };
                for (int i = 0; i < expectedParams.Length; i++)
                {
                    Assert.AreEqual(expectedParams[i], parameterRuleSet.Parameters[i].Value, 
                        $"Parameter {i} should be '{expectedParams[i]}'");
                }
                
                
                // Check the 'item' ruleset  
                var itemRuleSet = parameterizedRuleSets.FirstOrDefault(rs => rs.Name == "item");
                Assert.IsNotNull(itemRuleSet, "Should have found 'item' RuleSet");
                Assert.AreEqual(4, itemRuleSet.Parameters.Count, "item RuleSet should have 4 parameters");
                
                Console.WriteLine($"\n  RuleSet: {itemRuleSet.Name}");
                Console.WriteLine($"    Parameters: {string.Join(", ", itemRuleSet.Parameters.Select(p => p.Value))}");
                
                // Show all parameterized rulesets
                Console.WriteLine($"\n  All Parameterized RuleSets:");
                foreach (var rs in parameterizedRuleSets)
                {
                    Console.WriteLine($"    - {rs.Name}({string.Join(", ", rs.Parameters.Select(p => p.Value))})");
                }
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
