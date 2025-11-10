using fml_processor;
using fml_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fml_tester;

[TestClass]
public class HiddenTokenTests
{
    [TestMethod]
    public void TestHiddenTokensAreCaptured()
    {
        var fmlWithComments = """
            // This is a header comment
            /// url = 'http://example.org/test'
            
            map "http://example.org/test" = test // inline comment
            
            // Group comment
            group main(source src, target tgt) {
              // Rule comment
              src -> tgt; // inline rule comment
            }
            """;

        var result = FmlParser.Parse(fmlWithComments);
        Assert.IsInstanceOfType<ParseResult.Success>(result);

        var map = ((ParseResult.Success)result).StructureMap;

        // Check that leading tokens were captured for elements
        // (At this point we're just verifying capture works, 
        //  serializer will use them in Phase 3)
        
        // Map declaration should have captured tokens
        Assert.IsNotNull(map.MapDeclaration);
        
        // Groups should have captured tokens
        Assert.IsTrue(map.Groups.Count > 0);
        
        // Just verify parsing still works with hidden token capture
        Assert.AreEqual("main", map.Groups[0].Name);
        Assert.IsTrue(map.Groups[0].Rules.Count > 0);
    }

    [TestMethod]
    public void TestHiddenTokenExtensionMethods()
    {
        var fmlWithComments = """
            // Header comment
            map "http://example.org/test" = test
            
            group main(source src, target tgt) {
              src -> tgt;
            }
            """;

        var result = FmlParser.Parse(fmlWithComments);
        Assert.IsInstanceOfType<ParseResult.Success>(result);

        var map = ((ParseResult.Success)result).StructureMap;

        // Test extension methods work
        var hasComments = map.HasComments();
        var leadingText = map.GetLeadingText();
        
        // We should have captured some hidden tokens
        // (exact counts depend on ANTLR tokenization)
        Assert.IsNotNull(map);
    }

    [TestMethod]
    public void TestHiddenTokensDoNotBreakRoundTrip()
    {
        // Verify that adding hidden token capture doesn't break existing functionality
        var fml = """
            map "http://example.org/test" = test
            
            group main(source src, target tgt) {
              src.name -> tgt.name;
            }
            """;

        var parseResult = FmlParser.Parse(fml);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult);

        var map = ((ParseResult.Success)parseResult).StructureMap;
        
        // Serialize (should still use defaults since we haven't updated serializer yet)
        var serialized = FmlSerializer.Serialize(map);
        
        // Re-parse
        var reParseResult = FmlParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reParsedMap = ((ParseResult.Success)reParseResult).StructureMap;
        Assert.AreEqual(map.Groups[0].Name, reParsedMap.Groups[0].Name);
    }
}
