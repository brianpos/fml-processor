using fml_processor;
using fml_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace fml_tester;

/// <summary>
/// Tests for modifying the FML object model and serializing back to text.
/// These tests verify the core use case: reading FML, adding/modifying rules for extension processing,
/// and writing the updated FML back out.
/// </summary>
[TestClass]
public class ModificationTests
{
    [TestMethod]
    public void TestAddComplexExtensionProcessingRule()
    {
        // This test verifies the core use case: adding a complex rule for extension processing
        // to an existing FML structure map, then serializing it back to valid FML.
        
        var originalFml = """
            map "http://example.org/fhir/StructureMap/test" = test
            
            uses "http://hl7.org/fhir/StructureDefinition/ActivityDefinition" as source
            uses "http://hl7.org/fhir/StructureDefinition/ActivityDefinition" as target
            
            group ActivityDefinitionParticipant(source src, target tgt) {
              src.role -> tgt.role;
            }
            """;

        // Parse the original FML
        var parseResult = FmlParser.Parse(originalFml);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult, "Original FML should parse successfully");
        
        var structureMap = ((ParseResult.Success)parseResult).StructureMap;
        
        // Find the group we want to modify
        var group = structureMap.Groups.FirstOrDefault(g => g.Name == "ActivityDefinitionParticipant");
        Assert.IsNotNull(group, "Group should exist");
        
        // Create the complex extension processing rule with all three comment blocks
        var extensionRule = new Rule
        {
            // Add the leading comment: "The extension processing..."
            LeadingHiddenTokens = new List<HiddenToken>
            {
                new HiddenToken
                {
                    TokenType = FmlMappingLexer.WS,
                    Text = "  "
                },
                new HiddenToken
                {
                    TokenType = FmlMappingLexer.LINE_COMMENT,
                    Text = "// The extension processing (including processing backport URLs)"
                },
                new HiddenToken
                {
                    TokenType = FmlMappingLexer.WS,
                    Text = "\r\n"
                }
            },
            
            // Source: just "src"
            Sources = new List<RuleSource>
            {
                new RuleSource { Context = "src" }
            },
            
            // Multiple targets for the complex rule
            Targets = new List<RuleTarget>
            {
                // Target 1: ('http://hl7.org/fhir/5.0/StructureDefinition/ActivityDefinition#ActivityDefinition.participant.') as bpUrl
                // This is an expression target - serializer will add parentheses
                new RuleTarget
                {
                    Context = string.Empty,
                    Element = null,
                    Transform = new Transform
                    {
                        Type = TransformType.Evaluate,
                        Parameters = new List<TransformParameter>
                        {
                            new TransformParameter
                            {
                                Type = TransformParameterType.Expression,
                                Value = "'http://hl7.org/fhir/5.0/StructureDefinition/ActivityDefinition#ActivityDefinition.participant.'"
                            }
                        }
                    },
                    Variable = "bpUrl",
                    TrailingHiddenTokens = new List<HiddenToken>
                    {
                        new HiddenToken
                        {
                            TokenType = FmlMappingLexer.WS,
                            Text = "\r\n   \r\n   "
                        }
                    }
                },
                
                // Target 2: tgt.typeCanonical = (src.extension(bpUrl & 'typeCanonical').value)
                // Add comment before the backport extensions block
                new RuleTarget
                {
                    Context = "tgt",
                    Element = "typeCanonical",
                    LeadingHiddenTokens = new List<HiddenToken>
                    {
                        new HiddenToken
                        {
                            TokenType = FmlMappingLexer.LINE_COMMENT,
                            Text = "// Backport extensions"
                        },
                        new HiddenToken
                        {
                            TokenType = FmlMappingLexer.WS,
                            Text = "\r\n   "
                        }
                    },
                    Transform = new Transform
                    {
                        Type = TransformType.Evaluate,
                        Parameters = new List<TransformParameter>
                        {
                            new TransformParameter
                            {
                                Type = TransformParameterType.Expression,
                                Value = "src.extension(bpUrl & 'typeCanonical').value"
                            }
                        }
                    }
                },
                
                // Target 3: tgt.typeReference = (src.extension(bpUrl & 'typeReference').value)
                new RuleTarget
                {
                    Context = "tgt",
                    Element = "typeReference",
                    Transform = new Transform
                    {
                        Type = TransformType.Evaluate,
                        Parameters = new List<TransformParameter>
                        {
                            new TransformParameter
                            {
                                Type = TransformParameterType.Expression,
                                Value = "src.extension(bpUrl & 'typeReference').value"
                            }
                        }
                    },
                    TrailingHiddenTokens = new List<HiddenToken>
                    {
                        new HiddenToken
                        {
                            TokenType = FmlMappingLexer.WS,
                            Text = "\r\n    \r\n   "
                        }
                    }
                }
            },
            
            // Dependent: then BackboneElementXver(src, tgt, bpUrl)
            // Add comment before the "then" clause
            Dependent = new RuleDependent
            {
                LeadingHiddenTokens = new List<HiddenToken>
                {
                    new HiddenToken
                    {
                        TokenType = FmlMappingLexer.LINE_COMMENT,
                        Text = "// Regular extensions (and base)"
                    },
                    new HiddenToken
                    {
                        TokenType = FmlMappingLexer.WS,
                        Text = "\r\n   "
                    }
                },
                Invocations = new List<GroupInvocation>
                {
                    new GroupInvocation
                    {
                        Name = "BackboneElementXver",
                        Parameters = new List<InvocationParameter>
                        {
                            new InvocationParameter { Type = InvocationParameterType.Identifier, Value = "src" },
                            new InvocationParameter { Type = InvocationParameterType.Identifier, Value = "tgt" },
                            new InvocationParameter { Type = InvocationParameterType.Identifier, Value = "bpUrl" }
                        }
                    }
                }
            },
            
            // Rule name
            Name = "Extensions"
        };
        
        // Add the rule to the group
        group.Rules.Add(extensionRule);
        
        // Serialize the modified structure map
        var serialized = FmlSerializer.Serialize(structureMap);
        
        // Verify the serialized output contains key elements
        Assert.IsTrue(serialized.Contains("// The extension processing"), 
            "Serialized output should contain the comment");
        Assert.IsTrue(serialized.Contains("as bpUrl"), 
            "Serialized output should contain the bpUrl variable");
        Assert.IsTrue(serialized.Contains("tgt.typeCanonical"), 
            "Serialized output should contain typeCanonical target");
        Assert.IsTrue(serialized.Contains("tgt.typeReference"), 
            "Serialized output should contain typeReference target");
        Assert.IsTrue(serialized.Contains("then BackboneElementXver"), 
            "Serialized output should contain the dependent invocation");
        Assert.IsTrue(serialized.Contains("\"Extensions\""), 
            "Serialized output should contain the rule name");
        
        // Verify the modified FML can be parsed back
        var reParseResult = FmlParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult,
            $"Modified FML should parse successfully. Serialized output:\n{serialized}");
        
        var reParsedMap = ((ParseResult.Success)reParseResult).StructureMap;
        
        // Verify the structure is preserved
        var reParsedGroup = reParsedMap.Groups.FirstOrDefault(g => g.Name == "ActivityDefinitionParticipant");
        Assert.IsNotNull(reParsedGroup, "Group should exist after round-trip");
        Assert.AreEqual(2, reParsedGroup.Rules.Count, "Should have 2 rules (original + added)");
        
        // Verify the added rule
        var addedRule = reParsedGroup.Rules[1];
        Assert.AreEqual("Extensions", addedRule.Name, "Rule name should be preserved");
        Assert.AreEqual(1, addedRule.Sources.Count, "Should have 1 source");
        Assert.AreEqual(3, addedRule.Targets.Count, "Should have 3 targets");
        Assert.IsNotNull(addedRule.Dependent, "Should have dependent");
        Assert.AreEqual(1, addedRule.Dependent.Invocations.Count, "Should have 1 invocation");
        Assert.AreEqual("BackboneElementXver", addedRule.Dependent.Invocations[0].Name, 
            "Invocation name should be preserved");
        
        // Print the serialized output for inspection
        Console.WriteLine("=== MODIFIED FML OUTPUT ===");
        Console.WriteLine(serialized);
    }
    
    [TestMethod]
    public void TestModifyExistingRule()
    {
        // Test modifying an existing rule by adding a where clause
        var originalFml = """
            map "http://example.org/test" = test
            
            group main(source src, target tgt) {
              src.name -> tgt.name;
            }
            """;
        
        var parseResult = FmlParser.Parse(originalFml);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult);
        
        var structureMap = ((ParseResult.Success)parseResult).StructureMap;
        var rule = structureMap.Groups[0].Rules[0];
        
        // Modify the rule to add a where clause
        rule.Sources[0].Condition = "use = 'official'";
        
        // Serialize and verify
        var serialized = FmlSerializer.Serialize(structureMap);
        Assert.IsTrue(serialized.Contains("where (use = 'official')"),
            "Modified rule should contain where clause");
        
        // Verify round-trip
        var reParseResult = FmlParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reParsedMap = ((ParseResult.Success)reParseResult).StructureMap;
        var reParsedRule = reParsedMap.Groups[0].Rules[0];
        Assert.AreEqual("use = 'official'", reParsedRule.Sources[0].Condition,
            "Where clause should be preserved");
    }
    
    [TestMethod]
    public void TestAddRuleWithComments()
    {
        // Test adding a rule with inline and block comments
        var originalFml = """
            map "http://example.org/test" = test
            
            group main(source src, target tgt) {
              src.id -> tgt.id;
            }
            """;
        
        var parseResult = FmlParser.Parse(originalFml);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult);
        
        var structureMap = ((ParseResult.Success)parseResult).StructureMap;
        var group = structureMap.Groups[0];
        
        // Add a new rule with comments
        var newRule = new Rule
        {
            LeadingHiddenTokens = new List<HiddenToken>
            {
                new HiddenToken
                {
                    TokenType = FmlMappingLexer.WS,
                    Text = "\r\n  "
                },
                new HiddenToken
                {
                    TokenType = FmlMappingLexer.COMMENT,
                    Text = "/* This is a block comment\r\n   * explaining the new rule\r\n   */"
                },
                new HiddenToken
                {
                    TokenType = FmlMappingLexer.WS,
                    Text = "\r\n"
                }
            },
            Sources = new List<RuleSource>
            {
                new RuleSource { Context = "src", Element = "name", Variable = "n" }
            },
            Targets = new List<RuleTarget>
            {
                new RuleTarget { Context = "tgt", Element = "name", Variable = "tn" }
            },
            TrailingHiddenTokens = new List<HiddenToken>
            {
                new HiddenToken
                {
                    TokenType = FmlMappingLexer.WS,
                    Text = " "
                },
                new HiddenToken
                {
                    TokenType = FmlMappingLexer.LINE_COMMENT,
                    Text = "// Copy name to target"
                }
            }
        };
        
        group.Rules.Add(newRule);
        
        // Serialize
        var serialized = FmlSerializer.Serialize(structureMap);
        
        // Verify comments are in output
        Assert.IsTrue(serialized.Contains("/* This is a block comment"),
            "Block comment should be in output");
        Assert.IsTrue(serialized.Contains("// Copy name to target"),
            "Inline comment should be in output");
        
        // Verify round-trip
        var reParseResult = FmlParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reParsedMap = ((ParseResult.Success)reParseResult).StructureMap;
        Assert.AreEqual(2, reParsedMap.Groups[0].Rules.Count,
            "Should have 2 rules after round-trip");
        
        // Verify comments are preserved in round-trip
        // Note: Block comments (leading tokens) are preserved
        // Trailing inline comments may become leading tokens for the next element or EOF
        var reSerialized = FmlSerializer.Serialize(reParsedMap);
        Assert.IsTrue(reSerialized.Contains("/* This is a block comment"),
            "Block comment should survive round-trip");
        // Trailing comment behavior: it may appear as trailing or move to next line/EOF
        // This is expected ANTLR tokenization behavior
    }
    
    [TestMethod]
    public void TestInsertRuleAtSpecificPosition()
    {
        // Test inserting a rule at a specific position in the rules list
        var originalFml = """
            map "http://example.org/test" = test
            
            group main(source src, target tgt) {
              src.id -> tgt.id "first";
              src.status -> tgt.status "third";
            }
            """;
        
        var parseResult = FmlParser.Parse(originalFml);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult);
        
        var structureMap = ((ParseResult.Success)parseResult).StructureMap;
        var group = structureMap.Groups[0];
        
        // Create a rule to insert in the middle
        var middleRule = new Rule
        {
            Sources = new List<RuleSource>
            {
                new RuleSource { Context = "src", Element = "name", Variable = "n" }
            },
            Targets = new List<RuleTarget>
            {
                new RuleTarget { Context = "tgt", Element = "name", Variable = "tn" }
            },
            Name = "second"
        };
        
        // Insert at position 1 (between first and third)
        group.Rules.Insert(1, middleRule);
        
        // Serialize
        var serialized = FmlSerializer.Serialize(structureMap);
        
        // Verify the order
        var firstIndex = serialized.IndexOf("\"first\"");
        var secondIndex = serialized.IndexOf("\"second\"");
        var thirdIndex = serialized.IndexOf("\"third\"");
        
        Assert.IsTrue(firstIndex > 0 && secondIndex > 0 && thirdIndex > 0,
            "All three rules should be in output");
        Assert.IsTrue(firstIndex < secondIndex && secondIndex < thirdIndex,
            "Rules should be in correct order: first, second, third");
        
        // Verify round-trip
        var reParseResult = FmlParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reParsedMap = ((ParseResult.Success)reParseResult).StructureMap;
        Assert.AreEqual(3, reParsedMap.Groups[0].Rules.Count,
            "Should have 3 rules");
        Assert.AreEqual("first", reParsedMap.Groups[0].Rules[0].Name);
        Assert.AreEqual("second", reParsedMap.Groups[0].Rules[1].Name);
        Assert.AreEqual("third", reParsedMap.Groups[0].Rules[2].Name);
    }
    
    [TestMethod]
    public void TestRemoveAndReplaceRule()
    {
        // Test removing a rule and replacing it with a modified version
        var originalFml = """
            map "http://example.org/test" = test
            
            group main(source src, target tgt) {
              src.id -> tgt.id;
              src.name -> tgt.name;
              src.status -> tgt.status;
            }
            """;
        
        var parseResult = FmlParser.Parse(originalFml);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult);
        
        var structureMap = ((ParseResult.Success)parseResult).StructureMap;
        var group = structureMap.Groups[0];
        
        // Find and remove the "name" rule
        var nameRuleIndex = group.Rules.FindIndex(r => 
            r.Sources.Any(s => s.Element == "name"));
        Assert.IsTrue(nameRuleIndex >= 0, "Should find name rule");
        
        // Remove it
        group.Rules.RemoveAt(nameRuleIndex);
        
        // Add a more complex version
        var enhancedNameRule = new Rule
        {
            Sources = new List<RuleSource>
            {
                new RuleSource 
                { 
                    Context = "src", 
                    Element = "name", 
                    Variable = "n",
                    Condition = "use = 'official'"
                }
            },
            Targets = new List<RuleTarget>
            {
                new RuleTarget 
                { 
                    Context = "tgt", 
                    Element = "name",
                    Transform = new Transform
                    {
                        Type = TransformType.Copy,
                        Parameters = new List<TransformParameter>
                        {
                            new TransformParameter
                            {
                                Type = TransformParameterType.Identifier,
                                Value = "n"
                            }
                        }
                    }
                }
            },
            Name = "official-name"
        };
        
        // Insert at the same position
        group.Rules.Insert(nameRuleIndex, enhancedNameRule);
        
        // Serialize
        var serialized = FmlSerializer.Serialize(structureMap);
        
        // Verify the enhanced rule is present
        Assert.IsTrue(serialized.Contains("where (use = 'official')"),
            "Enhanced rule should have where clause");
        Assert.IsTrue(serialized.Contains("\"official-name\""),
            "Enhanced rule should have name");
        
        // Verify still have 3 rules
        var reParseResult = FmlParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult);
        
        var reParsedMap = ((ParseResult.Success)reParseResult).StructureMap;
        Assert.AreEqual(3, reParsedMap.Groups[0].Rules.Count,
            "Should still have 3 rules");
    }
    
    [TestMethod]
    public void TestAddMultipleTargetsToExistingRule()
    {
        // Test modifying a rule to add multiple targets (like the extension processing pattern)
        var originalFml = """
            map "http://example.org/test" = test
            
            group main(source src, target tgt) {
              src -> tgt;
            }
            """;
        
        var parseResult = FmlParser.Parse(originalFml);
        Assert.IsInstanceOfType<ParseResult.Success>(parseResult);
        
        var structureMap = ((ParseResult.Success)parseResult).StructureMap;
        var rule = structureMap.Groups[0].Rules[0];
        
        // Clear existing targets and add multiple new ones
        rule.Targets.Clear();
        
        // Add variable target - expression type so it gets parentheses
        rule.Targets.Add(new RuleTarget
        {
            Context = string.Empty,
            Transform = new Transform
            {
                Type = TransformType.Evaluate,
                Parameters = new List<TransformParameter>
                {
                    new TransformParameter
                    {
                        Type = TransformParameterType.Expression,
                        Value = "'http://example.org/extension'"
                    }
                }
            },
            Variable = "extUrl"
        });
        
        // Add first extension target
        rule.Targets.Add(new RuleTarget
        {
            Context = "tgt",
            Element = "property1",
            Transform = new Transform
            {
                Type = TransformType.Evaluate,
                Parameters = new List<TransformParameter>
                {
                    new TransformParameter
                    {
                        Type = TransformParameterType.Expression,
                        Value = "src.extension(extUrl & 'property1').value"
                    }
                }
            }
        });
        
        // Add second extension target
        rule.Targets.Add(new RuleTarget
        {
            Context = "tgt",
            Element = "property2",
            Transform = new Transform
            {
                Type = TransformType.Evaluate,
                Parameters = new List<TransformParameter>
                {
                    new TransformParameter
                    {
                        Type = TransformParameterType.Expression,
                        Value = "src.extension(extUrl & 'property2').value"
                    }
                }
            }
        });
        
        // Serialize
        var serialized = FmlSerializer.Serialize(structureMap);
        
        // Verify all targets are present
        Assert.IsTrue(serialized.Contains("as extUrl"), "Should have extUrl variable");
        Assert.IsTrue(serialized.Contains("tgt.property1"), "Should have property1");
        Assert.IsTrue(serialized.Contains("tgt.property2"), "Should have property2");
        Assert.IsTrue(serialized.Contains("src.extension(extUrl"), 
            "Should have extension expression");
        
        // Verify round-trip
        var reParseResult = FmlParser.Parse(serialized);
        Assert.IsInstanceOfType<ParseResult.Success>(reParseResult,
            $"Modified rule should parse. Output:\n{serialized}");
        
        var reParsedMap = ((ParseResult.Success)reParseResult).StructureMap;
        var reParsedRule = reParsedMap.Groups[0].Rules[0];
        Assert.AreEqual(3, reParsedRule.Targets.Count, 
            "Should have 3 targets after round-trip");
    }
}
