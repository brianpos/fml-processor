using Microsoft.VisualStudio.TestTools.UnitTesting;
using fsh_processor;
using fsh_processor.Models;

namespace fsh_tester;

[TestClass]
public class AddElementRuleTest
{
    [TestMethod]
    public void TestAddElementRuleParsing()
    {
        // Test that AddElementRule parses correctly with all components
        var fsh = @"
Logical: TestLogical
Parent: Element
Id: test-logical
Title: ""Test Logical Model""
Description: ""This is a test logical model""
* element1 0..1 string ""Short description"" ""Long definition text""
* element2 1..* CodeableConcept ""Another element""
* element3 0..1 Quantity or Range ""Element with multiple types""
";
        
        var result = FshParser.Parse(fsh);
        Assert.IsInstanceOfType<ParseResult.Success>(result, "Parse should succeed");
        
        var doc = ((ParseResult.Success)result).Document;
        Assert.AreEqual(1, doc.Entities.Count, "Should have one entity");
        
        var logical = doc.Entities[0] as Logical;
        Assert.IsNotNull(logical, "Entity should be a Logical");
        Assert.AreEqual("TestLogical", logical.Name);
        Assert.AreEqual("Element", logical.Parent);
        Assert.AreEqual("test-logical", logical.Id);
        Assert.AreEqual("Test Logical Model", logical.Title);
        Assert.AreEqual("This is a test logical model", logical.Description);
        
        // Check that we have 3 AddElementRules
        Assert.AreEqual(3, logical.Rules.Count, "Should have 3 rules");
        var addElementRules = logical.Rules.OfType<AddElementRule>().ToList();
        Assert.AreEqual(3, addElementRules.Count, "All rules should be AddElementRules");
        
        // Verify first rule
        var rule1 = addElementRules[0];
        Assert.AreEqual("element1", rule1.Path);
        Assert.AreEqual("0..1", rule1.Cardinality);
        Assert.AreEqual(1, rule1.TargetTypes.Count);
        Assert.AreEqual("string", rule1.TargetTypes[0]);
        Assert.AreEqual("Short description", rule1.ShortDescription);
        Assert.AreEqual("Long definition text", rule1.Definition);
        
        // Verify second rule
        var rule2 = addElementRules[1];
        Assert.AreEqual("element2", rule2.Path);
        Assert.AreEqual("1..*", rule2.Cardinality);
        Assert.AreEqual(1, rule2.TargetTypes.Count);
        Assert.AreEqual("CodeableConcept", rule2.TargetTypes[0]);
        Assert.AreEqual("Another element", rule2.ShortDescription);
        Assert.IsNull(rule2.Definition);
        
        // Verify third rule (multiple types)
        var rule3 = addElementRules[2];
        Assert.AreEqual("element3", rule3.Path);
        Assert.AreEqual("0..1", rule3.Cardinality);
        Assert.AreEqual(2, rule3.TargetTypes.Count);
        Assert.AreEqual("Quantity", rule3.TargetTypes[0]);
        Assert.AreEqual("Range", rule3.TargetTypes[1]);
        Assert.AreEqual("Element with multiple types", rule3.ShortDescription);
        Assert.IsNull(rule3.Definition);
    }
}
