using Microsoft.VisualStudio.TestTools.UnitTesting;
using fsh_processor;
using fsh_processor.Models;

namespace fsh_tester;

[TestClass]
public class MultilineStringTest
{
    [TestMethod]
    public void TestMultilineStringParsing()
    {
        // Test that triple-quoted strings have their delimiters properly removed
        var fsh = @"
Profile: TestProfile
Description: """"""This is a
multiline
description""""""
";
        
        var result = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<ParseResult.Success>(result, "Parse should succeed");
        
        var doc = ((ParseResult.Success)result).Document;
        Assert.AreEqual(1, doc.Entities.Count, "Should have one entity");
        
        var profile = doc.Entities[0] as Profile;
        Assert.IsNotNull(profile, "Entity should be a Profile");
        
        // The description should NOT contain the triple quotes
        Assert.IsNotNull(profile.Description, "Description should not be null");
        Assert.IsFalse(profile.Description.StartsWith("\"\"\""), "Description should not start with triple quotes");
        Assert.IsFalse(profile.Description.EndsWith("\"\"\""), "Description should not end with triple quotes");
        
        // Normalize line endings for comparison (Windows uses \r\n)
        var normalizedDescription = profile.Description.Replace("\r\n", "\n");
        Assert.AreEqual("This is a\nmultiline\ndescription", normalizedDescription, "Description should be extracted correctly");
    }

    [TestMethod]
    public void TestSingleQuoteStringParsing()
    {
        // Test that single-quoted strings still work correctly
        var fsh = @"
Profile: TestProfile
Description: ""This is a single line description""
";
        
        var result = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<ParseResult.Success>(result, "Parse should succeed");
        
        var doc = ((ParseResult.Success)result).Document;
        Assert.AreEqual(1, doc.Entities.Count, "Should have one entity");
        
        var profile = doc.Entities[0] as Profile;
        Assert.IsNotNull(profile, "Entity should be a Profile");
        
        // The description should NOT contain the quotes
        Assert.IsNotNull(profile.Description, "Description should not be null");
        Assert.IsFalse(profile.Description.StartsWith("\""), "Description should not start with quote");
        Assert.IsFalse(profile.Description.EndsWith("\""), "Description should not end with quote");
        Assert.AreEqual("This is a single line description", profile.Description, "Description should be extracted correctly");
    }

    [TestMethod]
    public void TestRoundTripMultilineString()
    {
        // Test that multiline strings can be parsed, serialized, and re-parsed
        var fsh = @"
Profile: TestProfile
Description: """"""This is a
multiline
description""""""
";
        
        // First parse
        var parseResult = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult, "Initial parse should succeed");
        var doc = ((ParseResult.Success)parseResult).Document;
        
        // Serialize
        var serialized = FshSerializer.Serialize(doc);
        
        // Re-parse
        var reParseResult = FshParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult, "Re-parse should succeed");
        
        var reDoc = ((ParseResult.Success)reParseResult).Document;
        var originalProfile = doc.Entities[0] as Profile;
        var reParsedProfile = reDoc.Entities[0] as Profile;
        
        Assert.IsNotNull(originalProfile, "Original should be a Profile");
        Assert.IsNotNull(reParsedProfile, "Re-parsed should be a Profile");
        Assert.AreEqual(originalProfile.Description, reParsedProfile.Description, "Descriptions should match after round-trip");
    }
}
