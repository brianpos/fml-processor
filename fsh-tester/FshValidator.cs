using fsh_processor;
using fsh_processor.Models;
using Hl7.Fhir.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data;
using System.Security.Cryptography;
using static fsh_processor.Models.ParseResult;
using static System.Net.Mime.MediaTypeNames;

namespace fsh_tester;

[TestClass]
public class FshValidationTests
{
    [TestMethod]
    public void TestAllRuleSets()
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

        List<FshDoc> fshDocs = new();
        foreach (var fshFile in fshFiles)
        {
            try
            {
                var fshText = File.ReadAllText(fshFile);

                // Parse original
                var result = FshParser.Parse(fshText);
                if (result is ParseResult.Success success)
                {
                    fshDocs.Add(success.Document);
                    success.Document.Entities.ForEach(e => e.AddAnnotation(new FileInfo(fshFile)));
                    successCount++;
                    continue;
                }
                if (result is ParseResult.Failure failure)
                {
                    var errorMsg = failure.Errors.Count > 0 ? failure.Errors[0].Message : "Unknown error";
                    failures.Add($"{Path.GetFileName(fshFile)}: Parse failed - {errorMsg}");
                    failCount++;
                    continue;
                }
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

        // Now we have all the FSH files loaded, lets start actually processing the content
        // Scan for all the aliases
        Console.WriteLine();
        Dictionary<string, string> aliasDict = new();
        var aliases = fshDocs.SelectMany(f => f.Entities.OfType<Alias>());
        foreach (var alias in aliases)
        {
            if (!aliasDict.ContainsKey(alias.Name))
            {
                aliasDict.Add(alias.Name, alias.Value);
                // Console.WriteLine($"Alias: {alias.Name} = {alias.Value}");
            }
            else
            {
                Console.WriteLine($"Duplicate Alias found: {alias.Name} = {alias.Value} (existing: {aliasDict[alias.Name]})");
            }
        }

        // Locate all the RuleSets
        Console.WriteLine();
        Dictionary<string, RuleSet> rsDict = new();
        var rss = fshDocs.SelectMany(f => f.Entities.OfType<RuleSet>());
        foreach (var rs in rss)
        {
            if (!rsDict.ContainsKey(rs.Name))
            {
                rsDict.Add(rs.Name, rs);
                if (rs.Parameters != null)
                    Console.WriteLine($"{rs.Name}({String.Join(", ", rs.Parameters?.Select(p => p.Value) ?? [])}) {rs.Annotation<FileInfo>()?.Name}");
                else
                    Console.WriteLine($"{rs.Name}  {rs.Annotation<FileInfo>()?.Name}");

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
            else
            {
                Console.WriteLine($"Duplicate RuleSet name: {rs.Name}");
            }
        }

        // Perform all the Ruleset substitutions
        Console.WriteLine();
        foreach (var entity in fshDocs.SelectMany(f => f.Entities))
        {
            if (entity is Profile p)
            {
                Console.WriteLine($"Processing Profile: {p.Name}");
                foreach (var rule in p.Rules.ToArray())
                {
                    if (rule is InsertRule rsRule)
                    {
                        Console.WriteLine($"  Found RuleSetRule: {rsRule.RuleSetReference}");
                        if (rsDict.ContainsKey(rsRule.RuleSetReference))
                        {
                            var ruleSet = rsDict[rsRule.RuleSetReference];
                            Console.WriteLine($"    Expanding RuleSet: {ruleSet.Name}");
                            // Here we would perform the actual rule expansion and substitution
                            // For this example, we just print out the rules
                            foreach (var rsSubRule in ruleSet.Rules)
                            {
                                Console.WriteLine($"      Injecting Rule: {rsSubRule.ToString()}");
                            }
                            if (ruleSet.UnparsedContent != null)
                            {
                                var content = ruleSet.UnparsedContent;
                                if (ruleSet.Parameters != null && ruleSet.Parameters.Count > 0 && rsRule.Parameters != null)
                                {
                                    for (int i = 0; i < ruleSet.Parameters.Count; i++)
                                    {
                                        var paramName = ruleSet.Parameters[i].Value;
                                        if (i < rsRule.Parameters.Count)
                                        {
                                            var paramValue = rsRule.Parameters[i];
                                            content = content.Replace($"{{{paramName}}}", paramValue);
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Error: No {paramName} parameter provided in {p.Annotation<FileInfo>()?.Name} {rsRule.Position}");
                                        }
                                    }
                                    if (rsRule.Parameters.Count > ruleSet.Parameters.Count)
                                    {
                                        Console.WriteLine($"Warning: Additional un-used parameters to {ruleSet.Name} RuleSet in {p.Annotation<FileInfo>()?.Name} {rsRule.Position}");
                                    }
                                }

                                // update the indentation to match that of the InsertRule that it will replace.
                                var lines = content.Split('\n').Select(t => t.TrimEnd()).Where(l => !string.IsNullOrEmpty(l));
                                content = String.Join(rsRule.Indent, lines);
                                Console.WriteLine("----");
                                Console.WriteLine(content);
                                Console.WriteLine("----");

                                // parse this content to put into the resource
                                var fakeProfile = $"Profile: FakeProfile\r\nTitle: \"Fake Title\"\r\n{content}";
                                var resultBit = FshParser.Parse(fakeProfile);
                                if (resultBit is ParseResult.Success successBit)
                                {
                                    var fakeRules = successBit.Document.Entities.OfType<Profile>().First().Rules;
                                    foreach (var fr in fakeRules)
                                    {
                                        Console.WriteLine($"      Injecting Parsed Rule: {fr.ToString()}");
                                        fr.SetAnnotation(ruleSet.Annotation<FileInfo>());
                                        fr.SetAnnotation(rsRule); // the rule we came from (since we'll be removing it from the collection)
                                    }
                                    p.Rules.InsertRange(p.Rules.IndexOf(rule), fakeRules);
                                    p.Rules.Remove(rule);
                                }
                                else if (resultBit is ParseResult.Failure failureBit)
                                {
                                    Console.WriteLine($"    Error parsing injected RuleSet content for {ruleSet.Name}:");
                                    foreach (var err in failureBit.Errors)
                                    {
                                        Console.WriteLine($"      {err.Message} at {err.Location}");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine($"    Error: RuleSet '{rsRule.RuleSetReference}' not found.");
                            }
                        }
                    }
                }
                Console.WriteLine();
            }
        }
    }

    #region Helper Methods

    private static string NormalizeWhitespace(string text)
    {
        // Normalize line endings
        text = text.Replace("\r", "");

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

        return string.Join("\r\n", lines).Trim();
    }

    #endregion
}
