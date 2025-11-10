using fml_processor;
using fml_processor.Models;

namespace fml_tester
{
    [TestClass]
    public sealed class SerializerTests
    {
        [TestMethod]
        public void TestBasicSerialization()
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

            // Parse
            var result = FmlParser.Parse(fmlText);
            Assert.IsInstanceOfType<ParseResult.Success>(result);
            
            var success = (ParseResult.Success)result;
            var structureMap = success.StructureMap;

            // Serialize
            var serialized = FmlSerializer.Serialize(structureMap);

            // Verify it's not empty
            Assert.IsFalse(string.IsNullOrWhiteSpace(serialized));

            // Parse the serialized version
            var reParseResult = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(reParseResult, 
                $"Serialized FML should parse successfully. Serialized output:\n{serialized}");

            var reParseSuccess = (ParseResult.Success)reParseResult;
            var reParsedMap = reParseSuccess.StructureMap;

            // Verify structure is preserved
            Assert.AreEqual(structureMap.MapDeclaration?.Url, reParsedMap.MapDeclaration?.Url);
            Assert.AreEqual(structureMap.MapDeclaration?.Identifier, reParsedMap.MapDeclaration?.Identifier);
            Assert.AreEqual(structureMap.Structures.Count, reParsedMap.Structures.Count);
            Assert.AreEqual(structureMap.Groups.Count, reParsedMap.Groups.Count);
            Assert.AreEqual(structureMap.Groups[0].Rules.Count, reParsedMap.Groups[0].Rules.Count);
        }

        [TestMethod]
        public void TestSerializeWithMetadata()
        {
            var structureMap = new FmlStructureMap
            {
                Metadata = new List<MetadataDeclaration>
                {
                    new MetadataDeclaration
                    {
                        Path = "url",
                        Value = "http://example.org/fhir/StructureMap/test"
                    },
                    new MetadataDeclaration
                    {
                        Path = "name",
                        Value = "TestMap"
                    },
                    new MetadataDeclaration
                    {
                        Path = "version",
                        Value = "1.0.0"
                    }
                },
                MapDeclaration = new MapDeclaration
                {
                    Url = "http://example.org/fhir/StructureMap/test",
                    Identifier = "test"
                },
                Groups = new List<GroupDeclaration>
                {
                    new GroupDeclaration
                    {
                        Name = "main",
                        Parameters = new List<GroupParameter>
                        {
                            new GroupParameter { Mode = ParameterMode.Source, Name = "src" },
                            new GroupParameter { Mode = ParameterMode.Target, Name = "tgt" }
                        },
                        Rules = new List<Rule>
                        {
                            new Rule
                            {
                                Sources = new List<RuleSource>
                                {
                                    new RuleSource { Context = "src" }
                                },
                                Targets = new List<RuleTarget>
                                {
                                    new RuleTarget { Context = "tgt" }
                                }
                            }
                        }
                    }
                }
            };

            var serialized = FmlSerializer.Serialize(structureMap);

            // Verify metadata is included
            Assert.IsTrue(serialized.Contains("/// url = "));
            Assert.IsTrue(serialized.Contains("/// name = "));
            Assert.IsTrue(serialized.Contains("/// version = "));

            // Verify it parses back
            var result = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(result);

            var success = (ParseResult.Success)result;
            Assert.AreEqual(3, success.StructureMap.Metadata.Count);
        }

        [TestMethod]
        public void TestSerializeComplexRule()
        {
            var fmlText = """
                map "http://example.org/test" = test
                
                group test(source src : Patient, target tgt : Bundle) {
                  src.name as vName where (use = 'official') -> tgt.entry.name = vName;
                  src.identifier : Identifier as id -> tgt.id = create('Identifier') as newId then {
                    id.value -> newId.value;
                    id.system -> newId.system;
                  };
                }
                """;

            var result = FmlParser.Parse(fmlText);
            Assert.IsInstanceOfType<ParseResult.Success>(result);

            var success = (ParseResult.Success)result;
            var serialized = FmlSerializer.Serialize(success.StructureMap);

            // Verify complex features are serialized
            Assert.IsTrue(serialized.Contains("where ("));
            Assert.IsTrue(serialized.Contains("then {"));

            // Verify it re-parses
            var reParseResult = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(reParseResult,
                $"Complex FML should re-parse. Serialized:\n{serialized}");

            var reParsed = ((ParseResult.Success)reParseResult).StructureMap;
            Assert.AreEqual(2, reParsed.Groups[0].Rules.Count);
        }

        [TestMethod]
        public void TestSerializeWithImportsAndConstants()
        {
            var structureMap = new FmlStructureMap
            {
                MapDeclaration = new MapDeclaration
                {
                    Url = "http://example.org/test",
                    Identifier = "test"
                },
                Imports = new List<ImportDeclaration>
                {
                    new ImportDeclaration { Url = "http://example.org/common" }
                },
                Constants = new List<ConstantDeclaration>
                {
                    new ConstantDeclaration 
                    { 
                        Name = "defaultSystem", 
                        Expression = "'http://example.org/fhir/system'" 
                    }
                },
                Groups = new List<GroupDeclaration>
                {
                    new GroupDeclaration
                    {
                        Name = "main",
                        Parameters = new List<GroupParameter>
                        {
                            new GroupParameter { Mode = ParameterMode.Source, Name = "src" },
                            new GroupParameter { Mode = ParameterMode.Target, Name = "tgt" }
                        },
                        Rules = new List<Rule>
                        {
                            new Rule
                            {
                                Sources = new List<RuleSource>
                                {
                                    new RuleSource { Context = "src" }
                                },
                                Targets = new List<RuleTarget>
                                {
                                    new RuleTarget { Context = "tgt" }
                                }
                            }
                        }
                    }
                }
            };

            var serialized = FmlSerializer.Serialize(structureMap);

            // Verify imports and constants
            Assert.IsTrue(serialized.Contains("imports "));
            Assert.IsTrue(serialized.Contains("let defaultSystem = "));

            // Verify it parses back
            var result = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(result);

            var success = (ParseResult.Success)result;
            Assert.AreEqual(1, success.StructureMap.Imports.Count);
            Assert.AreEqual(1, success.StructureMap.Constants.Count);
        }

        [TestMethod]
        public void TestSerializeGroupWithExtends()
        {
            var structureMap = new FmlStructureMap
            {
                MapDeclaration = new MapDeclaration
                {
                    Url = "http://example.org/test",
                    Identifier = "test"
                },
                Groups = new List<GroupDeclaration>
                {
                    new GroupDeclaration
                    {
                        Name = "base",
                        Parameters = new List<GroupParameter>
                        {
                            new GroupParameter { Mode = ParameterMode.Source, Name = "src" },
                            new GroupParameter { Mode = ParameterMode.Target, Name = "tgt" }
                        },
                        Rules = new List<Rule>()
                    },
                    new GroupDeclaration
                    {
                        Name = "derived",
                        Extends = "base",
                        TypeMode = GroupTypeMode.Types,
                        Parameters = new List<GroupParameter>
                        {
                            new GroupParameter { Mode = ParameterMode.Source, Name = "src", Type = "Patient" },
                            new GroupParameter { Mode = ParameterMode.Target, Name = "tgt", Type = "Bundle" }
                        },
                        Rules = new List<Rule>()
                    }
                }
            };

            var serialized = FmlSerializer.Serialize(structureMap);

            // Verify extends and type mode
            Assert.IsTrue(serialized.Contains("extends base"));
            Assert.IsTrue(serialized.Contains("<<types>>"));

            // Verify it parses back
            var result = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(result);

            var success = (ParseResult.Success)result;
            Assert.AreEqual("base", success.StructureMap.Groups[1].Extends);
            Assert.AreEqual(GroupTypeMode.Types, success.StructureMap.Groups[1].TypeMode);
        }

        [TestMethod]
        public void TestRoundTripPreservesSemantics()
        {
            // Complex FML with many features
            var fmlText = """
                /// url = 'http://example.org/fhir/StructureMap/comprehensive'
                /// name = 'ComprehensiveTest'
                
                map "http://example.org/fhir/StructureMap/comprehensive" = comprehensive
                
                uses "http://hl7.org/fhir/StructureDefinition/Patient" as source
                uses "http://hl7.org/fhir/StructureDefinition/Bundle" as target
                
                imports "http://example.org/common"
                
                let system = 'http://example.org/system';
                
                group main(source src : Patient, target tgt : Bundle) {
                  src.name as n where (use = 'official') -> tgt.entry.name = n "official name";
                  src.identifier : Identifier as id -> tgt.id = create('Identifier') as newId then {
                    id.value -> newId.value;
                    id.system -> newId.system;
                  };
                }
                """;

            // Parse original
            var originalResult = FmlParser.Parse(fmlText);
            Assert.IsInstanceOfType<ParseResult.Success>(originalResult, 
                originalResult is ParseResult.Failure f ? $"Original parse failed: {string.Join(", ", f.Errors.Select(e => e.Message))}" : "");
            var originalMap = ((ParseResult.Success)originalResult).StructureMap;

            // Serialize
            var serialized = FmlSerializer.Serialize(originalMap);

            // Parse serialized version
            var serializedResult = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(serializedResult,
                $"Round-trip should succeed. Serialized:\n{serialized}");
            var serializedMap = ((ParseResult.Success)serializedResult).StructureMap;

            // Verify all major elements are preserved
            Assert.AreEqual(originalMap.Metadata.Count, serializedMap.Metadata.Count);
            Assert.AreEqual(originalMap.MapDeclaration?.Url, serializedMap.MapDeclaration?.Url);
            Assert.AreEqual(originalMap.Structures.Count, serializedMap.Structures.Count);
            Assert.AreEqual(originalMap.Imports.Count, serializedMap.Imports.Count);
            Assert.AreEqual(originalMap.Constants.Count, serializedMap.Constants.Count);
            Assert.AreEqual(originalMap.Groups.Count, serializedMap.Groups.Count);

            // Verify group details
            var originalGroup = originalMap.Groups[0];
            var serializedGroup = serializedMap.Groups[0];
            Assert.AreEqual(originalGroup.Name, serializedGroup.Name);
            Assert.AreEqual(originalGroup.Parameters.Count, serializedGroup.Parameters.Count);
            Assert.AreEqual(originalGroup.Rules.Count, serializedGroup.Rules.Count);

            // Verify first rule
            var originalRule = originalGroup.Rules[0];
            var serializedRule = serializedGroup.Rules[0];
            Assert.AreEqual(originalRule.Name, serializedRule.Name);
            Assert.AreEqual(originalRule.Sources.Count, serializedRule.Sources.Count);
            Assert.AreEqual(originalRule.Targets.Count, serializedRule.Targets.Count);
        }

        [TestMethod]
        public void TestSerializeNullInputThrows()
        {
            try
            {
                FmlSerializer.Serialize(null!);
                Assert.Fail("Expected ArgumentNullException to be thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        [TestMethod]
        public void TestSerializeMinimalMap()
        {
            var structureMap = new FmlStructureMap
            {
                MapDeclaration = new MapDeclaration
                {
                    Url = "http://example.org/minimal",
                    Identifier = "minimal"
                },
                Groups = new List<GroupDeclaration>
                {
                    new GroupDeclaration
                    {
                        Name = "main",
                        Parameters = new List<GroupParameter>
                        {
                            new GroupParameter { Mode = ParameterMode.Source, Name = "s" },
                            new GroupParameter { Mode = ParameterMode.Target, Name = "t" }
                        },
                        Rules = new List<Rule>
                        {
                            new Rule
                            {
                                Sources = new List<RuleSource>
                                {
                                    new RuleSource { Context = "s" }
                                },
                                Targets = new List<RuleTarget>
                                {
                                    new RuleTarget { Context = "t" }
                                }
                            }
                        }
                    }
                }
            };

            var serialized = FmlSerializer.Serialize(structureMap);

            // Should be valid FML
            var result = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(result,
                $"Minimal map should serialize and parse. Output:\n{serialized}");
        }

        [TestMethod]
        public void TestSerializeSourceWithCardinality()
        {
            var fmlText = """
                map "http://example.org/test" = test
                
                group test(source src, target tgt) {
                  src.name 1..* -> tgt.name;
                  src.identifier 0..1 -> tgt.id;
                }
                """;

            var result = FmlParser.Parse(fmlText);
            Assert.IsInstanceOfType<ParseResult.Success>(result);

            var success = (ParseResult.Success)result;
            var serialized = FmlSerializer.Serialize(success.StructureMap);

            // Verify cardinality is serialized
            Assert.IsTrue(serialized.Contains("1..*") || serialized.Contains("0..1"));

            // Verify it re-parses
            var reParseResult = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(reParseResult,
                $"Cardinality should round-trip. Serialized:\n{serialized}");
        }

        [TestMethod]
        public void TestSerializeSourceWithListMode()
        {
            var structureMap = new FmlStructureMap
            {
                MapDeclaration = new MapDeclaration
                {
                    Url = "http://example.org/test",
                    Identifier = "test"
                },
                Groups = new List<GroupDeclaration>
                {
                    new GroupDeclaration
                    {
                        Name = "main",
                        Parameters = new List<GroupParameter>
                        {
                            new GroupParameter { Mode = ParameterMode.Source, Name = "src" },
                            new GroupParameter { Mode = ParameterMode.Target, Name = "tgt" }
                        },
                        Rules = new List<Rule>
                        {
                            new Rule
                            {
                                Sources = new List<RuleSource>
                                {
                                    new RuleSource 
                                    { 
                                        Context = "src",
                                        Element = "name",
                                        ListMode = SourceListMode.First,
                                        Variable = "n"
                                    }
                                },
                                Targets = new List<RuleTarget>
                                {
                                    new RuleTarget 
                                    { 
                                        Context = "tgt",
                                        Element = "name",
                                        ListMode = TargetListMode.Single
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var serialized = FmlSerializer.Serialize(structureMap);

            // Verify list modes are serialized
            Assert.IsTrue(serialized.Contains("first"));
            Assert.IsTrue(serialized.Contains("single"));

            // Verify it parses back
            var result = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(result,
                $"List modes should serialize. Output:\n{serialized}");
        }

        [TestMethod]
        public void TestSerializeGroupInvocation()
        {
            var structureMap = new FmlStructureMap
            {
                MapDeclaration = new MapDeclaration
                {
                    Url = "http://example.org/test",
                    Identifier = "test"
                },
                Groups = new List<GroupDeclaration>
                {
                    new GroupDeclaration
                    {
                        Name = "helper",
                        Parameters = new List<GroupParameter>
                        {
                            new GroupParameter { Mode = ParameterMode.Source, Name = "s" },
                            new GroupParameter { Mode = ParameterMode.Target, Name = "t" }
                        },
                        Rules = new List<Rule>()
                    },
                    new GroupDeclaration
                    {
                        Name = "main",
                        Parameters = new List<GroupParameter>
                        {
                            new GroupParameter { Mode = ParameterMode.Source, Name = "src" },
                            new GroupParameter { Mode = ParameterMode.Target, Name = "tgt" }
                        },
                        Rules = new List<Rule>
                        {
                            new Rule
                            {
                                Sources = new List<RuleSource>
                                {
                                    new RuleSource { Context = "src", Element = "name", Variable = "n" }
                                },
                                Targets = new List<RuleTarget>
                                {
                                    new RuleTarget { Context = "tgt", Element = "name", Variable = "tn" }
                                },
                                Dependent = new RuleDependent
                                {
                                    Invocations = new List<GroupInvocation>
                                    {
                                        new GroupInvocation
                                        {
                                            Name = "helper",
                                            Parameters = new List<InvocationParameter>
                                            {
                                                new InvocationParameter 
                                                { 
                                                    Type = InvocationParameterType.Identifier, 
                                                    Value = "n" 
                                                },
                                                new InvocationParameter 
                                                { 
                                                    Type = InvocationParameterType.Identifier, 
                                                    Value = "tn" 
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var serialized = FmlSerializer.Serialize(structureMap);

            // Verify invocation is serialized
            Assert.IsTrue(serialized.Contains("then helper("));

            // Verify it parses back
            var result = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(result,
                $"Group invocation should serialize. Output:\n{serialized}");

            var success = (ParseResult.Success)result;
            var mainGroup = success.StructureMap.Groups.First(g => g.Name == "main");
            Assert.IsNotNull(mainGroup.Rules[0].Dependent);
            Assert.AreEqual(1, mainGroup.Rules[0].Dependent.Invocations.Count);
        }

        [TestMethod]
        public void TestParseAllR4toR5Maps()
        {
            // Scan all FML files in the R4toR5 directory
            var fmlDirectory = @"c:\git\hl7\fhir-cross-version\input\R4toR5";
            
            if (!Directory.Exists(fmlDirectory))
            {
                Assert.Inconclusive($"Directory not found: {fmlDirectory}. This test requires the FHIR cross-version repository.");
                return;
            }

            var fmlFiles = Directory.GetFiles(fmlDirectory, "*.fml", SearchOption.AllDirectories);
            
            Assert.IsTrue(fmlFiles.Length > 0, 
                $"No FML files found in {fmlDirectory}");

            var failures = new List<(string FileName, string Error)>();
            var successCount = 0;

            foreach (var fmlFile in fmlFiles)
            {
                try
                {
                    var fmlText = File.ReadAllText(fmlFile);
                    var result = FmlParser.Parse(fmlText);

                    if (result is ParseResult.Failure failure)
                    {
                        var errors = string.Join(", ", failure.Errors.Select(e => $"{e.Location}: {e.Message}"));
                        failures.Add((Path.GetFileName(fmlFile), errors));
                    }
                    else
                    {
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    failures.Add((Path.GetFileName(fmlFile), $"Exception: {ex.Message}"));
                }
            }

            // Report results
            Console.WriteLine($"Parsed {successCount} out of {fmlFiles.Length} FML files successfully.");
            
            if (failures.Any())
            {
                Console.WriteLine($"\nFailed to parse {failures.Count} files:");
                foreach (var (fileName, error) in failures)
                {
                    Console.WriteLine($"  {fileName}: {error}");
                }
                
                Assert.Fail($"Failed to parse {failures.Count} out of {fmlFiles.Length} FML files. See output for details.");
            }

            Assert.AreEqual(fmlFiles.Length, successCount, 
                $"All {fmlFiles.Length} FML files should parse successfully.");
        }

        [TestMethod]
        public void TestRoundTripAllR4toR5Maps()
        {
            // Scan all FML files in the R4toR5 directory
            var fmlDirectory = @"c:\git\hl7\fhir-cross-version\input\R4toR5";
            
            if (!Directory.Exists(fmlDirectory))
            {
                Assert.Inconclusive($"Directory not found: {fmlDirectory}. This test requires the FHIR cross-version repository.");
                return;
            }

            var fmlFiles = Directory.GetFiles(fmlDirectory, "*.fml", SearchOption.AllDirectories);
            
            Assert.IsTrue(fmlFiles.Length > 0, 
                $"No FML files found in {fmlDirectory}");

            var failures = new List<(string FileName, string Error)>();
            var successCount = 0;

            foreach (var fmlFile in fmlFiles)
            {
                try
                {
                    var originalFml = File.ReadAllText(fmlFile);
                    
                    // Parse original
                    var parseResult = FmlParser.Parse(originalFml);
                    if (parseResult is ParseResult.Failure parseFailure)
                    {
                        var errors = string.Join(", ", parseFailure.Errors.Select(e => $"{e.Location}: {e.Message}"));
                        failures.Add((Path.GetFileName(fmlFile), $"Parse failed: {errors}"));
                        continue;
                    }

                    var originalMap = ((ParseResult.Success)parseResult).StructureMap;

                    // Serialize
                    string serialized;
                    try
                    {
                        serialized = FmlSerializer.Serialize(originalMap);
                    }
                    catch (Exception ex)
                    {
                        failures.Add((Path.GetFileName(fmlFile), $"Serialization failed: {ex.Message}"));
                        continue;
                    }

                    // Parse serialized version
                    var reParseResult = FmlParser.Parse(serialized);
                    if (reParseResult is ParseResult.Failure reParseFailure)
                    {
                        var errors = string.Join(", ", reParseFailure.Errors.Select(e => $"{e.Location}: {e.Message}"));
                        failures.Add((Path.GetFileName(fmlFile), $"Re-parse failed: {errors}"));
                        
                        // Save the serialized output for debugging
                        var debugFile = Path.Combine(Path.GetTempPath(), $"fml_debug_{Path.GetFileName(fmlFile)}");
                        File.WriteAllText(debugFile, serialized);
                        Console.WriteLine($"Debug: Serialized output saved to {debugFile}");
                        continue;
                    }

                    var reParsedMap = ((ParseResult.Success)reParseResult).StructureMap;

                    // Verify structure is preserved
                    try
                    {
                        // Check metadata count
                        if (originalMap.Metadata.Count != reParsedMap.Metadata.Count)
                        {
                            failures.Add((Path.GetFileName(fmlFile), 
                                $"Metadata count mismatch: {originalMap.Metadata.Count} vs {reParsedMap.Metadata.Count}"));
                            continue;
                        }

                        // Check map declaration
                        if (originalMap.MapDeclaration?.Url != reParsedMap.MapDeclaration?.Url ||
                            originalMap.MapDeclaration?.Identifier != reParsedMap.MapDeclaration?.Identifier)
                        {
                            failures.Add((Path.GetFileName(fmlFile), 
                                $"Map declaration mismatch"));
                            continue;
                        }

                        // Check structures count
                        if (originalMap.Structures.Count != reParsedMap.Structures.Count)
                        {
                            failures.Add((Path.GetFileName(fmlFile), 
                                $"Structures count mismatch: {originalMap.Structures.Count} vs {reParsedMap.Structures.Count}"));
                            continue;
                        }

                        // Check imports count
                        if (originalMap.Imports.Count != reParsedMap.Imports.Count)
                        {
                            failures.Add((Path.GetFileName(fmlFile), 
                                $"Imports count mismatch: {originalMap.Imports.Count} vs {reParsedMap.Imports.Count}"));
                            continue;
                        }

                        // Check constants count
                        if (originalMap.Constants.Count != reParsedMap.Constants.Count)
                        {
                            failures.Add((Path.GetFileName(fmlFile), 
                                $"Constants count mismatch: {originalMap.Constants.Count} vs {reParsedMap.Constants.Count}"));
                            continue;
                        }

                        // Check groups count
                        if (originalMap.Groups.Count != reParsedMap.Groups.Count)
                        {
                            failures.Add((Path.GetFileName(fmlFile), 
                                $"Groups count mismatch: {originalMap.Groups.Count} vs {reParsedMap.Groups.Count}"));
                            continue;
                        }

                        // Check each group's rule count
                        for (int i = 0; i < originalMap.Groups.Count; i++)
                        {
                            if (originalMap.Groups[i].Rules.Count != reParsedMap.Groups[i].Rules.Count)
                            {
                                failures.Add((Path.GetFileName(fmlFile), 
                                    $"Group '{originalMap.Groups[i].Name}' rules count mismatch: {originalMap.Groups[i].Rules.Count} vs {reParsedMap.Groups[i].Rules.Count}"));
                                goto ContinueOuter; // Skip to next file
                            }
                        }

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failures.Add((Path.GetFileName(fmlFile), $"Verification failed: {ex.Message}"));
                    }
                }
                catch (Exception ex)
                {
                    failures.Add((Path.GetFileName(fmlFile), $"Exception: {ex.Message}"));
                }

                ContinueOuter:;
            }

            // Report results
            Console.WriteLine($"Successfully round-tripped {successCount} out of {fmlFiles.Length} FML files.");
            
            if (failures.Any())
            {
                Console.WriteLine($"\nFailed to round-trip {failures.Count} files:");
                foreach (var (fileName, error) in failures.Take(20)) // Limit to first 20 for readability
                {
                    Console.WriteLine($"  {fileName}: {error}");
                }
                
                if (failures.Count > 20)
                {
                    Console.WriteLine($"  ... and {failures.Count - 20} more.");
                }
                
                Assert.Fail($"Failed to round-trip {failures.Count} out of {fmlFiles.Length} FML files. See output for details.");
            }

            Assert.AreEqual(fmlFiles.Length, successCount, 
                $"All {fmlFiles.Length} FML files should round-trip successfully.");
        }

        [TestMethod]
        public void TestTextBasedRoundTrip_WithComments()
        {
            // Test that comments are preserved in round-trip
            // This test is expected to FAIL until comment preservation is implemented
            var fmlWithComments = """
                // Copyright (c) 2024
                // This is a header comment
                
                /// url = 'http://example.org/fhir/StructureMap/test'
                /// name = 'TestMap'
                
                map "http://example.org/fhir/StructureMap/test" = test
                
                // Structure declarations
                uses "http://hl7.org/fhir/StructureDefinition/Patient" as source
                uses "http://hl7.org/fhir/StructureDefinition/Bundle" as target
                
                /* This is a block comment
                 * describing the main group
                 */
                group main(source src : Patient, target tgt : Bundle) {
                  // Simple copy rule
                  src.name as n -> tgt.name = n;
                  
                  /* Multi-line comment
                     before complex rule */
                  src.identifier : Identifier as id -> tgt.id = create('Identifier') as newId then {
                    // Nested rule comment
                    id.value -> newId.value; // End of line comment
                  };
                }
                // End of file comment
                """;

            // Parse original
            var parseResult = FmlParser.Parse(fmlWithComments);
            Assert.IsInstanceOfType<ParseResult.Success>(parseResult,
                "FML with comments should parse successfully");

            var originalMap = ((ParseResult.Success)parseResult).StructureMap;

            // Serialize
            var serialized = FmlSerializer.Serialize(originalMap);

            // Normalize whitespace for comparison (collapse multiple blank lines, trim lines)
            var normalizedOriginal = NormalizeWhitespace(fmlWithComments);
            var normalizedSerialized = NormalizeWhitespace(serialized);

            // This assertion will FAIL until comment preservation is implemented
            Assert.AreEqual(normalizedOriginal, normalizedSerialized,
                "Serialized FML should preserve comments. This test documents the expected behavior for comment preservation feature.");
        }

        [TestMethod]
        public void TestTextBasedRoundTrip_WithoutComments()
        {
            // Test text-based round-trip on FML without comments
            // This should pass as it doesn't require comment preservation
            var fmlWithoutComments = """
                /// url = 'http://example.org/fhir/StructureMap/simple'
                /// name = 'SimpleMap'
                
                map "http://example.org/fhir/StructureMap/simple" = simple
                
                uses "http://hl7.org/fhir/StructureDefinition/Patient" as source
                uses "http://hl7.org/fhir/StructureDefinition/Bundle" as target
                
                group main(source src : Patient, target tgt : Bundle) {
                  src.name as n -> tgt.name = n;
                  src.identifier as id -> tgt.id = id;
                }
                """;

            // Parse original
            var parseResult = FmlParser.Parse(fmlWithoutComments);
            Assert.IsInstanceOfType<ParseResult.Success>(parseResult);

            var originalMap = ((ParseResult.Success)parseResult).StructureMap;

            // Serialize
            var serialized = FmlSerializer.Serialize(originalMap);

            // Parse serialized version to ensure it's valid
            var reParseResult = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(reParseResult,
                $"Serialized FML should parse. Output:\n{serialized}");

            // Normalize whitespace for comparison
            var normalizedOriginal = NormalizeWhitespace(fmlWithoutComments);
            var normalizedSerialized = NormalizeWhitespace(serialized);

            // Text-based comparison (this may not be exact due to formatting differences)
            // For now, just verify they're similar in structure
            Assert.IsTrue(normalizedSerialized.Contains("map \"http://example.org/fhir/StructureMap/simple\" = simple"),
                "Serialized FML should contain map declaration");
            Assert.IsTrue(normalizedSerialized.Contains("group main(source src : Patient, target tgt : Bundle)"),
                "Serialized FML should contain group declaration");
            Assert.IsTrue(normalizedSerialized.Contains("src.name as n -> tgt.name = n"),
                "Serialized FML should contain rules");
        }

        [TestMethod]
        public void TestCommentPreservationMetadata()
        {
            // Verify that comments are preserved in the object model and round-trip correctly
            var fmlWithComments = """
                // Header comment
                /// url = 'http://example.org/test'
                
                map "http://example.org/test" = test
                
                group main(source src, target tgt) {
                  // Rule comment
                  src -> tgt;
                }
                """;

            var parseResult = FmlParser.Parse(fmlWithComments);
            Assert.IsInstanceOfType<ParseResult.Success>(parseResult);

            var structureMap = ((ParseResult.Success)parseResult).StructureMap;

            // Verify header comments are captured in leading hidden tokens
            Assert.IsNotNull(structureMap.LeadingHiddenTokens, 
                "Structure map should have captured leading hidden tokens");
            Assert.IsTrue(structureMap.LeadingHiddenTokens.Any(t => t.IsComment), 
                "Leading hidden tokens should include header comment");
            
            // Verify comments are preserved in serialization
            var serialized = FmlSerializer.Serialize(structureMap);
            Assert.IsTrue(serialized.Contains("// Header comment"), 
                "Serialized output should contain the header comment");
            Assert.IsTrue(serialized.Contains("// Rule comment"), 
                "Serialized output should contain the rule comment");
            
            // Verify round-trip preserves comments
            var reParseResult = FmlParser.Parse(serialized);
            Assert.IsInstanceOfType<ParseResult.Success>(reParseResult, 
                "Serialized FML with comments should parse successfully");
            
            var reParsedMap = ((ParseResult.Success)reParseResult).StructureMap;
            
            // Re-serialize and verify comments are still there
            var reSerialized = FmlSerializer.Serialize(reParsedMap);
            Assert.IsTrue(reSerialized.Contains("// Header comment"), 
                "Round-trip should preserve header comment");
            Assert.IsTrue(reSerialized.Contains("// Rule comment"), 
                "Round-trip should preserve rule comment");
        }

        /// <summary>
        /// Normalizes whitespace for text comparison by:
        /// - Trimming each line
        /// - Removing empty lines
        /// - Collapsing multiple spaces to single space
        /// - Normalizing line endings
        /// </summary>
        private static string NormalizeWhitespace(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            var normalized = lines
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " "));
            
            return string.Join("\n", normalized);
        }
    }
}
