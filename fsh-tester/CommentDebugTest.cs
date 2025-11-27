using Microsoft.VisualStudio.TestTools.UnitTesting;
using fsh_processor;
using fsh_processor.Models;
using System;
using System.Linq;

namespace fsh_tester;

[TestClass]
public class CommentDebugTest
{
    [TestMethod]
    public void TestInlineCommentOnRule()
    {
        // This test documents the known limitation: inline comments after rules (on the same line)
        // are absorbed into the STAR token and are not preserved separately.
        // This is due to the STAR pattern: ([\r\n] | LINE_COMMENT) WS* '*' [ \u00A0]
        // which matches LINE_COMMENT before the *, not after it.
        
        var fshText = @"Profile: MyProfile
Parent: Patient

* name 1..1 MS // name is required
* identifier 0..* MS
";

        var result = FshParser.Parse(fshText);
        Assert.IsInstanceOfType<ParseResult.Success>(result, "Parse should succeed");
        
        var doc = ((ParseResult.Success)result).Document;
        var profile = doc.Entities[0] as Profile;
        Assert.IsNotNull(profile);
        
        Console.WriteLine($"Profile has {profile.Rules.Count} rules");
        
        var nameRule = profile.Rules[0];
        Console.WriteLine($"Rule 0: {nameRule.GetType().Name}");
        Console.WriteLine($"  Leading tokens: {nameRule.LeadingHiddenTokens?.Count ?? 0}");
        Console.WriteLine($"  Trailing tokens: {nameRule.TrailingHiddenTokens?.Count ?? 0}");
        
        if (nameRule.TrailingHiddenTokens != null)
        {
            for (int i = 0; i < nameRule.TrailingHiddenTokens.Count; i++)
            {
                var token = nameRule.TrailingHiddenTokens[i];
                Console.WriteLine($"  Trailing token {i}: Type={token.TokenType}, Text='{token.Text.Replace("\n", "\\n").Replace("\r", "\\r")}'");
            }
        }
        
        // Check second rule too
        var identifierRule = profile.Rules[1];
        Console.WriteLine($"Rule 1: {identifierRule.GetType().Name}");
        Console.WriteLine($"  Leading tokens: {identifierRule.LeadingHiddenTokens?.Count ?? 0}");
        Console.WriteLine($"  Trailing tokens: {identifierRule.TrailingHiddenTokens?.Count ?? 0}");
        
        if (identifierRule.LeadingHiddenTokens != null)
        {
            for (int i = 0; i < identifierRule.LeadingHiddenTokens.Count; i++)
            {
                var token = identifierRule.LeadingHiddenTokens[i];
                Console.WriteLine($"  Leading token {i}: Type={token.TokenType}, Text='{token.Text.Replace("\n", "\\n").Replace("\r", "\\r")}'");
            }
        }
        
        // Serialize
        var serialized = FshSerializer.Serialize(doc);
        Console.WriteLine("=== SERIALIZED ===");
        Console.WriteLine(serialized);
        Console.WriteLine("==================");
        
        Assert.IsTrue(serialized.Contains("// name is required"), "Inline comments after rules are preserved");
    }
}
