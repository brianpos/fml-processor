using fml_processor;
using fml_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace fml_tester;

/// <summary>
/// Analytics tests to measure real-world FML feature usage across R4toR5 conversion maps
/// </summary>
[TestClass]
public class FmlFeatureAnalyticsTests
{
    [TestMethod]
    public void AnalyzeR4toR5FeatureUsage()
    {
        // Path to R4toR5 FML files
        var fmlDirectory = @"c:\git\hl7\fhir-cross-version\input\R4toR5";
        
        if (!Directory.Exists(fmlDirectory))
        {
            Assert.Inconclusive($"Directory not found: {fmlDirectory}. This test requires the FHIR cross-version repository.");
            return;
        }

        var fmlFiles = Directory.GetFiles(fmlDirectory, "*.fml", SearchOption.AllDirectories);
        
        Assert.IsTrue(fmlFiles.Length > 0, 
            $"No FML files found in {fmlDirectory}");

        // Initialize counters
        var analytics = new FmlFeatureAnalytics();
        var parseFailures = new List<string>();

        // Parse all files and collect analytics
        foreach (var fmlFile in fmlFiles)
        {
            try
            {
                var fmlText = File.ReadAllText(fmlFile);
                var result = FmlParser.Parse(fmlText);

                if (result is ParseResult.Success success)
                {
                    analytics.AnalyzeStructureMap(success.StructureMap);
                }
                else if (result is ParseResult.Failure failure)
                {
                    parseFailures.Add(Path.GetFileName(fmlFile));
                }
            }
            catch (Exception ex)
            {
                parseFailures.Add($"{Path.GetFileName(fmlFile)}: {ex.Message}");
            }
        }

        // Generate report
        var report = analytics.GenerateReport(fmlFiles.Length, parseFailures);
        
        Console.WriteLine(report);

        // Also write to file for easy reference
        var reportPath = Path.Combine(Path.GetTempPath(), "fml-feature-analytics.md");
        File.WriteAllText(reportPath, report);
        Console.WriteLine($"\nReport saved to: {reportPath}");

        // Assert we parsed most files successfully
        var successRate = ((double)(fmlFiles.Length - parseFailures.Count) / fmlFiles.Length) * 100;
        Assert.IsTrue(successRate >= 90, 
            $"Success rate {successRate:F1}% is below 90%. Failed files: {string.Join(", ", parseFailures.Take(5))}");
    }
}

/// <summary>
/// Collects analytics about FML feature usage
/// </summary>
public class FmlFeatureAnalytics
{
    // File-level counters
    public int TotalFiles { get; set; }
    public int FilesWithMetadata { get; set; }
    public int FilesWithConceptMaps { get; set; }
    public int FilesWithImports { get; set; }
    public int FilesWithConstants { get; set; }
    
    // Declaration counters
    public int MetadataDeclarations { get; set; }
    public int MapDeclarations { get; set; }
    public int StructureDeclarations { get; set; }
    public int ImportDeclarations { get; set; }
    public int ConstantDeclarations { get; set; }
    public int ConceptMapDeclarations { get; set; }
    
    // Group counters
    public int Groups { get; set; }
    public int GroupsWithExtends { get; set; }
    public int GroupsWithTypeMode { get; set; }
    public int GroupParameters { get; set; }
    
    // Rule counters
    public int TotalRules { get; set; }
    public int RulesWithNames { get; set; }
    public int RulesWithMultipleSources { get; set; }
    public int RulesWithMultipleTargets { get; set; }
    public int RulesWithDependents { get; set; }
    public int RulesWithNestedRules { get; set; }
    
    // Source counters
    public int Sources { get; set; }
    public int SourcesWithType { get; set; }
    public int SourcesWithCardinality { get; set; }
    public int SourcesWithDefault { get; set; }
    public int SourcesWithListMode { get; set; }
    public int SourcesWithVariable { get; set; }
    public int SourcesWithCondition { get; set; }
    public int SourcesWithCheck { get; set; }
    public int SourcesWithLog { get; set; }
    
    // Target counters
    public int Targets { get; set; }
    public int TargetsWithTransform { get; set; }
    public int TargetsWithVariable { get; set; }
    public int TargetsWithListMode { get; set; }
    public int ExpressionOnlyTargets { get; set; }
    
    // Transform type counters
    public Dictionary<string, int> TransformTypes { get; } = new();
    
    // Invocation counters
    public int GroupInvocations { get; set; }
    public Dictionary<string, int> InvocationCounts { get; } = new();
    
    // List mode counters
    public Dictionary<string, int> SourceListModes { get; } = new();
    public Dictionary<string, int> TargetListModes { get; } = new();
    
    // Hidden token counters (comment usage)
    public int ElementsWithLeadingComments { get; set; }
    public int ElementsWithTrailingComments { get; set; }
    public int TotalCommentTokens { get; set; }
    public int TotalWhitespaceTokens { get; set; }

    public void AnalyzeStructureMap(FmlStructureMap map)
    {
        TotalFiles++;
        
        // Analyze metadata
        if (map.Metadata.Any())
        {
            FilesWithMetadata++;
            MetadataDeclarations += map.Metadata.Count;
        }
        
        // Analyze concept maps
        if (map.ConceptMaps.Any())
        {
            FilesWithConceptMaps++;
            ConceptMapDeclarations += map.ConceptMaps.Count;
        }
        
        // Analyze imports
        if (map.Imports.Any())
        {
            FilesWithImports++;
            ImportDeclarations += map.Imports.Count;
        }
        
        // Analyze constants
        if (map.Constants.Any())
        {
            FilesWithConstants++;
            ConstantDeclarations += map.Constants.Count;
        }
        
        // Analyze map declaration
        if (map.MapDeclaration != null)
        {
            MapDeclarations++;
        }
        
        // Analyze structures
        StructureDeclarations += map.Structures.Count;
        
        // Analyze groups
        foreach (var group in map.Groups)
        {
            AnalyzeGroup(group);
        }
        
        // Analyze hidden tokens
        AnalyzeHiddenTokens(map);
    }

    private void AnalyzeGroup(GroupDeclaration group)
    {
        Groups++;
        
        if (!string.IsNullOrEmpty(group.Extends))
        {
            GroupsWithExtends++;
        }
        
        if (group.TypeMode.HasValue)
        {
            GroupsWithTypeMode++;
        }
        
        GroupParameters += group.Parameters.Count;
        
        // Analyze parameters for hidden tokens
        foreach (var param in group.Parameters)
        {
            AnalyzeHiddenTokens(param);
        }
        
        // Analyze rules
        foreach (var rule in group.Rules)
        {
            AnalyzeRule(rule);
        }
        
        AnalyzeHiddenTokens(group);
    }

    private void AnalyzeRule(Rule rule)
    {
        TotalRules++;
        
        if (!string.IsNullOrEmpty(rule.Name))
        {
            RulesWithNames++;
        }
        
        if (rule.Sources.Count > 1)
        {
            RulesWithMultipleSources++;
        }
        
        if (rule.Targets.Count > 1)
        {
            RulesWithMultipleTargets++;
        }
        
        if (rule.Dependent != null)
        {
            RulesWithDependents++;
            
            if (rule.Dependent.Rules.Any())
            {
                RulesWithNestedRules++;
                foreach (var nestedRule in rule.Dependent.Rules)
                {
                    AnalyzeRule(nestedRule);
                }
            }
            
            AnalyzeDependent(rule.Dependent);
        }
        
        // Analyze sources
        foreach (var source in rule.Sources)
        {
            AnalyzeSource(source);
        }
        
        // Analyze targets
        foreach (var target in rule.Targets)
        {
            AnalyzeTarget(target);
        }
        
        AnalyzeHiddenTokens(rule);
    }

    private void AnalyzeSource(RuleSource source)
    {
        Sources++;
        
        if (!string.IsNullOrEmpty(source.Type))
        {
            SourcesWithType++;
        }
        
        if (source.Min.HasValue)
        {
            SourcesWithCardinality++;
        }
        
        if (!string.IsNullOrEmpty(source.DefaultValue))
        {
            SourcesWithDefault++;
        }
        
        if (source.ListMode.HasValue)
        {
            SourcesWithListMode++;
            var mode = source.ListMode.Value.ToString();
            SourceListModes[mode] = SourceListModes.GetValueOrDefault(mode) + 1;
        }
        
        if (!string.IsNullOrEmpty(source.Variable))
        {
            SourcesWithVariable++;
        }
        
        if (!string.IsNullOrEmpty(source.Condition))
        {
            SourcesWithCondition++;
        }
        
        if (!string.IsNullOrEmpty(source.Check))
        {
            SourcesWithCheck++;
        }
        
        if (!string.IsNullOrEmpty(source.Log))
        {
            SourcesWithLog++;
        }
        
        AnalyzeHiddenTokens(source);
    }

    private void AnalyzeTarget(RuleTarget target)
    {
        Targets++;
        
        // Check for expression-only target
        if (string.IsNullOrEmpty(target.Context) && target.Transform != null)
        {
            ExpressionOnlyTargets++;
        }
        
        if (target.Transform != null)
        {
            TargetsWithTransform++;
            var transformType = target.Transform.Type;
            TransformTypes[transformType] = TransformTypes.GetValueOrDefault(transformType) + 1;
        }
        
        if (!string.IsNullOrEmpty(target.Variable))
        {
            TargetsWithVariable++;
        }
        
        if (target.ListMode.HasValue)
        {
            TargetsWithListMode++;
            var mode = target.ListMode.Value.ToString();
            TargetListModes[mode] = TargetListModes.GetValueOrDefault(mode) + 1;
        }
        
        AnalyzeHiddenTokens(target);
    }

    private void AnalyzeDependent(RuleDependent dependent)
    {
        foreach (var invocation in dependent.Invocations)
        {
            GroupInvocations++;
            var name = invocation.Name;
            InvocationCounts[name] = InvocationCounts.GetValueOrDefault(name) + 1;
            AnalyzeHiddenTokens(invocation);
        }
        
        AnalyzeHiddenTokens(dependent);
    }

    private void AnalyzeHiddenTokens(FmlNode node)
    {
        if (node.LeadingHiddenTokens != null && node.LeadingHiddenTokens.Any())
        {
            var hasComments = node.LeadingHiddenTokens.Any(t => t.IsComment);
            if (hasComments)
            {
                ElementsWithLeadingComments++;
            }
            
            TotalCommentTokens += node.LeadingHiddenTokens.Count(t => t.IsComment);
            TotalWhitespaceTokens += node.LeadingHiddenTokens.Count(t => t.IsWhitespace);
        }
        
        if (node.TrailingHiddenTokens != null && node.TrailingHiddenTokens.Any())
        {
            var hasComments = node.TrailingHiddenTokens.Any(t => t.IsComment);
            if (hasComments)
            {
                ElementsWithTrailingComments++;
            }
            
            TotalCommentTokens += node.TrailingHiddenTokens.Count(t => t.IsComment);
            TotalWhitespaceTokens += node.TrailingHiddenTokens.Count(t => t.IsWhitespace);
        }
    }

    public string GenerateReport(int totalFiles, List<string> failures)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# FML Feature Usage Analytics - R4toR5 Conversion Maps");
        sb.AppendLine();
        sb.AppendLine($"**Analysis Date**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Total Files Analyzed**: {totalFiles}");
        sb.AppendLine($"**Successfully Parsed**: {totalFiles - failures.Count} ({((double)(totalFiles - failures.Count) / totalFiles * 100):F1}%)");
        sb.AppendLine($"**Parse Failures**: {failures.Count}");
        sb.AppendLine();
        
        // File-level statistics
        sb.AppendLine("## File-Level Statistics");
        sb.AppendLine();
        sb.AppendLine("| Feature | Count | Percentage |");
        sb.AppendLine("|---------|-------|------------|");
        sb.AppendLine($"| Files with Metadata | {FilesWithMetadata} | {Percentage(FilesWithMetadata, TotalFiles)} |");
        sb.AppendLine($"| Files with Imports | {FilesWithImports} | {Percentage(FilesWithImports, TotalFiles)} |");
        sb.AppendLine($"| Files with Constants | {FilesWithConstants} | {Percentage(FilesWithConstants, TotalFiles)} |");
        sb.AppendLine($"| Files with ConceptMaps | {FilesWithConceptMaps} | {Percentage(FilesWithConceptMaps, TotalFiles)} |");
        sb.AppendLine();
        
        // Declaration statistics
        sb.AppendLine("## Declaration Statistics");
        sb.AppendLine();
        sb.AppendLine("| Declaration Type | Total Count | Avg per File |");
        sb.AppendLine("|------------------|-------------|--------------|");
        sb.AppendLine($"| Metadata | {MetadataDeclarations} | {Average(MetadataDeclarations, TotalFiles)} |");
        sb.AppendLine($"| Map | {MapDeclarations} | {Average(MapDeclarations, TotalFiles)} |");
        sb.AppendLine($"| Structure (uses) | {StructureDeclarations} | {Average(StructureDeclarations, TotalFiles)} |");
        sb.AppendLine($"| Import | {ImportDeclarations} | {Average(ImportDeclarations, TotalFiles)} |");
        sb.AppendLine($"| Constant (let) | {ConstantDeclarations} | {Average(ConstantDeclarations, TotalFiles)} |");
        sb.AppendLine($"| ConceptMap | {ConceptMapDeclarations} | {Average(ConceptMapDeclarations, TotalFiles)} |");
        sb.AppendLine();
        
        // Group statistics
        sb.AppendLine("## Group Statistics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Count | Notes |");
        sb.AppendLine("|--------|-------|-------|");
        sb.AppendLine($"| Total Groups | {Groups} | {Average(Groups, TotalFiles)} per file |");
        sb.AppendLine($"| Groups with 'extends' | {GroupsWithExtends} | {Percentage(GroupsWithExtends, Groups)} |");
        sb.AppendLine($"| Groups with TypeMode | {GroupsWithTypeMode} | {Percentage(GroupsWithTypeMode, Groups)} |");
        sb.AppendLine($"| Total Parameters | {GroupParameters} | {Average(GroupParameters, Groups)} per group |");
        sb.AppendLine();
        
        // Rule statistics
        sb.AppendLine("## Rule Statistics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Count | Percentage |");
        sb.AppendLine("|--------|-------|------------|");
        sb.AppendLine($"| Total Rules | {TotalRules} | - |");
        sb.AppendLine($"| Rules with Names | {RulesWithNames} | {Percentage(RulesWithNames, TotalRules)} |");
        sb.AppendLine($"| Rules with Multiple Sources | {RulesWithMultipleSources} | {Percentage(RulesWithMultipleSources, TotalRules)} |");
        sb.AppendLine($"| Rules with Multiple Targets | {RulesWithMultipleTargets} | {Percentage(RulesWithMultipleTargets, TotalRules)} |");
        sb.AppendLine($"| Rules with Dependents | {RulesWithDependents} | {Percentage(RulesWithDependents, TotalRules)} |");
        sb.AppendLine($"| Rules with Nested Rules | {RulesWithNestedRules} | {Percentage(RulesWithNestedRules, TotalRules)} |");
        sb.AppendLine();
        
        // Source statistics
        sb.AppendLine("## Source Element Statistics");
        sb.AppendLine();
        sb.AppendLine("| Feature | Count | Percentage |");
        sb.AppendLine("|---------|-------|------------|");
        sb.AppendLine($"| Total Sources | {Sources} | - |");
        sb.AppendLine($"| Sources with Type | {SourcesWithType} | {Percentage(SourcesWithType, Sources)} |");
        sb.AppendLine($"| Sources with Cardinality | {SourcesWithCardinality} | {Percentage(SourcesWithCardinality, Sources)} |");
        sb.AppendLine($"| Sources with Default | {SourcesWithDefault} | {Percentage(SourcesWithDefault, Sources)} |");
        sb.AppendLine($"| Sources with ListMode | {SourcesWithListMode} | {Percentage(SourcesWithListMode, Sources)} |");
        sb.AppendLine($"| Sources with Variable | {SourcesWithVariable} | {Percentage(SourcesWithVariable, Sources)} |");
        sb.AppendLine($"| Sources with Condition (where) | {SourcesWithCondition} | {Percentage(SourcesWithCondition, Sources)} |");
        sb.AppendLine($"| Sources with Check | {SourcesWithCheck} | {Percentage(SourcesWithCheck, Sources)} |");
        sb.AppendLine($"| Sources with Log | {SourcesWithLog} | {Percentage(SourcesWithLog, Sources)} |");
        sb.AppendLine();
        
        // Source list modes
        if (SourceListModes.Any())
        {
            sb.AppendLine("### Source List Mode Distribution");
            sb.AppendLine();
            sb.AppendLine("| Mode | Count | Percentage |");
            sb.AppendLine("|------|-------|------------|");
            foreach (var mode in SourceListModes.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"| {mode.Key} | {mode.Value} | {Percentage(mode.Value, SourcesWithListMode)} |");
            }
            sb.AppendLine();
        }
        
        // Target statistics
        sb.AppendLine("## Target Element Statistics");
        sb.AppendLine();
        sb.AppendLine("| Feature | Count | Percentage |");
        sb.AppendLine("|---------|-------|------------|");
        sb.AppendLine($"| Total Targets | {Targets} | - |");
        sb.AppendLine($"| Targets with Transform | {TargetsWithTransform} | {Percentage(TargetsWithTransform, Targets)} |");
        sb.AppendLine($"| Targets with Variable | {TargetsWithVariable} | {Percentage(TargetsWithVariable, Targets)} |");
        sb.AppendLine($"| Targets with ListMode | {TargetsWithListMode} | {Percentage(TargetsWithListMode, Targets)} |");
        sb.AppendLine($"| Expression-only Targets | {ExpressionOnlyTargets} | {Percentage(ExpressionOnlyTargets, Targets)} |");
        sb.AppendLine();
        
        // Target list modes
        if (TargetListModes.Any())
        {
            sb.AppendLine("### Target List Mode Distribution");
            sb.AppendLine();
            sb.AppendLine("| Mode | Count | Percentage |");
            sb.AppendLine("|------|-------|------------|");
            foreach (var mode in TargetListModes.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"| {mode.Key} | {mode.Value} | {Percentage(mode.Value, TargetsWithListMode)} |");
            }
            sb.AppendLine();
        }
        
        // Transform type distribution
        if (TransformTypes.Any())
        {
            sb.AppendLine("## Transform Type Distribution");
            sb.AppendLine();
            sb.AppendLine("| Transform Type | Count | Percentage |");
            sb.AppendLine("|----------------|-------|------------|");
            foreach (var transform in TransformTypes.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"| {transform.Key} | {transform.Value} | {Percentage(transform.Value, TargetsWithTransform)} |");
            }
            sb.AppendLine();
        }
        
        // Group invocation statistics
        sb.AppendLine("## Group Invocation Statistics");
        sb.AppendLine();
        sb.AppendLine($"**Total Invocations**: {GroupInvocations}");
        sb.AppendLine($"**Unique Groups Called**: {InvocationCounts.Count}");
        sb.AppendLine();
        
        if (InvocationCounts.Any())
        {
            sb.AppendLine("### Most Frequently Called Groups");
            sb.AppendLine();
            sb.AppendLine("| Group Name | Call Count |");
            sb.AppendLine("|------------|------------|");
            foreach (var inv in InvocationCounts.OrderByDescending(x => x.Value).Take(20))
            {
                sb.AppendLine($"| {inv.Key} | {inv.Value} |");
            }
            sb.AppendLine();
        }
        
        // Comment usage statistics
        sb.AppendLine("## Comment Usage Statistics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Count |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Elements with Leading Comments | {ElementsWithLeadingComments} |");
        sb.AppendLine($"| Elements with Trailing Comments | {ElementsWithTrailingComments} |");
        sb.AppendLine($"| Total Comment Tokens | {TotalCommentTokens} |");
        sb.AppendLine($"| Total Whitespace Tokens | {TotalWhitespaceTokens} |");
        sb.AppendLine();
        
        // Key insights
        sb.AppendLine("## Key Insights");
        sb.AppendLine();
        sb.AppendLine($"- **Most Common Transform**: {TransformTypes.OrderByDescending(x => x.Value).FirstOrDefault().Key ?? "N/A"}");
        sb.AppendLine($"- **Average Rules per Group**: {Average(TotalRules, Groups)}");
        sb.AppendLine($"- **Average Sources per Rule**: {Average(Sources, TotalRules)}");
        sb.AppendLine($"- **Average Targets per Rule**: {Average(Targets, TotalRules)}");
        sb.AppendLine($"- **Condition Usage Rate**: {Percentage(SourcesWithCondition, Sources)} of sources use 'where' clauses");
        sb.AppendLine($"- **Variable Assignment Rate**: {Percentage(SourcesWithVariable, Sources)} of sources assign variables");
        sb.AppendLine($"- **Comment Usage Rate**: {Percentage(ElementsWithLeadingComments + ElementsWithTrailingComments, TotalRules + Sources + Targets + Groups)} of major elements have comments");
        sb.AppendLine();
        
        if (failures.Any())
        {
            sb.AppendLine("## Parse Failures");
            sb.AppendLine();
            sb.AppendLine("The following files failed to parse:");
            sb.AppendLine();
            foreach (var failure in failures.Take(20))
            {
                sb.AppendLine($"- {failure}");
            }
            if (failures.Count > 20)
            {
                sb.AppendLine($"- ... and {failures.Count - 20} more");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("---");
        sb.AppendLine($"*Generated by FML Feature Analytics - {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
        
        return sb.ToString();
    }

    private string Percentage(int value, int total)
    {
        if (total == 0) return "0.0%";
        return $"{(double)value / total * 100:F1}%";
    }

    private string Average(int value, int count)
    {
        if (count == 0) return "0.0";
        return $"{(double)value / count:F1}";
    }
}
