using fml_processor;
using fml_processor.Models;

namespace fml_tester
{
    [TestClass]
    public sealed class ParserTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            // Example FML to parse
            var fmlText = """
                map "http://example.org/fhir/StructureMap/test" = test
                
                uses "http://hl7.org/fhir/StructureDefinition/Patient" as source
                uses "http://hl7.org/fhir/StructureDefinition/Bundle" as target
                
                group test(source src : Patient, target bundle : Bundle) {
                  src.name as vName -> bundle.entry.name = vName;
                }
                """;

            // Use the FmlParser to scan the resource into a new object model
            var result = FmlParser.Parse(fmlText);

            // Verify successful parse
            Assert.IsInstanceOfType<ParseResult.Success>(result);
            
            var success = (ParseResult.Success)result;
            var structureMap = success.StructureMap;

            // Perform some in-memory manipulations
            Assert.IsNotNull(structureMap);
            Assert.IsNotNull(structureMap.MapDeclaration);
            Assert.AreEqual("http://example.org/fhir/StructureMap/test", structureMap.MapDeclaration.Url);
            Assert.AreEqual("test", structureMap.MapDeclaration.Identifier);
            
            // Verify structures
            Assert.AreEqual(2, structureMap.Structures.Count);
            Assert.AreEqual(StructureMode.Source, structureMap.Structures[0].Mode);
            Assert.AreEqual(StructureMode.Target, structureMap.Structures[1].Mode);
            
            // Verify groups
            Assert.AreEqual(1, structureMap.Groups.Count);
            var group = structureMap.Groups[0];
            Assert.AreEqual("test", group.Name);
            Assert.AreEqual(2, group.Parameters.Count);
            
            // Verify parameters
            Assert.AreEqual(ParameterMode.Source, group.Parameters[0].Mode);
            Assert.AreEqual("src", group.Parameters[0].Name);
            Assert.AreEqual("Patient", group.Parameters[0].Type);
            
            Assert.AreEqual(ParameterMode.Target, group.Parameters[1].Mode);
            Assert.AreEqual("bundle", group.Parameters[1].Name);
            Assert.AreEqual("Bundle", group.Parameters[1].Type);
            
            // Verify rules
            Assert.AreEqual(1, group.Rules.Count);
            var rule = group.Rules[0];
            
            // Verify sources
            Assert.AreEqual(1, rule.Sources.Count);
            var source = rule.Sources[0];
            Assert.AreEqual("src", source.Context);
            Assert.AreEqual("name", source.Element);
            Assert.AreEqual("vName", source.Variable);
            
            // Verify position tracking works
            Assert.IsNotNull(source.Position);
            Assert.IsTrue(source.Position.StartLine > 0);
            
            // Verify targets
            Assert.AreEqual(1, rule.Targets.Count);
            var target = rule.Targets[0];
            Assert.AreEqual("bundle", target.Context);
            Assert.AreEqual("entry.name", target.Element);
            
            // Verify transform
            Assert.IsNotNull(target.Transform);
            Assert.AreEqual(TransformType.Copy, target.Transform.Type);
            Assert.AreEqual(1, target.Transform.Parameters.Count);
            
            // Example manipulation: Add metadata
            structureMap.Metadata.Add(new MetadataDeclaration
            {
                Path = "version",
                Value = "1.0.0",
                IsMarkdown = false
            });
            
            Assert.AreEqual(1, structureMap.Metadata.Count);
            
            // TODO: Write it back out to a new resource
            // This would require implementing a serializer/writer
            // For now, we've successfully parsed and manipulated the model
        }

        [TestMethod]
        public void TestInvalidFml()
        {
            var invalidFml = "map 'invalid";

            var result = FmlParser.Parse(invalidFml);

            // Should fail to parse
            Assert.IsInstanceOfType<ParseResult.Failure>(result);
            
            var failure = (ParseResult.Failure)result;
            Assert.IsTrue(failure.Errors.Count > 0);
            
            // First error should be a syntax error
            var error = failure.Errors[0];
            Assert.AreEqual(ErrorSeverity.Error, error.Severity);
            Assert.AreEqual("syntax", error.Code);
        }

        [TestMethod]
        public void TestParseOrThrow_Success()
        {
            var fmlText = """
                map "http://example.org/test" = test
                group test(source src, target tgt) {
                  src -> tgt;
                }
                """;

            var structureMap = FmlParser.ParseOrThrow(fmlText);

            Assert.IsNotNull(structureMap);
            Assert.AreEqual(1, structureMap.Groups.Count);
        }

        [TestMethod]
        public void TestParseOrThrow_Failure()
        {
            var invalidFml = "this is not valid FML";

            // Should throw FmlParseException
            FmlParseException? exception = null;
            
            try
            {
                FmlParser.ParseOrThrow(invalidFml);
                Assert.Fail("Expected FmlParseException to be thrown");
            }
            catch (FmlParseException ex)
            {
                exception = ex;
            }

            // Verify the exception has error details
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception.Errors.Count > 0);
            Assert.AreEqual(ErrorSeverity.Error, exception.Errors[0].Severity);
        }

        [TestMethod]
        public void TestFmlWithComments()
        {
            // FML with various types of comments
            var fmlWithComments = """
                // This is a single-line comment at the start
                map "http://example.org/fhir/StructureMap/commented" = commented
                
                // Comment before uses statements
                uses "http://hl7.org/fhir/StructureDefinition/Patient" as source // Inline comment
                uses "http://hl7.org/fhir/StructureDefinition/Bundle" as target
                
                /* This is a
                   multi-line comment
                   before the group */
                group tutorial(source src : Patient, target bundle : Bundle) {
                  // Comment inside the group
                  src.name as vName -> bundle.entry.name = vName; // Inline comment on rule
                  
                  /* Multi-line comment
                     inside the group */
                  src.gender as vGender -> bundle.entry.gender = vGender;
                  
                  // Comment before nested rule
                  src.telecom as vTelecom -> bundle.entry.telecom as vEntry then {
                    // Nested comment
                    vTelecom.value -> vEntry.value; // End-of-line comment
                  }; // Comment after closing brace
                }
                // Comment at the end of file
                """;

            var result = FmlParser.Parse(fmlWithComments);

            // Verify successful parse despite comments
            Assert.IsInstanceOfType<ParseResult.Success>(result, 
                "FML with comments should parse successfully");
            
            var success = (ParseResult.Success)result;
            var structureMap = success.StructureMap;

            // Verify the structure was parsed correctly
            Assert.IsNotNull(structureMap);
            Assert.IsNotNull(structureMap.MapDeclaration);
            Assert.AreEqual("http://example.org/fhir/StructureMap/commented", 
                structureMap.MapDeclaration.Url);
            Assert.AreEqual("commented", structureMap.MapDeclaration.Identifier);
            
            // Verify structures (comments should not affect parsing)
            Assert.AreEqual(2, structureMap.Structures.Count);
            Assert.AreEqual(StructureMode.Source, structureMap.Structures[0].Mode);
            Assert.AreEqual(StructureMode.Target, structureMap.Structures[1].Mode);
            
            // Verify group was parsed
            Assert.AreEqual(1, structureMap.Groups.Count);
            var group = structureMap.Groups[0];
            Assert.AreEqual("tutorial", group.Name);
            Assert.AreEqual(2, group.Parameters.Count);
            
            // Verify all rules were parsed (3 rules total)
            Assert.AreEqual(3, group.Rules.Count, 
                "Should have 3 rules despite comments");
            
            // Verify first rule (name mapping)
            var rule1 = group.Rules[0];
            Assert.AreEqual(1, rule1.Sources.Count);
            Assert.AreEqual("src", rule1.Sources[0].Context);
            Assert.AreEqual("name", rule1.Sources[0].Element);
            Assert.AreEqual("vName", rule1.Sources[0].Variable);
            
            // Verify second rule (gender mapping)
            var rule2 = group.Rules[1];
            Assert.AreEqual(1, rule2.Sources.Count);
            Assert.AreEqual("src", rule2.Sources[0].Context);
            Assert.AreEqual("gender", rule2.Sources[0].Element);
            Assert.AreEqual("vGender", rule2.Sources[0].Variable);
            
            // Verify third rule (telecom mapping with nested rules)
            var rule3 = group.Rules[2];
            Assert.AreEqual(1, rule3.Sources.Count);
            Assert.AreEqual("src", rule3.Sources[0].Context);
            Assert.AreEqual("telecom", rule3.Sources[0].Element);
            Assert.IsNotNull(rule3.Dependent, "Should have dependent expression");
            Assert.AreEqual(1, rule3.Dependent.Rules.Count, 
                "Should have nested rule despite comments");
            
            // Verify nested rule
            var nestedRule = rule3.Dependent.Rules[0];
            Assert.AreEqual(1, nestedRule.Sources.Count);
            Assert.AreEqual("vTelecom", nestedRule.Sources[0].Context);
            Assert.AreEqual("value", nestedRule.Sources[0].Element);
        }

        [TestMethod]
        public void TestFmlWithBlockComments()
        {
            // FML with block comments only
            var fmlWithBlockComments = """
                /* Copyright (c) 2024
                 * This is a multi-line copyright notice
                 * All rights reserved
                 */
                map "http://example.org/test" = test
                
                /* Group definition follows */
                group test(source src, target tgt) {
                  /* Simple copy rule */
                  src -> tgt;
                }
                """;

            var result = FmlParser.Parse(fmlWithBlockComments);

            Assert.IsInstanceOfType<ParseResult.Success>(result);
            var success = (ParseResult.Success)result;
            
            Assert.IsNotNull(success.StructureMap);
            Assert.AreEqual(1, success.StructureMap.Groups.Count);
            Assert.AreEqual(1, success.StructureMap.Groups[0].Rules.Count);
        }

        [TestMethod]
        public void TestFmlWithNestedComments()
        {
            // FML with comments in various positions
            var fmlWithNestedComments = """
                map "http://example.org/test" = test
                
                group test(
                  source src,  // Source parameter
                  target tgt   // Target parameter
                ) {
                  // Rule 1
                  src.name /* source name */ -> tgt.name /* target name */;
                  
                  /* Rule 2 with complex mapping */
                  src.identifier as id /* identifier */ -> tgt.id = id /* copy */;
                }
                """;

            var result = FmlParser.Parse(fmlWithNestedComments);

            Assert.IsInstanceOfType<ParseResult.Success>(result);
            var success = (ParseResult.Success)result;
            var map = success.StructureMap;
            
            Assert.AreEqual(1, map.Groups.Count);
            var group = map.Groups[0];
            Assert.AreEqual(2, group.Parameters.Count);
            Assert.AreEqual(2, group.Rules.Count, "Should parse 2 rules with inline comments");
        }

        [TestMethod]
        public void TestFmlWithOnlyComments()
        {
            // Edge case: FML that is mostly comments
            var fmlMostlyComments = """
                // Comment line 1
                // Comment line 2
                map "http://example.org/minimal" = minimal
                // Comment line 3
                // Comment line 4
                group minimal(source s, target t) {
                  // Comment line 5
                  s -> t;
                  // Comment line 6
                }
                // Comment line 7
                """;

            var result = FmlParser.Parse(fmlMostlyComments);

            Assert.IsInstanceOfType<ParseResult.Success>(result);
            var success = (ParseResult.Success)result;
            
            Assert.AreEqual("minimal", success.StructureMap.MapDeclaration?.Identifier);
            Assert.AreEqual(1, success.StructureMap.Groups.Count);
        }

        [TestMethod]
        public void TestFmlCommentsDoNotAffectPositionTracking()
        {
            // Verify that comments don't break position tracking
            var fmlWithComments = """
                map "http://example.org/test" = test
                
                // This comment should not affect line numbers
                group test(source src : Patient, target tgt : Bundle) {
                  /* Block comment */
                  src.name as vName -> tgt.name = vName;
                }
                """;

            var result = FmlParser.Parse(fmlWithComments);
            Assert.IsInstanceOfType<ParseResult.Success>(result);
            
            var success = (ParseResult.Success)result;
            var rule = success.StructureMap.Groups[0].Rules[0];
            
            // Position should still be tracked correctly on the rule itself
            Assert.IsNotNull(rule.Position, "Rule should have position tracking");
            
            // Verify line numbers are reasonable (accounting for comments)
            Assert.IsTrue(rule.Position.StartLine > 0, "Rule should have valid start line");
            Assert.IsTrue(rule.Position.StartLine >= 6, "Rule should be after comments (around line 6)");
            
            // Verify sources and targets were parsed correctly despite comments
            Assert.AreEqual(1, rule.Sources.Count, "Should have one source");
            Assert.AreEqual("src", rule.Sources[0].Context);
            Assert.AreEqual("name", rule.Sources[0].Element);
            
            Assert.AreEqual(1, rule.Targets.Count, "Should have one target");
            Assert.AreEqual("tgt", rule.Targets[0].Context);
            Assert.AreEqual("name", rule.Targets[0].Element);
        }
    }
}
