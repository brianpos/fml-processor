using fsh_processor;
using fsh_processor.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace fsh_tester;

/// <summary>
/// Analytics tests to measure real-world FSH feature usage across SDC IG
/// </summary>
[TestClass]
public class FshFeatureAnalyticsTests
{
    [TestMethod]
    public void AnalyzeSDCIgFeatureUsage()
    {
        // Path to SDC IG FSH files
        var fshDirectory = @"C:\git\hl7\sdc\input\fsh";
        
        if (!Directory.Exists(fshDirectory))
        {
            Assert.Inconclusive($"Directory not found: {fshDirectory}. This test requires the SDC IG repository.");
            return;
        }

        var fshFiles = Directory.GetFiles(fshDirectory, "*.fsh", SearchOption.AllDirectories);
        
        Assert.IsTrue(fshFiles.Length > 0, 
            $"No FSH files found in {fshDirectory}");

        // Initialize counters
        var analytics = new FshFeatureAnalytics();
        var parseFailures = new List<string>();

        // Parse all files and collect analytics
        foreach (var fshFile in fshFiles)
        {
            try
            {
                var fshText = File.ReadAllText(fshFile);
                var result = FshParser.Parse(fshText);

                if (result is ParseResult.Success success)
                {
                    analytics.AnalyzeDocument(success.Document);
                }
                else if (result is ParseResult.Failure failure)
                {
                    parseFailures.Add(Path.GetFileName(fshFile));
                }
            }
            catch (Exception ex)
            {
                parseFailures.Add($"{Path.GetFileName(fshFile)}: {ex.Message}");
            }
        }

        // Generate report
        var report = analytics.GenerateReport(fshFiles.Length, parseFailures);
        
        Console.WriteLine(report);

        // Also write to file for easy reference
        var reportPath = Path.Combine(Path.GetTempPath(), "fsh-feature-analytics.md");
        File.WriteAllText(reportPath, report);
        Console.WriteLine($"\nReport saved to: {reportPath}");

        // Assert we parsed most files successfully
        var successRate = ((double)(fshFiles.Length - parseFailures.Count) / fshFiles.Length) * 100;
        Assert.IsTrue(successRate >= 90, 
            $"Success rate {successRate:F1}% is below 90%. Failed files: {string.Join(", ", parseFailures.Take(5))}");
    }
}

/// <summary>
/// Collects analytics about FSH feature usage
/// </summary>
public class FshFeatureAnalytics
{
    // File-level counters
    public int TotalFiles { get; set; }
    public int FilesWithProfiles { get; set; }
    public int FilesWithExtensions { get; set; }
    public int FilesWithInstances { get; set; }
    public int FilesWithValueSets { get; set; }
    public int FilesWithCodeSystems { get; set; }
    public int FilesWithRuleSets { get; set; }
    
    // Entity counters
    public int Aliases { get; set; }
    public int Profiles { get; set; }
    public int Extensions { get; set; }
    public int Logicals { get; set; }
    public int Resources { get; set; }
    public int Instances { get; set; }
    public int Invariants { get; set; }
    public int ValueSets { get; set; }
    public int CodeSystems { get; set; }
    public int RuleSets { get; set; }
    public int ParameterizedRuleSets { get; set; }
    public int Mappings { get; set; }
    
    // Rule counters (SD rules)
    public int CardRules { get; set; }
    public int FlagRules { get; set; }
    public int ValueSetRules { get; set; }
    public int FixedValueRules { get; set; }
    public int ContainsRules { get; set; }
    public int OnlyRules { get; set; }
    public int ObeysRules { get; set; }
    public int CaretValueRules { get; set; }
    public int InsertRules { get; set; }
    public int PathRules { get; set; }
    
    // LR rule counters
    public int LrCardRules { get; set; }
    public int LrFlagRules { get; set; }
    public int AddElementRules { get; set; }
    public int AddCRElementRules { get; set; }
    
    // Instance rule counters
    public int InstanceFixedValueRules { get; set; }
    public int InstanceInsertRules { get; set; }
    public int InstancePathRules { get; set; }
    
    // Invariant rule counters
    public int InvariantFixedValueRules { get; set; }
    public int InvariantInsertRules { get; set; }
    public int InvariantPathRules { get; set; }
    
    // ValueSet rule counters
    public int VsComponentRules { get; set; }
    public int VsCaretValueRules { get; set; }
    public int CodeCaretValueRules { get; set; }
    public int VsInsertRules { get; set; }
    public int CodeInsertRules { get; set; }
    
    // CodeSystem rule counters
    public int Concepts { get; set; }
    public int CsCaretValueRules { get; set; }
    public int CsInsertRules { get; set; }
    
    // Mapping rule counters
    public int MappingMapRules { get; set; }
    public int MappingInsertRules { get; set; }
    public int MappingPathRules { get; set; }
    
    // Extension context counters
    public Dictionary<string, int> ExtensionContexts { get; } = new();
    
    // Logical characteristics counters
    public Dictionary<string, int> LogicalCharacteristics { get; } = new();
    
    // Flag usage counters
    public Dictionary<string, int> FlagTypes { get; } = new();
    
    // ValueSet binding strength counters
    public Dictionary<string, int> BindingStrengths { get; } = new();
    
    // Insert rule usage (which rulesets are inserted)
    public Dictionary<string, int> InsertedRuleSets { get; } = new();
    
    // Value type counters
    public Dictionary<string, int> ValueTypes { get; } = new();
    
    // Hidden token counters (comment usage)
    public int ElementsWithLeadingComments { get; set; }
    public int ElementsWithTrailingComments { get; set; }
    public int TotalCommentTokens { get; set; }
    public int TotalWhitespaceTokens { get; set; }
    
    // Metadata counters
    public int EntitiesWithId { get; set; }
    public int EntitiesWithTitle { get; set; }
    public int EntitiesWithDescription { get; set; }

    public void AnalyzeDocument(FshDoc document)
    {
        TotalFiles++;
        
        // Track which entity types are in this file
        var hasProfiles = false;
        var hasExtensions = false;
        var hasInstances = false;
        var hasValueSets = false;
        var hasCodeSystems = false;
        var hasRuleSets = false;
        
        // Analyze each entity
        foreach (var entity in document.Entities)
        {
            AnalyzeHiddenTokens(entity);
            
            switch (entity)
            {
                case Alias alias:
                    Aliases++;
                    break;
                    
                case Profile profile:
                    Profiles++;
                    hasProfiles = true;
                    AnalyzeProfile(profile);
                    break;
                    
                case Extension extension:
                    Extensions++;
                    hasExtensions = true;
                    AnalyzeExtension(extension);
                    break;
                    
                case Logical logical:
                    Logicals++;
                    AnalyzeLogical(logical);
                    break;
                    
                case Resource resource:
                    Resources++;
                    AnalyzeResource(resource);
                    break;
                    
                case Instance instance:
                    Instances++;
                    hasInstances = true;
                    AnalyzeInstance(instance);
                    break;
                    
                case Invariant invariant:
                    Invariants++;
                    AnalyzeInvariant(invariant);
                    break;
                    
                case ValueSet valueSet:
                    ValueSets++;
                    hasValueSets = true;
                    AnalyzeValueSet(valueSet);
                    break;
                    
                case CodeSystem codeSystem:
                    CodeSystems++;
                    hasCodeSystems = true;
                    AnalyzeCodeSystem(codeSystem);
                    break;
                    
                case RuleSet ruleSet:
                    RuleSets++;
                    hasRuleSets = true;
                    if (ruleSet.IsParameterized)
                    {
                        ParameterizedRuleSets++;
                    }
                    AnalyzeRuleSet(ruleSet);
                    break;
                    
                case Mapping mapping:
                    Mappings++;
                    AnalyzeMapping(mapping);
                    break;
            }
        }
        
        // Update file-level counters
        if (hasProfiles) FilesWithProfiles++;
        if (hasExtensions) FilesWithExtensions++;
        if (hasInstances) FilesWithInstances++;
        if (hasValueSets) FilesWithValueSets++;
        if (hasCodeSystems) FilesWithCodeSystems++;
        if (hasRuleSets) FilesWithRuleSets++;
    }

    private void AnalyzeProfile(Profile profile)
    {
        AnalyzeMetadata(profile);
        
        foreach (var rule in profile.Rules)
        {
            AnalyzeSdRule(rule);
        }
    }

    private void AnalyzeExtension(Extension extension)
    {
        AnalyzeMetadata(extension);
        
        // Track context types
        foreach (var context in extension.Contexts)
        {
            var contextValue = context.IsQuoted ? $"\"{context.Value}\"" : context.Value;
            ExtensionContexts[contextValue] = ExtensionContexts.GetValueOrDefault(contextValue) + 1;
        }
        
        foreach (var rule in extension.Rules)
        {
            AnalyzeSdRule(rule);
        }
    }

    private void AnalyzeLogical(Logical logical)
    {
        AnalyzeMetadata(logical);
        
        // Track characteristics
        foreach (var characteristic in logical.Characteristics)
        {
            LogicalCharacteristics[characteristic] = LogicalCharacteristics.GetValueOrDefault(characteristic) + 1;
        }
        
        foreach (var rule in logical.Rules)
        {
            AnalyzeLrRule(rule);
        }
    }

    private void AnalyzeResource(Resource resource)
    {
        AnalyzeMetadata(resource);
        
        foreach (var rule in resource.Rules)
        {
            AnalyzeLrRule(rule);
        }
    }

    private void AnalyzeInstance(Instance instance)
    {
        AnalyzeMetadata(instance);
        
        foreach (var rule in instance.Rules)
        {
            AnalyzeHiddenTokens(rule);
            
            switch (rule)
            {
                case InstanceFixedValueRule fixedValue:
                    InstanceFixedValueRules++;
                    AnalyzeValue(fixedValue.Value);
                    break;
                case InstanceInsertRule insert:
                    InstanceInsertRules++;
                    InsertedRuleSets[insert.RuleSetReference] = InsertedRuleSets.GetValueOrDefault(insert.RuleSetReference) + 1;
                    break;
                case InstancePathRule path:
                    InstancePathRules++;
                    break;
            }
        }
    }

    private void AnalyzeInvariant(Invariant invariant)
    {
        AnalyzeMetadata(invariant);
        
        foreach (var rule in invariant.Rules)
        {
            AnalyzeHiddenTokens(rule);
            
            switch (rule)
            {
                case InvariantFixedValueRule fixedValue:
                    InvariantFixedValueRules++;
                    AnalyzeValue(fixedValue.Value);
                    break;
                case InvariantInsertRule insert:
                    InvariantInsertRules++;
                    InsertedRuleSets[insert.RuleSetReference] = InsertedRuleSets.GetValueOrDefault(insert.RuleSetReference) + 1;
                    break;
                case InvariantPathRule path:
                    InvariantPathRules++;
                    break;
            }
        }
    }

    private void AnalyzeValueSet(ValueSet valueSet)
    {
        AnalyzeMetadata(valueSet);
        
        foreach (var rule in valueSet.Rules)
        {
            AnalyzeHiddenTokens(rule);
            
            switch (rule)
            {
                case VsComponentRule component:
                    VsComponentRules++;
                    break;
                case VsCaretValueRule caret:
                    VsCaretValueRules++;
                    AnalyzeValue(caret.Value);
                    break;
                case CodeCaretValueRule codeCaret:
                    CodeCaretValueRules++;
                    AnalyzeValue(codeCaret.Value);
                    break;
                case VsInsertRule insert:
                    VsInsertRules++;
                    InsertedRuleSets[insert.RuleSetReference] = InsertedRuleSets.GetValueOrDefault(insert.RuleSetReference) + 1;
                    break;
                case CodeInsertRule codeInsert:
                    CodeInsertRules++;
                    InsertedRuleSets[codeInsert.RuleSetReference] = InsertedRuleSets.GetValueOrDefault(codeInsert.RuleSetReference) + 1;
                    break;
            }
        }
    }

    private void AnalyzeCodeSystem(CodeSystem codeSystem)
    {
        AnalyzeMetadata(codeSystem);
        
        foreach (var rule in codeSystem.Rules)
        {
            AnalyzeHiddenTokens(rule);
            
            switch (rule)
            {
                case Concept concept:
                    Concepts++;
                    break;
                case CsCaretValueRule caret:
                    CsCaretValueRules++;
                    AnalyzeValue(caret.Value);
                    break;
                case CsInsertRule insert:
                    CsInsertRules++;
                    InsertedRuleSets[insert.RuleSetReference] = InsertedRuleSets.GetValueOrDefault(insert.RuleSetReference) + 1;
                    break;
            }
        }
    }

    private void AnalyzeRuleSet(RuleSet ruleSet)
    {
        AnalyzeHiddenTokens(ruleSet);
        
        // RuleSets contain rules directly
        foreach (var rule in ruleSet.Rules)
        {
            AnalyzeHiddenTokens(rule);
            // RuleSet rules are RuleSetRule type which is just a wrapper
            // The actual rules are stored in the Rules collection
            AnalyzeSdRule(rule);
        }
    }

    private void AnalyzeMapping(Mapping mapping)
    {
        AnalyzeMetadata(mapping);
        
        foreach (var rule in mapping.Rules)
        {
            AnalyzeHiddenTokens(rule);
            
            switch (rule)
            {
                case MappingMapRule map:
                    MappingMapRules++;
                    break;
                case MappingInsertRule insert:
                    MappingInsertRules++;
                    InsertedRuleSets[insert.RuleSetReference] = InsertedRuleSets.GetValueOrDefault(insert.RuleSetReference) + 1;
                    break;
                case MappingPathRule path:
                    MappingPathRules++;
                    break;
            }
        }
    }

    private void AnalyzeSdRule(FshRule rule)
    {
        AnalyzeHiddenTokens(rule);
        
        switch (rule)
        {
            case CardRule card:
                CardRules++;
                break;
            case FlagRule flag:
                FlagRules++;
                foreach (var flagValue in flag.Flags)
                {
                    FlagTypes[flagValue] = FlagTypes.GetValueOrDefault(flagValue) + 1;
                }
                break;
            case ValueSetRule valueSet:
                ValueSetRules++;
                if (valueSet.Strength != null)
                {
                    BindingStrengths[valueSet.Strength] = BindingStrengths.GetValueOrDefault(valueSet.Strength) + 1;
                }
                break;
            case FixedValueRule fixedValue:
                FixedValueRules++;
                AnalyzeValue(fixedValue.Value);
                break;
            case ContainsRule contains:
                ContainsRules++;
                break;
            case OnlyRule only:
                OnlyRules++;
                break;
            case ObeysRule obeys:
                ObeysRules++;
                break;
            case CaretValueRule caret:
                CaretValueRules++;
                AnalyzeValue(caret.Value);
                break;
            case InsertRule insert:
                InsertRules++;
                InsertedRuleSets[insert.RuleSetReference] = InsertedRuleSets.GetValueOrDefault(insert.RuleSetReference) + 1;
                break;
            case PathRule path:
                PathRules++;
                break;
        }
    }

    private void AnalyzeLrRule(FshRule rule)
    {
        AnalyzeHiddenTokens(rule);
        
        switch (rule)
        {
            case LrCardRule card:
                LrCardRules++;
                break;
            case LrFlagRule flag:
                LrFlagRules++;
                foreach (var flagValue in flag.Flags)
                {
                    FlagTypes[flagValue] = FlagTypes.GetValueOrDefault(flagValue) + 1;
                }
                break;
            case AddElementRule addElement:
                AddElementRules++;
                break;
            case AddCRElementRule addCr:
                AddCRElementRules++;
                break;
            default:
                // LR entities can also have SD rules
                AnalyzeSdRule(rule);
                break;
        }
    }

    private void AnalyzeValue(FshValue? value)
    {
        if (value == null) return;
        
        var typeName = value.GetType().Name;
        ValueTypes[typeName] = ValueTypes.GetValueOrDefault(typeName) + 1;
    }

    private void AnalyzeMetadata(FshEntity entity)
    {
        // Check for Id, Title, Description based on entity type
        switch (entity)
        {
            case Profile p:
                if (!string.IsNullOrEmpty(p.Id?.Value)) EntitiesWithId++;
                if (!string.IsNullOrEmpty(p.Title?.Value)) EntitiesWithTitle++;
                if (!string.IsNullOrEmpty(p.Description?.Value)) EntitiesWithDescription++;
                break;
            case Extension e:
                if (!string.IsNullOrEmpty(e.Id)) EntitiesWithId++;
                if (!string.IsNullOrEmpty(e.Title)) EntitiesWithTitle++;
                if (!string.IsNullOrEmpty(e.Description)) EntitiesWithDescription++;
                break;
            case Logical l:
                if (!string.IsNullOrEmpty(l.Id)) EntitiesWithId++;
                if (!string.IsNullOrEmpty(l.Title)) EntitiesWithTitle++;
                if (!string.IsNullOrEmpty(l.Description)) EntitiesWithDescription++;
                break;
            case Resource r:
                if (!string.IsNullOrEmpty(r.Id)) EntitiesWithId++;
                if (!string.IsNullOrEmpty(r.Title)) EntitiesWithTitle++;
                if (!string.IsNullOrEmpty(r.Description)) EntitiesWithDescription++;
                break;
            case Instance i:
                if (!string.IsNullOrEmpty(i.Title)) EntitiesWithTitle++;
                if (!string.IsNullOrEmpty(i.Description)) EntitiesWithDescription++;
                break;
            case Invariant inv:
                if (!string.IsNullOrEmpty(inv.Description)) EntitiesWithDescription++;
                break;
            case ValueSet vs:
                if (!string.IsNullOrEmpty(vs.Id)) EntitiesWithId++;
                if (!string.IsNullOrEmpty(vs.Title)) EntitiesWithTitle++;
                if (!string.IsNullOrEmpty(vs.Description)) EntitiesWithDescription++;
                break;
            case CodeSystem cs:
                if (!string.IsNullOrEmpty(cs.Id)) EntitiesWithId++;
                if (!string.IsNullOrEmpty(cs.Title)) EntitiesWithTitle++;
                if (!string.IsNullOrEmpty(cs.Description)) EntitiesWithDescription++;
                break;
            case Mapping m:
                if (!string.IsNullOrEmpty(m.Id)) EntitiesWithId++;
                if (!string.IsNullOrEmpty(m.Title)) EntitiesWithTitle++;
                if (!string.IsNullOrEmpty(m.Description)) EntitiesWithDescription++;
                break;
        }
    }

    private void AnalyzeHiddenTokens(FshNode node)
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
        
        sb.AppendLine("# FSH Feature Usage Analytics - SDC Implementation Guide");
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
        sb.AppendLine($"| Files with Profiles | {FilesWithProfiles} | {Percentage(FilesWithProfiles, TotalFiles)} |");
        sb.AppendLine($"| Files with Extensions | {FilesWithExtensions} | {Percentage(FilesWithExtensions, TotalFiles)} |");
        sb.AppendLine($"| Files with Instances | {FilesWithInstances} | {Percentage(FilesWithInstances, TotalFiles)} |");
        sb.AppendLine($"| Files with ValueSets | {FilesWithValueSets} | {Percentage(FilesWithValueSets, TotalFiles)} |");
        sb.AppendLine($"| Files with CodeSystems | {FilesWithCodeSystems} | {Percentage(FilesWithCodeSystems, TotalFiles)} |");
        sb.AppendLine($"| Files with RuleSets | {FilesWithRuleSets} | {Percentage(FilesWithRuleSets, TotalFiles)} |");
        sb.AppendLine();
        
        // Entity statistics
        sb.AppendLine("## Entity Statistics");
        sb.AppendLine();
        sb.AppendLine("| Entity Type | Total Count | Avg per File |");
        sb.AppendLine("|-------------|-------------|--------------|");
        sb.AppendLine($"| Alias | {Aliases} | {Average(Aliases, TotalFiles)} |");
        sb.AppendLine($"| Profile | {Profiles} | {Average(Profiles, TotalFiles)} |");
        sb.AppendLine($"| Extension | {Extensions} | {Average(Extensions, TotalFiles)} |");
        sb.AppendLine($"| Logical | {Logicals} | {Average(Logicals, TotalFiles)} |");
        sb.AppendLine($"| Resource | {Resources} | {Average(Resources, TotalFiles)} |");
        sb.AppendLine($"| Instance | {Instances} | {Average(Instances, TotalFiles)} |");
        sb.AppendLine($"| Invariant | {Invariants} | {Average(Invariants, TotalFiles)} |");
        sb.AppendLine($"| ValueSet | {ValueSets} | {Average(ValueSets, TotalFiles)} |");
        sb.AppendLine($"| CodeSystem | {CodeSystems} | {Average(CodeSystems, TotalFiles)} |");
        sb.AppendLine($"| RuleSet | {RuleSets} | {Average(RuleSets, TotalFiles)} |");
        sb.AppendLine($"| - Parameterized | {ParameterizedRuleSets} | {Percentage(ParameterizedRuleSets, RuleSets)} |");
        sb.AppendLine($"| Mapping | {Mappings} | {Average(Mappings, TotalFiles)} |");
        sb.AppendLine();
        
        // SD Rule statistics
        var totalSdRules = CardRules + FlagRules + ValueSetRules + FixedValueRules + ContainsRules +
                          OnlyRules + ObeysRules + CaretValueRules + InsertRules + PathRules;
        
        sb.AppendLine("## SD Rule Statistics (Profile/Extension)");
        sb.AppendLine();
        sb.AppendLine("| Rule Type | Count | Percentage |");
        sb.AppendLine("|-----------|-------|------------|");
        sb.AppendLine($"| Total SD Rules | {totalSdRules} | - |");
        sb.AppendLine($"| CardRule | {CardRules} | {Percentage(CardRules, totalSdRules)} |");
        sb.AppendLine($"| FlagRule | {FlagRules} | {Percentage(FlagRules, totalSdRules)} |");
        sb.AppendLine($"| ValueSetRule | {ValueSetRules} | {Percentage(ValueSetRules, totalSdRules)} |");
        sb.AppendLine($"| FixedValueRule | {FixedValueRules} | {Percentage(FixedValueRules, totalSdRules)} |");
        sb.AppendLine($"| ContainsRule | {ContainsRules} | {Percentage(ContainsRules, totalSdRules)} |");
        sb.AppendLine($"| OnlyRule | {OnlyRules} | {Percentage(OnlyRules, totalSdRules)} |");
        sb.AppendLine($"| ObeysRule | {ObeysRules} | {Percentage(ObeysRules, totalSdRules)} |");
        sb.AppendLine($"| CaretValueRule | {CaretValueRules} | {Percentage(CaretValueRules, totalSdRules)} |");
        sb.AppendLine($"| InsertRule | {InsertRules} | {Percentage(InsertRules, totalSdRules)} |");
        sb.AppendLine($"| PathRule | {PathRules} | {Percentage(PathRules, totalSdRules)} |");
        sb.AppendLine();
        
        // LR Rule statistics
        if (Logicals > 0 || Resources > 0)
        {
            var totalLrRules = LrCardRules + LrFlagRules + AddElementRules + AddCRElementRules;
            
            sb.AppendLine("## LR Rule Statistics (Logical/Resource)");
            sb.AppendLine();
            sb.AppendLine("| Rule Type | Count | Percentage |");
            sb.AppendLine("|-----------|-------|------------|");
            sb.AppendLine($"| Total LR Rules | {totalLrRules} | - |");
            sb.AppendLine($"| LrCardRule | {LrCardRules} | {Percentage(LrCardRules, totalLrRules)} |");
            sb.AppendLine($"| LrFlagRule | {LrFlagRules} | {Percentage(LrFlagRules, totalLrRules)} |");
            sb.AppendLine($"| AddElementRule | {AddElementRules} | {Percentage(AddElementRules, totalLrRules)} |");
            sb.AppendLine($"| AddCRElementRule | {AddCRElementRules} | {Percentage(AddCRElementRules, totalLrRules)} |");
            sb.AppendLine();
        }
        
        // Instance Rule statistics
        if (Instances > 0)
        {
            var totalInstanceRules = InstanceFixedValueRules + InstanceInsertRules + InstancePathRules;
            
            sb.AppendLine("## Instance Rule Statistics");
            sb.AppendLine();
            sb.AppendLine("| Rule Type | Count | Percentage |");
            sb.AppendLine("|-----------|-------|------------|");
            sb.AppendLine($"| Total Instance Rules | {totalInstanceRules} | - |");
            sb.AppendLine($"| FixedValueRule | {InstanceFixedValueRules} | {Percentage(InstanceFixedValueRules, totalInstanceRules)} |");
            sb.AppendLine($"| InsertRule | {InstanceInsertRules} | {Percentage(InstanceInsertRules, totalInstanceRules)} |");
            sb.AppendLine($"| PathRule | {InstancePathRules} | {Percentage(InstancePathRules, totalInstanceRules)} |");
            sb.AppendLine();
        }
        
        // ValueSet Rule statistics
        if (ValueSets > 0)
        {
            var totalVsRules = VsComponentRules + VsCaretValueRules + CodeCaretValueRules + VsInsertRules + CodeInsertRules;
            
            sb.AppendLine("## ValueSet Rule Statistics");
            sb.AppendLine();
            sb.AppendLine("| Rule Type | Count | Percentage |");
            sb.AppendLine("|-----------|-------|------------|");
            sb.AppendLine($"| Total VS Rules | {totalVsRules} | - |");
            sb.AppendLine($"| VsComponentRule | {VsComponentRules} | {Percentage(VsComponentRules, totalVsRules)} |");
            sb.AppendLine($"| VsCaretValueRule | {VsCaretValueRules} | {Percentage(VsCaretValueRules, totalVsRules)} |");
            sb.AppendLine($"| CodeCaretValueRule | {CodeCaretValueRules} | {Percentage(CodeCaretValueRules, totalVsRules)} |");
            sb.AppendLine($"| VsInsertRule | {VsInsertRules} | {Percentage(VsInsertRules, totalVsRules)} |");
            sb.AppendLine($"| CodeInsertRule | {CodeInsertRules} | {Percentage(CodeInsertRules, totalVsRules)} |");
            sb.AppendLine();
        }
        
        // CodeSystem Rule statistics
        if (CodeSystems > 0)
        {
            var totalCsRules = Concepts + CsCaretValueRules + CsInsertRules;
            
            sb.AppendLine("## CodeSystem Rule Statistics");
            sb.AppendLine();
            sb.AppendLine("| Rule Type | Count | Percentage |");
            sb.AppendLine("|-----------|-------|------------|");
            sb.AppendLine($"| Total CS Rules | {totalCsRules} | - |");
            sb.AppendLine($"| Concept | {Concepts} | {Percentage(Concepts, totalCsRules)} |");
            sb.AppendLine($"| CsCaretValueRule | {CsCaretValueRules} | {Percentage(CsCaretValueRules, totalCsRules)} |");
            sb.AppendLine($"| CsInsertRule | {CsInsertRules} | {Percentage(CsInsertRules, totalCsRules)} |");
            sb.AppendLine();
        }
        
        // Flag usage distribution
        if (FlagTypes.Any())
        {
            sb.AppendLine("## Flag Usage Distribution");
            sb.AppendLine();
            sb.AppendLine("| Flag | Count | Percentage |");
            sb.AppendLine("|------|-------|------------|");
            var totalFlags = FlagTypes.Values.Sum();
            foreach (var flag in FlagTypes.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"| {flag.Key} | {flag.Value} | {Percentage(flag.Value, totalFlags)} |");
            }
            sb.AppendLine();
        }
        
        // Binding strength distribution
        if (BindingStrengths.Any())
        {
            sb.AppendLine("## ValueSet Binding Strength Distribution");
            sb.AppendLine();
            sb.AppendLine("| Strength | Count | Percentage |");
            sb.AppendLine("|----------|-------|------------|");
            foreach (var strength in BindingStrengths.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"| {strength.Key} | {strength.Value} | {Percentage(strength.Value, ValueSetRules)} |");
            }
            sb.AppendLine();
        }
        
        // Extension context distribution
        if (ExtensionContexts.Any())
        {
            sb.AppendLine("## Extension Context Distribution");
            sb.AppendLine();
            sb.AppendLine("| Context | Count |");
            sb.AppendLine("|---------|-------|");
            foreach (var context in ExtensionContexts.OrderByDescending(x => x.Value).Take(20))
            {
                sb.AppendLine($"| {context.Key} | {context.Value} |");
            }
            if (ExtensionContexts.Count > 20)
            {
                sb.AppendLine($"| ... and {ExtensionContexts.Count - 20} more | |");
            }
            sb.AppendLine();
        }
        
        // Logical characteristics distribution
        if (LogicalCharacteristics.Any())
        {
            sb.AppendLine("## Logical Characteristics Distribution");
            sb.AppendLine();
            sb.AppendLine("| Characteristic | Count |");
            sb.AppendLine("|----------------|-------|");
            foreach (var characteristic in LogicalCharacteristics.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"| {characteristic.Key} | {characteristic.Value} |");
            }
            sb.AppendLine();
        }
        
        // Insert rule usage (most frequently inserted rulesets)
        if (InsertedRuleSets.Any())
        {
            sb.AppendLine("## Most Frequently Inserted RuleSets");
            sb.AppendLine();
            sb.AppendLine("| RuleSet Name | Insert Count |");
            sb.AppendLine("|--------------|--------------|");
            foreach (var ruleset in InsertedRuleSets.OrderByDescending(x => x.Value).Take(20))
            {
                sb.AppendLine($"| {ruleset.Key} | {ruleset.Value} |");
            }
            if (InsertedRuleSets.Count > 20)
            {
                sb.AppendLine($"| ... and {InsertedRuleSets.Count - 20} more | |");
            }
            sb.AppendLine();
        }
        
        // Value type distribution
        if (ValueTypes.Any())
        {
            sb.AppendLine("## Value Type Distribution");
            sb.AppendLine();
            sb.AppendLine("| Value Type | Count | Percentage |");
            sb.AppendLine("|------------|-------|------------|");
            var totalValues = ValueTypes.Values.Sum();
            foreach (var valueType in ValueTypes.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"| {valueType.Key} | {valueType.Value} | {Percentage(valueType.Value, totalValues)} |");
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
        
        // Metadata usage statistics
        sb.AppendLine("## Metadata Usage Statistics");
        sb.AppendLine();
        var totalEntities = Aliases + Profiles + Extensions + Logicals + Resources + 
                           Instances + Invariants + ValueSets + CodeSystems + RuleSets + Mappings;
        sb.AppendLine("| Metadata Field | Count | Percentage |");
        sb.AppendLine("|----------------|-------|------------|");
        sb.AppendLine($"| Entities with Id | {EntitiesWithId} | {Percentage(EntitiesWithId, totalEntities)} |");
        sb.AppendLine($"| Entities with Title | {EntitiesWithTitle} | {Percentage(EntitiesWithTitle, totalEntities)} |");
        sb.AppendLine($"| Entities with Description | {EntitiesWithDescription} | {Percentage(EntitiesWithDescription, totalEntities)} |");
        sb.AppendLine();
        
        // Key insights
        sb.AppendLine("## Key Insights");
        sb.AppendLine();
        sb.AppendLine($"- **Total Entities**: {totalEntities}");
        sb.AppendLine($"- **Most Common Entity Type**: {GetMostCommonEntityType()}");
        sb.AppendLine($"- **Most Common SD Rule**: {GetMostCommonSdRule()}");
        sb.AppendLine($"- **Most Common Flag**: {FlagTypes.OrderByDescending(x => x.Value).FirstOrDefault().Key ?? "N/A"}");
        sb.AppendLine($"- **Most Common Binding Strength**: {BindingStrengths.OrderByDescending(x => x.Value).FirstOrDefault().Key ?? "N/A"}");
        sb.AppendLine($"- **Most Inserted RuleSet**: {InsertedRuleSets.OrderByDescending(x => x.Value).FirstOrDefault().Key ?? "N/A"}");
        sb.AppendLine($"- **Parameterized RuleSet Usage**: {Percentage(ParameterizedRuleSets, RuleSets)} of RuleSets are parameterized");
        sb.AppendLine($"- **Average Rules per Profile**: {(Profiles > 0 ? Average(totalSdRules, Profiles) : "0.0")}");
        sb.AppendLine($"- **Comment Usage Rate**: {Percentage(ElementsWithLeadingComments + ElementsWithTrailingComments, totalSdRules)} of rules have comments");
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
        sb.AppendLine($"*Generated by FSH Feature Analytics - {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
        
        return sb.ToString();
    }

    private string GetMostCommonEntityType()
    {
        var entityCounts = new Dictionary<string, int>
        {
            { "Alias", Aliases },
            { "Profile", Profiles },
            { "Extension", Extensions },
            { "Logical", Logicals },
            { "Resource", Resources },
            { "Instance", Instances },
            { "Invariant", Invariants },
            { "ValueSet", ValueSets },
            { "CodeSystem", CodeSystems },
            { "RuleSet", RuleSets },
            { "Mapping", Mappings }
        };
        
        return entityCounts.OrderByDescending(x => x.Value).FirstOrDefault().Key ?? "N/A";
    }

    private string GetMostCommonSdRule()
    {
        var ruleCounts = new Dictionary<string, int>
        {
            { "CardRule", CardRules },
            { "FlagRule", FlagRules },
            { "ValueSetRule", ValueSetRules },
            { "FixedValueRule", FixedValueRules },
            { "ContainsRule", ContainsRules },
            { "OnlyRule", OnlyRules },
            { "ObeysRule", ObeysRules },
            { "CaretValueRule", CaretValueRules },
            { "InsertRule", InsertRules },
            { "PathRule", PathRules }
        };
        
        return ruleCounts.OrderByDescending(x => x.Value).FirstOrDefault().Key ?? "N/A";
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
