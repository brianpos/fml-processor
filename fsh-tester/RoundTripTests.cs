using fsh_processor;
using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fsh_tester;

[TestClass]
public class RoundTripTests
{
    [TestMethod]
    public void TestBasicSerialization()
    {
        // Basic round-trip: Parse ? Serialize ? Re-parse ? Verify structure preserved
        var fshText = @"
Alias: $SCT = http://snomed.info/sct

Profile: MyPatient
Parent: Patient
Id: my-patient
Title: ""My Patient Profile""
Description: ""A simple patient profile for testing""

* name 1..* MS
* identifier 0..* MS
* birthDate 1..1 MS
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        Assert.IsNotNull(doc);
        Assert.AreEqual(2, doc.Entities.Count);

        // Serialize back to FSH
        var serialized = FshSerializer.Serialize(doc);
        Assert.IsNotNull(serialized);

        // Re-parse the serialized output
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reparsedDoc = ((ParseResult.Success)reParseResult).Document;
        Assert.IsNotNull(reparsedDoc);
        
        // Verify structure preserved
        Assert.AreEqual(doc.Entities.Count, reparsedDoc.Entities.Count);
        
        // Verify alias
        var alias = doc.Entities[0] as Alias;
        var reparsedAlias = reparsedDoc.Entities[0] as Alias;
        Assert.IsNotNull(alias);
        Assert.IsNotNull(reparsedAlias);
        Assert.AreEqual(alias.Name, reparsedAlias.Name);
        Assert.AreEqual(alias.Value, reparsedAlias.Value);
        
        // Verify profile
        var profile = doc.Entities[1] as Profile;
        var reparsedProfile = reparsedDoc.Entities[1] as Profile;
        Assert.IsNotNull(profile);
        Assert.IsNotNull(reparsedProfile);
        Assert.AreEqual(profile.Name, reparsedProfile.Name);
        Assert.AreEqual(profile.Parent, reparsedProfile.Parent);
        Assert.AreEqual(profile.Id, reparsedProfile.Id);
        Assert.AreEqual(profile.Title, reparsedProfile.Title);
        Assert.AreEqual(profile.Description, reparsedProfile.Description);
        Assert.AreEqual(profile.Rules.Count, reparsedProfile.Rules.Count);
    }

    [TestMethod]
    public void TestSerializeAlias()
    {
        var fshText = "Alias: $SCT = http://snomed.info/sct";
        
        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        var serialized = FshSerializer.Serialize(doc);
        
        // Verify the alias is properly formatted
        Assert.IsTrue(serialized.Contains("Alias: $SCT = http://snomed.info/sct"));
    }

    [TestMethod]
    public void TestSerializeProfile()
    {
        var fshText = @"Profile: MyProfile
Parent: Patient
Id: my-profile
Title: ""My Profile""
Description: ""Test profile""

* name 1..1 MS
* identifier 0..* MS
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        var serialized = FshSerializer.Serialize(doc);
        
        // Verify key elements are present
        Assert.IsTrue(serialized.Contains("Profile: MyProfile"));
        Assert.IsTrue(serialized.Contains("Parent: Patient"));
        Assert.IsTrue(serialized.Contains("Id: my-profile"));
        Assert.IsTrue(serialized.Contains("Title: \"My Profile\""));
        Assert.IsTrue(serialized.Contains("Description: \"Test profile\""));
        Assert.IsTrue(serialized.Contains("* name 1..1 MS"));
        Assert.IsTrue(serialized.Contains("* identifier 0..* MS"));
    }

    [TestMethod]
    public void TestSerializeExtension()
    {
        var fshText = @"Extension: MyExtension
Id: my-extension
Title: ""My Extension""
Description: ""Test extension""
Context: Patient, Observation

* value[x] 1..1 MS
* valueString 0..1
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        var serialized = FshSerializer.Serialize(doc);
        
        // Re-parse and verify
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reparsedDoc = ((ParseResult.Success)reParseResult).Document;
        var ext = reparsedDoc.Entities[0] as Extension;
        Assert.IsNotNull(ext);
        Assert.AreEqual("MyExtension", ext.Name);
        Assert.AreEqual(2, ext.Contexts?.Count);
    }

    [TestMethod]
    public void TestSerializeInstance()
    {
        var fshText = @"Instance: MyPatient
InstanceOf: Patient
Usage: #example
Title: ""My Patient Instance""

* name.family = ""Smith""
* name.given = ""John""
* birthDate = 1980-01-01
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        var serialized = FshSerializer.Serialize(doc);
        
        // Re-parse and verify
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reparsedDoc = ((ParseResult.Success)reParseResult).Document;
        var instance = reparsedDoc.Entities[0] as Instance;
        Assert.IsNotNull(instance);
        Assert.AreEqual("MyPatient", instance.Name);
        Assert.AreEqual("Patient", instance.InstanceOf);
        Assert.AreEqual("#example", instance.Usage);
    }

    [TestMethod]
    public void TestSerializeValueSet()
    {
        var fshText = @"ValueSet: MyValueSet
Id: my-valueset
Title: ""My ValueSet""

* include codes from system http://snomed.info/sct where concept is-a #73211009
* exclude http://snomed.info/sct#12345
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        var serialized = FshSerializer.Serialize(doc);
        
        // Re-parse and verify
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reparsedDoc = ((ParseResult.Success)reParseResult).Document;
        var vs = reparsedDoc.Entities[0] as ValueSet;
        Assert.IsNotNull(vs);
        Assert.AreEqual("MyValueSet", vs.Name);
    }

    [TestMethod]
    public void TestSerializeCodeSystem()
    {
        var fshText = @"CodeSystem: MyCodeSystem
Id: my-codesystem
Title: ""My CodeSystem""

* #code1 ""Display 1"" ""Definition 1""
* #code2 ""Display 2""
* #code3
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        var serialized = FshSerializer.Serialize(doc);
        
        // Re-parse and verify
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reparsedDoc = ((ParseResult.Success)reParseResult).Document;
        var cs = reparsedDoc.Entities[0] as CodeSystem;
        Assert.IsNotNull(cs);
        Assert.AreEqual("MyCodeSystem", cs.Name);
        Assert.AreEqual(3, cs.Rules.Count);
    }

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
    public void TestSerializeLogical()
    {
        var fshText = @"Logical: MyLogical
Parent: Element
Id: my-logical
Characteristics: #can-be-target

* element1 1..1 SU string ""Element 1"" ""First element""
* element2 0..* BackboneElement ""Element 2""
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        var serialized = FshSerializer.Serialize(doc);
        
        // Re-parse and verify
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reparsedDoc = ((ParseResult.Success)reParseResult).Document;
        var logical = reparsedDoc.Entities[0] as Logical;
        Assert.IsNotNull(logical);
        Assert.AreEqual("MyLogical", logical.Name);
        Assert.AreEqual(1, logical.Characteristics?.Count);
    }

    [TestMethod]
    public void TestSerializeRuleSet()
    {
        var fshText = @"RuleSet: CommonRules
* status 1..1 MS
* code 1..1 MS
* subject 1..1 MS
  * reference 0..1
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        var serialized = FshSerializer.Serialize(doc);
        
        // DEBUG: Output what was serialized
        Console.WriteLine("=== SERIALIZED OUTPUT ===");
        Console.WriteLine(serialized);
        Console.WriteLine("=== END SERIALIZED OUTPUT ===");
        
        // Re-parse and verify
        var reParseResult = FshParser.Parse(serialized);
        if (reParseResult is ParseResult.Failure failure)
        {
            Console.WriteLine($"Re-parse failed: {failure.Errors.Count} errors");
            foreach (var error in failure.Errors)
            {
                Console.WriteLine($"  Line {error.Line}:{error.Column} - {error.Message}");
            }
        }
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reparsedDoc = ((ParseResult.Success)reParseResult).Document;
        var ruleSet = reparsedDoc.Entities[0] as RuleSet;
        Assert.IsNotNull(ruleSet);
        Assert.AreEqual("CommonRules", ruleSet.Name);
        Assert.AreEqual(4, ruleSet.Rules.Count);
    }

    [TestMethod]
    public void TestSerializeParameterizedRuleSet()
    {
        var fshText = @"RuleSet: taskInputUrl(type,  valueUrl)
* input[+]
  * type = {type}
  * valueUrl = ""{valueUrl}""
";

        var result = FshParser.Parse(fshText);
        if (result is ParseResult.Failure failure)
        {
            Console.WriteLine($"Initial parse failed: {failure.Errors.Count} errors");
            foreach (var error in failure.Errors)
            {
                Console.WriteLine($"  Line {error.Line}:{error.Column} - {error.Message}");
            }
        }
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        
        // DEBUG: Check what we parsed
        var ruleSet = doc.Entities[0] as RuleSet;
        Console.WriteLine($"Parsed RuleSet: {ruleSet?.Name}, IsParameterized: {ruleSet?.IsParameterized}, Params: {ruleSet?.Parameters?.Count}");
        
        var serialized = FshSerializer.Serialize(doc);
        
        // DEBUG: Output what was serialized
        Console.WriteLine("=== SERIALIZED OUTPUT ===");
        Console.WriteLine(serialized);
        Console.WriteLine("=== END SERIALIZED OUTPUT ===");
        
        // Re-parse and verify
        var reParseResult = FshParser.Parse(serialized);
        if (reParseResult is ParseResult.Failure reFailure)
        {
            Console.WriteLine($"Re-parse failed: {reFailure.Errors.Count} errors");
            foreach (var error in reFailure.Errors)
            {
                Console.WriteLine($"  Line {error.Line}:{error.Column} - {error.Message}");
            }
        }
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reparsedDoc = ((ParseResult.Success)reParseResult).Document;
        var reparsedRuleSet = reparsedDoc.Entities[0] as RuleSet;
        Assert.IsNotNull(reparsedRuleSet);
        Assert.IsTrue(reparsedRuleSet.IsParameterized);
        Assert.AreEqual(2, reparsedRuleSet.Parameters?.Count);
        Assert.AreEqual("type", reparsedRuleSet.Parameters?[0].Value);
        Assert.AreEqual("valueUrl", reparsedRuleSet.Parameters?[1].Value);
    }

    [TestMethod]
    public void TestSerializeMapping()
    {
        var fshText = @"Mapping: MyMapping
Id: my-mapping
Source: MyProfile
Target: ""http://example.org/target""

* name -> ""target.name""
* identifier -> ""target.id"" ""application/fhir""
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        var serialized = FshSerializer.Serialize(doc);
        
        // Re-parse and verify
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reparsedDoc = ((ParseResult.Success)reParseResult).Document;
        var mapping = reparsedDoc.Entities[0] as Mapping;
        Assert.IsNotNull(mapping);
        Assert.AreEqual("MyMapping", mapping.Name);
    }

    [TestMethod]
    public void TestCommentPreservation()
    {
        var fshText = @"// Header comment
Alias: $SCT = http://snomed.info/sct // inline comment

// Profile comment
Profile: MyProfile
Parent: Patient

* name 1..1 MS
* identifier 0..* MS
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        
        // Verify hidden tokens captured
        Assert.IsNotNull(doc.LeadingHiddenTokens);
        Assert.IsTrue(doc.LeadingHiddenTokens.Any(t => t.Text.Contains("Header comment")));
        
        var alias = doc.Entities[0] as Alias;
        Assert.IsNotNull(alias?.TrailingHiddenTokens);
        Assert.IsTrue(alias.TrailingHiddenTokens.Any(t => t.Text.Contains("inline comment")));
        
        // Serialize and verify comments preserved
        var serialized = FshSerializer.Serialize(doc);
        Console.WriteLine(serialized);
        Assert.IsTrue(serialized.Contains("// Header comment"));
        Assert.IsTrue(serialized.Contains("// inline comment"));
        Assert.IsTrue(serialized.Contains("// Profile comment"));
    }

    [TestMethod]
    public void TestRoundTripPreservesSemantics()
    {
        var fshText = @"Profile: MyObservation
Parent: Observation
Id: my-observation

* status 1..1 MS
* code from http://example.org/vs (required)
* value[x] 0..1
* valueQuantity only Quantity
* component contains systolic 1..1 and diastolic 1..1
* component[systolic].code = http://loinc.org#8480-6
* component[diastolic].code = http://loinc.org#8462-4
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        
        // DEBUG: Check original rules
        var originalProfile = doc.Entities[0] as Profile;
        Console.WriteLine("=== ORIGINAL RULES ===");
        for (int i = 0; i < originalProfile.Rules.Count; i++)
        {
            var rule = originalProfile.Rules[i];
            Console.WriteLine($"{i}: {rule.GetType().Name} - Path: {rule.Path}");
        }
        Console.WriteLine("=== END ORIGINAL RULES ===");
        
        var serialized = FshSerializer.Serialize(doc);
        
        // Re-parse
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reparsedDoc = ((ParseResult.Success)reParseResult).Document;
        var profile = reparsedDoc.Entities[0] as Profile;
        Assert.IsNotNull(profile);

        // DEBUG: Output what was serialized
        Console.WriteLine("=== SERIALIZED OUTPUT ===");
        Console.WriteLine(serialized);
        Console.WriteLine("=== END SERIALIZED OUTPUT ===");

        // Verify all rules preserved
        Assert.AreEqual(7, profile.Rules.Count);
        
        // Verify specific rule types
        Assert.IsInstanceOfType<CardRule>(profile.Rules[0]);
        Assert.IsInstanceOfType<ValueSetRule>(profile.Rules[1]);
        Assert.IsInstanceOfType<CardRule>(profile.Rules[2]);
        Assert.IsInstanceOfType<OnlyRule>(profile.Rules[3]);
        Assert.IsInstanceOfType<ContainsRule>(profile.Rules[4]);
    }

    [TestMethod]
    public void TestRoundTripAllSDCFiles()
    {
        // Get all FSH files from SDC IG
        var sdcPath = @"C:\git\hl7\sdc\input\fsh";
        
        if (!Directory.Exists(sdcPath))
        {
            Assert.Inconclusive($"SDC IG directory not found at {sdcPath}. Skipping batch test.");
            return;
        }

        var fshFiles = Directory.GetFiles(sdcPath, "*.fsh", SearchOption.AllDirectories);
        Assert.IsTrue(fshFiles.Length > 0, "No FSH files found in SDC IG");

        int successCount = 0;
        int failCount = 0;
        var failures = new List<string>();

        foreach (var fshFile in fshFiles)
        {
            try
            {
                var fshText = File.ReadAllText(fshFile);
                
                // Parse original
                var result = FshParser.Parse(fshText);
                if (result is not ParseResult.Success success)
                {
                    var failure = (ParseResult.Failure)result;
                    var errorMsg = failure.Errors.Count > 0 ? failure.Errors[0].Message : "Unknown error";
                    failures.Add($"{Path.GetFileName(fshFile)}: Parse failed - {errorMsg}");
                    failCount++;
                    continue;
                }

                // Serialize
                var serialized = FshSerializer.Serialize(success.Document);

                // Re-parse
                var reParseResult = FshParser.Parse(serialized);
                if (reParseResult is not ParseResult.Success reSuccess)
                {
                    var failure = (ParseResult.Failure)reParseResult;
                    var errorMsg = failure.Errors.Count > 0 ? failure.Errors[0].Message : "Unknown error";
                    failures.Add($"{Path.GetFileName(fshFile)}: Re-parse failed - {errorMsg}");
                    failCount++;
                    continue;
                }

                // Verify entity count matches
                if (success.Document.Entities.Count != reSuccess.Document.Entities.Count)
                {
                    failures.Add($"{Path.GetFileName(fshFile)}: Entity count mismatch - original {success.Document.Entities.Count}, re-parsed {reSuccess.Document.Entities.Count}");
                    failCount++;
                    continue;
                }

                successCount++;
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(fshFile)}: Exception - {ex.Message}");
                failCount++;
            }
        }

        // Report results
        Console.WriteLine($"\nRound-trip test results:");
        Console.WriteLine($"Total files: {fshFiles.Length}");
        Console.WriteLine($"Success: {successCount}");
        Console.WriteLine($"Failed: {failCount}");
        
        if (failures.Count > 0)
        {
            Console.WriteLine($"\nFailures:");
            foreach (var failure in failures)
            {
                Console.WriteLine($"  - {failure}");
            }
        }

        // Assert overall success
        Assert.IsTrue(successCount > 0, "No files successfully round-tripped");
        Assert.AreEqual(0, failCount, $"{failCount} files failed to round-trip. See test output for details.");
    }

    [TestMethod]
    public void TestTextBasedRoundTrip()
    {
        // Text-based comparison - verify serialized output can be re-parsed to same structure
        var fshText = @"Profile: MyProfile
Parent: Patient

* name 1..1 MS
* identifier 0..* MS
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        var serialized = FshSerializer.Serialize(doc);
        
        // Re-parse
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        // Serialize again
        var reserialized = FshSerializer.Serialize(((ParseResult.Success)reParseResult).Document);
        
        // Normalize whitespace for comparison
        var normalized1 = NormalizeWhitespace(serialized);
        var normalized2 = NormalizeWhitespace(reserialized);
        
        // Should be identical after normalization
        Assert.AreEqual(normalized1, normalized2, "Re-serialization produced different output");
    }

    [TestMethod]
    public void TestSerializeNullInputThrows()
    {
        try
        {
            FshSerializer.Serialize(null!);
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void TestSerializeMinimalDocument()
    {
        var fshText = "Alias: $SCT = http://snomed.info/sct";
        
        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result);
        
        var doc = ((ParseResult.Success)result).Document;
        var serialized = FshSerializer.Serialize(doc);
        
        Assert.IsNotNull(serialized);
        Assert.IsTrue(serialized.Length > 0);
    }

    #region Helper Methods

    private static string NormalizeWhitespace(string text)
    {
        // Normalize line endings
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        
        // Normalize multiple blank lines to single blank line
        while (text.Contains("\n\n\n"))
        {
            text = text.Replace("\n\n\n", "\n\n");
        }
        
        // Trim trailing whitespace from each line
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }
        
        return string.Join('\n', lines).Trim();
    }

    #endregion
}
