using System.Text;
using fsh_processor.Models;

namespace fsh_processor;

/// <summary>
/// FSH Serializer - Serializes a <see cref="FshDoc"/> object model back into FSH text format.
/// </summary>
/// <remarks>
/// This serializer converts a structured <see cref="FshDoc"/> object model back to
/// valid FHIR Shorthand (FSH) text that can be parsed by <see cref="FshParser"/>.
/// The output preserves formatting, whitespace, and comments through hidden tokens.
/// </remarks>
public static class FshSerializer
{
    private const string Indent = "  "; // Two spaces for indentation

    /// <summary>
    /// Serialize a <see cref="FshDoc"/> to FSH text format.
    /// </summary>
    /// <param name="document">The FSH document to serialize</param>
    /// <returns>FSH text representation of the document</returns>
    public static string Serialize(FshDoc document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var sb = new StringBuilder();

        // Output leading hidden tokens for the document (header comments)
        OutputLeadingHiddenTokens(sb, document, string.Empty);

        // Serialize all entities
        for (int i = 0; i < document.Entities.Count; i++)
        {
            SerializeEntity(sb, document.Entities[i]);
            
            // Add blank line between entities (but not after the last one)
            if (i < document.Entities.Count - 1)
            {
                sb.AppendLine();
            }
        }

        // Output trailing hidden tokens (end-of-file comments)
        OutputTrailingHiddenTokens(sb, document);

        return sb.ToString();
    }

    #region Entity Serialization

    private static void SerializeEntity(StringBuilder sb, FshEntity entity)
    {
        switch (entity)
        {
            case Alias alias:
                SerializeAlias(sb, alias);
                break;
            case Profile profile:
                SerializeProfile(sb, profile);
                break;
            case Extension extension:
                SerializeExtension(sb, extension);
                break;
            case Logical logical:
                SerializeLogical(sb, logical);
                break;
            case Resource resource:
                SerializeResource(sb, resource);
                break;
            case Instance instance:
                SerializeInstance(sb, instance);
                break;
            case Invariant invariant:
                SerializeInvariant(sb, invariant);
                break;
            case ValueSet valueSet:
                SerializeValueSet(sb, valueSet);
                break;
            case CodeSystem codeSystem:
                SerializeCodeSystem(sb, codeSystem);
                break;
            case RuleSet ruleSet:
                SerializeRuleSet(sb, ruleSet);
                break;
            case Mapping mapping:
                SerializeMapping(sb, mapping);
                break;
            default:
                throw new InvalidOperationException($"Unknown entity type: {entity.GetType().Name}");
        }
    }

    private static void SerializeAlias(StringBuilder sb, Alias alias)
    {
        OutputLeadingHiddenTokens(sb, alias, string.Empty);
        
        sb.Append("Alias: ");
        sb.Append(alias.Name);
        sb.Append(" = ");
        sb.Append(alias.Value);
        
        OutputTrailingHiddenTokens(sb, alias);
        sb.AppendLine();
    }

    private static void SerializeProfile(StringBuilder sb, Profile profile)
    {
        OutputLeadingHiddenTokens(sb, profile, string.Empty);
        
        sb.Append("Profile: ");
        sb.Append(profile.Name);
        
        OutputTrailingHiddenTokens(sb, profile);
        sb.AppendLine();

        // Metadata
        SerializeProfileMetadata(sb, profile);

        // Rules
        foreach (var rule in profile.Rules)
        {
            SerializeLrRule(sb, rule);
        }
    }

    private static void SerializeProfileMetadata(StringBuilder sb, Profile profile)
    {
        if (profile.Parent != null)
        {
            sb.Append("Parent: ");
            sb.Append(profile.Parent.Value);
            OutputTrailingHiddenTokens(sb, profile.Parent);
            sb.AppendLine();
        }
        if (profile.Id != null)
        {
            sb.Append("Id: ");
            sb.Append(profile.Id.Value);
            OutputTrailingHiddenTokens(sb, profile.Id);
            sb.AppendLine();
        }
        if (profile.Title != null)
        {
            sb.Append("Title: ");
            SerializeQuotedString(sb, profile.Title.Value);
            OutputTrailingHiddenTokens(sb, profile.Title);
            sb.AppendLine();
        }
        if (profile.Description != null)
        {
            sb.Append("Description: ");
            SerializeQuotedString(sb, profile.Description.Value);
            OutputTrailingHiddenTokens(sb, profile.Description);
            sb.AppendLine();
        }
    }

    private static void SerializeExtension(StringBuilder sb, Extension extension)
    {
        OutputLeadingHiddenTokens(sb, extension, string.Empty);
        
        sb.Append("Extension: ");
        sb.Append(extension.Name);
        
        OutputTrailingHiddenTokens(sb, extension);
        sb.AppendLine();

        // Metadata
        SerializeExtensionMetadata(sb, extension);

        // Context
        if (extension.Contexts != null && extension.Contexts.Count > 0)
        {
            sb.Append("Context: ");
            for (int i = 0; i < extension.Contexts.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(extension.Contexts[i].Value);
            }
            sb.AppendLine();
        }

        // Rules
        foreach (var rule in extension.Rules)
        {
            SerializeLrRule(sb, rule);
        }
    }

    private static void SerializeExtensionMetadata(StringBuilder sb, Extension extension)
    {
        if (extension.Parent != null)
        {
            sb.Append("Parent: ");
            sb.AppendLine(extension.Parent);
        }
        if (extension.Id != null)
        {
            sb.Append("Id: ");
            sb.AppendLine(extension.Id);
        }
        if (extension.Title != null)
        {
            sb.Append("Title: ");
            SerializeQuotedString(sb, extension.Title);
            sb.AppendLine();
        }
        if (extension.Description != null)
        {
            sb.Append("Description: ");
            SerializeQuotedString(sb, extension.Description);
            sb.AppendLine();
        }
    }

    private static void SerializeLogical(StringBuilder sb, Logical logical)
    {
        OutputLeadingHiddenTokens(sb, logical, string.Empty);
        
        sb.Append("Logical: ");
        sb.Append(logical.Name);
        
        OutputTrailingHiddenTokens(sb, logical);
        sb.AppendLine();

        // Metadata
        SerializeLogicalMetadata(sb, logical);

        // Characteristics
        if (logical.Characteristics != null && logical.Characteristics.Count > 0)
        {
            sb.Append("Characteristics: ");
            for (int i = 0; i < logical.Characteristics.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(logical.Characteristics[i]);
            }
            sb.AppendLine();
        }

        // Rules
        foreach (var rule in logical.Rules)
        {
            SerializeLrRule(sb, rule);
        }
    }

    private static void SerializeLogicalMetadata(StringBuilder sb, Logical logical)
    {
        if (logical.Parent != null)
        {
            sb.Append("Parent: ");
            sb.AppendLine(logical.Parent);
        }
        if (logical.Id != null)
        {
            sb.Append("Id: ");
            sb.AppendLine(logical.Id);
        }
        if (logical.Title != null)
        {
            sb.Append("Title: ");
            SerializeQuotedString(sb, logical.Title);
            sb.AppendLine();
        }
        if (logical.Description != null)
        {
            sb.Append("Description: ");
            SerializeQuotedString(sb, logical.Description);
            sb.AppendLine();
        }
    }

    private static void SerializeResource(StringBuilder sb, Resource resource)
    {
        OutputLeadingHiddenTokens(sb, resource, string.Empty);
        
        sb.Append("Resource: ");
        sb.Append(resource.Name);
        
        OutputTrailingHiddenTokens(sb, resource);
        sb.AppendLine();

        // Metadata
        SerializeResourceMetadata(sb, resource);

        // Rules
        foreach (var rule in resource.Rules)
        {
            SerializeLrRule(sb, rule);
        }
    }

    private static void SerializeResourceMetadata(StringBuilder sb, Resource resource)
    {
        if (resource.Parent != null)
        {
            sb.Append("Parent: ");
            sb.AppendLine(resource.Parent);
        }
        if (resource.Id != null)
        {
            sb.Append("Id: ");
            sb.AppendLine(resource.Id);
        }
        if (resource.Title != null)
        {
            sb.Append("Title: ");
            SerializeQuotedString(sb, resource.Title);
            sb.AppendLine();
        }
        if (resource.Description != null)
        {
            sb.Append("Description: ");
            SerializeQuotedString(sb, resource.Description);
            sb.AppendLine();
        }
    }

    private static void SerializeInstance(StringBuilder sb, Instance instance)
    {
        OutputLeadingHiddenTokens(sb, instance, string.Empty);
        
        sb.Append("Instance: ");
        sb.Append(instance.Name);
        
        OutputTrailingHiddenTokens(sb, instance);
        sb.AppendLine();

        // Metadata
        if (instance.InstanceOf != null)
        {
            sb.Append("InstanceOf: ");
            sb.AppendLine(instance.InstanceOf);
        }
        if (instance.Title != null)
        {
            sb.Append("Title: ");
            SerializeQuotedString(sb, instance.Title);
            sb.AppendLine();
        }
        if (instance.Description != null)
        {
            sb.Append("Description: ");
            SerializeQuotedString(sb, instance.Description);
            sb.AppendLine();
        }
        if (instance.Usage != null)
        {
            sb.Append("Usage: ");
            sb.AppendLine(instance.Usage);
        }

        // Rules
        foreach (var rule in instance.Rules)
        {
            SerializeInstanceRule(sb, rule);
        }
    }

    private static void SerializeInvariant(StringBuilder sb, Invariant invariant)
    {
        OutputLeadingHiddenTokens(sb, invariant, string.Empty);
        
        sb.Append("Invariant: ");
        sb.Append(invariant.Name);
        
        OutputTrailingHiddenTokens(sb, invariant);
        sb.AppendLine();

        // Metadata
        if (invariant.Description != null)
        {
            sb.Append("Description: ");
            SerializeQuotedString(sb, invariant.Description);
            sb.AppendLine();
        }
        if (invariant.Expression != null)
        {
            sb.Append("Expression: ");
            SerializeQuotedString(sb, invariant.Expression);
            sb.AppendLine();
        }
        if (invariant.XPath != null)
        {
            sb.Append("XPath: ");
            SerializeQuotedString(sb, invariant.XPath);
            sb.AppendLine();
        }
        if (invariant.Severity != null)
        {
            sb.Append("Severity: ");
            sb.AppendLine(invariant.Severity);
        }

        // Rules
        foreach (var rule in invariant.Rules)
        {
            SerializeInvariantRule(sb, rule);
        }
    }

    private static void SerializeValueSet(StringBuilder sb, ValueSet valueSet)
    {
        OutputLeadingHiddenTokens(sb, valueSet, string.Empty);
        
        sb.Append("ValueSet: ");
        sb.Append(valueSet.Name);
        
        OutputTrailingHiddenTokens(sb, valueSet);
        sb.AppendLine();

        // Metadata
        if (valueSet.Id != null)
        {
            sb.Append("Id: ");
            sb.AppendLine(valueSet.Id);
        }
        if (valueSet.Title != null)
        {
            sb.Append("Title: ");
            SerializeQuotedString(sb, valueSet.Title);
            sb.AppendLine();
        }
        if (valueSet.Description != null)
        {
            sb.Append("Description: ");
            SerializeQuotedString(sb, valueSet.Description);
            sb.AppendLine();
        }

        // Rules
        foreach (var rule in valueSet.Rules)
        {
            SerializeVsRule(sb, rule);
        }
    }

    private static void SerializeCodeSystem(StringBuilder sb, CodeSystem codeSystem)
    {
        OutputLeadingHiddenTokens(sb, codeSystem, string.Empty);
        
        sb.Append("CodeSystem: ");
        sb.Append(codeSystem.Name);
        
        OutputTrailingHiddenTokens(sb, codeSystem);
        sb.AppendLine();

        // Metadata
        if (codeSystem.Id != null)
        {
            sb.Append("Id: ");
            sb.AppendLine(codeSystem.Id);
        }
        if (codeSystem.Title != null)
        {
            sb.Append("Title: ");
            SerializeQuotedString(sb, codeSystem.Title);
            sb.AppendLine();
        }
        if (codeSystem.Description != null)
        {
            sb.Append("Description: ");
            SerializeQuotedString(sb, codeSystem.Description);
            sb.AppendLine();
        }

        // Rules
        foreach (var rule in codeSystem.Rules)
        {
            SerializeCsRule(sb, rule);
        }
    }

    private static void SerializeRuleSet(StringBuilder sb, RuleSet ruleSet)
    {
        OutputLeadingHiddenTokens(sb, ruleSet, string.Empty);
        
        sb.Append("RuleSet: ");
        
        if (ruleSet.IsParameterized)
        {
            // For parameterized rulesets, output unparsed content
            sb.Append(ruleSet.Name);
            sb.Append("(");
            
            if (ruleSet.Parameters != null)
            {
                for (int i = 0; i < ruleSet.Parameters.Count; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    
                    var param = ruleSet.Parameters[i];
                    if (param.IsBracketed)
                        sb.Append("[[");
                    
                    sb.Append(param.Value);
                    
                    if (param.IsBracketed)
                        sb.Append("]]");
                }
            }
            
            sb.Append(")");
            
            OutputTrailingHiddenTokens(sb, ruleSet);
            sb.AppendLine();
            
            // Output unparsed content as-is
            if (ruleSet.UnparsedContent != null)
            {
                sb.Append(ruleSet.UnparsedContent);
                if (!ruleSet.UnparsedContent.EndsWith("\n"))
                    sb.AppendLine();
            }
        }
        else
        {
            // Non-parameterized ruleset
            sb.Append(ruleSet.Name);
            
            OutputTrailingHiddenTokens(sb, ruleSet);
            sb.AppendLine();

            // Rules
            foreach (var rule in ruleSet.Rules)
            {
                SerializeRuleSetRule(sb, rule);
            }
        }
    }

    private static void SerializeMapping(StringBuilder sb, Mapping mapping)
    {
        OutputLeadingHiddenTokens(sb, mapping, string.Empty);
        
        sb.Append("Mapping: ");
        sb.Append(mapping.Name);
        
        OutputTrailingHiddenTokens(sb, mapping);
        sb.AppendLine();

        // Metadata
        if (mapping.Id != null)
        {
            sb.Append("Id: ");
            sb.AppendLine(mapping.Id);
        }
        if (mapping.Source != null)
        {
            sb.Append("Source: ");
            sb.AppendLine(mapping.Source);
        }
        if (mapping.Target != null)
        {
            sb.Append("Target: ");
            SerializeQuotedString(sb, mapping.Target);
            sb.AppendLine();
        }
        if (mapping.Title != null)
        {
            sb.Append("Title: ");
            SerializeQuotedString(sb, mapping.Title);
            sb.AppendLine();
        }
        if (mapping.Description != null)
        {
            sb.Append("Description: ");
            SerializeQuotedString(sb, mapping.Description);
            sb.AppendLine();
        }

        // Rules
        foreach (var rule in mapping.Rules)
        {
            SerializeMappingRule(sb, rule);
        }
    }

    #endregion

    #region Rule Serialization

    private static void SerializeSdRule(StringBuilder sb, SdRule rule)
    {
        //Based on Rules.cs, SdRule subclasses are: CardRule, FlagRule, ValueSetRule, ContainsRule, OnlyRule, ObeysRule, PathRule
        // Note: FixedValueRule, CaretValueRule, InsertRule inherit from FshRule, not SdRule
        if (rule is CardRule cardRule)
        {
            SerializeCardRule(sb, cardRule);
        }
        else if (rule is FlagRule flagRule)
        {
            SerializeFlagRule(sb, flagRule);
        }
        else if (rule is ValueSetRule valueSetRule)
        {
            SerializeValueSetRule(sb, valueSetRule);
        }
        else if (rule is ContainsRule containsRule)
        {
            SerializeContainsRule(sb, containsRule);
        }
        else if (rule is OnlyRule onlyRule)
        {
            SerializeOnlyRule(sb, onlyRule);
        }
        else if (rule is ObeysRule obeysRule)
        {
            SerializeObeysRule(sb, obeysRule);
        }
        else if (rule is PathRule pathRule)
        {
            SerializePathRule(sb, pathRule);
        }
        else
        {
            throw new InvalidOperationException($"Unknown SD rule type: {rule.GetType().Name}");
        }
    }

    private static void SerializeLrRule(StringBuilder sb, FshRule rule)
    {
        // LR rules (Logical/Resource) can contain:
        // - AddElementRule, AddCRElementRule (LR-specific)
        // - Any SdRule subclass (CardRule, FlagRule, ValueSetRule, ContainsRule, OnlyRule, ObeysRule, PathRule)
        // - FixedValueRule, CaretValueRule, InsertRule (from FshRule)
        if (rule is AddElementRule addElementRule)
        {
            SerializeAddElementRule(sb, addElementRule);
        }
        else if (rule is AddCRElementRule addCRElementRule)
        {
            SerializeAddCRElementRule(sb, addCRElementRule);
        }
        else if (rule is SdRule sdRule)
        {
            // Handles: CardRule, FlagRule, ValueSetRule, ContainsRule, OnlyRule, ObeysRule, PathRule
            SerializeSdRule(sb, sdRule);
        }
        else if (rule is FixedValueRule fixedValueRule)
        {
            SerializeFixedValueRule(sb, fixedValueRule);
        }
        else if (rule is CaretValueRule caretValueRule)
        {
            SerializeCaretValueRule(sb, caretValueRule);
        }
        else if (rule is InsertRule insertRule)
        {
            SerializeInsertRule(sb, insertRule);
        }
        else
        {
            throw new InvalidOperationException($"Unknown LR rule type: {rule.GetType().Name}");
        }
    }

    private static void SerializeInstanceRule(StringBuilder sb, InstanceRule rule)
    {
        // Instance rules are converted from base rule types
        switch (rule)
        {
            case InstanceFixedValueRule fvr:
                SerializeInstanceFixedValueRule(sb, fvr);
                break;
            case InstanceInsertRule ir:
                SerializeInstanceInsertRule(sb, ir);
                break;
            case InstancePathRule pr:
                SerializeInstancePathRule(sb, pr);
                break;
            default:
                throw new InvalidOperationException($"Unknown instance rule type: {rule.GetType().Name}");
        }
    }

    private static void SerializeInvariantRule(StringBuilder sb, InvariantRule rule)
    {
        // Invariant rules are converted from base rule types
        switch (rule)
        {
            case InvariantFixedValueRule fvr:
                SerializeInvariantFixedValueRule(sb, fvr);
                break;
            case InvariantInsertRule ir:
                SerializeInvariantInsertRule(sb, ir);
                break;
            case InvariantPathRule pr:
                SerializeInvariantPathRule(sb, pr);
                break;
            default:
                throw new InvalidOperationException($"Unknown invariant rule type: {rule.GetType().Name}");
        }
    }

    private static void SerializeVsRule(StringBuilder sb, VsRule rule)
    {
        switch (rule)
        {
            case VsComponentRule componentRule:
                SerializeVsComponentRule(sb, componentRule);
                break;
            case VsCaretValueRule caretValueRule:
                SerializeVsCaretValueRule(sb, caretValueRule);
                break;
            case CodeCaretValueRule codeCaretValueRule:
                SerializeCodeCaretValueRule(sb, codeCaretValueRule);
                break;
            case VsInsertRule insertRule:
                SerializeVsInsertRule(sb, insertRule);
                break;
            case CodeInsertRule codeInsertRule:
                SerializeCodeInsertRule(sb, codeInsertRule);
                break;
            default:
                throw new InvalidOperationException($"Unknown VS rule type: {rule.GetType().Name}");
        }
    }

    private static void SerializeCsRule(StringBuilder sb, CsRule rule)
    {
        switch (rule)
        {
            case Concept concept:
                SerializeConcept(sb, concept);
                break;
            case CsCaretValueRule caretValueRule:
                SerializeCsCaretValueRule(sb, caretValueRule);
                break;
            case CsInsertRule insertRule:
                SerializeCsInsertRule(sb, insertRule);
                break;
            default:
                throw new InvalidOperationException($"Unknown CS rule type: {rule.GetType().Name}");
        }
    }

    private static void SerializeMappingRule(StringBuilder sb, MappingRule rule)
    {
        switch (rule)
        {
            case MappingMapRule mapRule:
                SerializeMappingMapRule(sb, mapRule);
                break;
            case MappingInsertRule insertRule:
                SerializeMappingInsertRule(sb, insertRule);
                break;
            case MappingPathRule pathRule:
                SerializeMappingPathRule(sb, pathRule);
                break;
            default:
                throw new InvalidOperationException($"Unknown mapping rule type: {rule.GetType().Name}");
        }
    }

    private static void SerializeRuleSetRule(StringBuilder sb, FshRule rule)
    {
        // RuleSet rules can be any rule type - delegate to appropriate serializer
        SerializeLrRule(sb, rule);
    }

    // Specific rule serialization methods
    private static void SerializeCardRule(StringBuilder sb, CardRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        sb.Append(" ");
        sb.Append(rule.Cardinality);
        
        if (rule.Flags != null && rule.Flags.Count > 0)
        {
            foreach (var flag in rule.Flags)
            {
                sb.Append(" ");
                sb.Append(flag);
            }
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeFlagRule(StringBuilder sb, FlagRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        
        if (rule.AdditionalPaths != null && rule.AdditionalPaths.Count > 0)
        {
            foreach (var path in rule.AdditionalPaths)
            {
                sb.Append(" and ");
                sb.Append(path);
            }
        }
        
        if (rule.Flags != null && rule.Flags.Count > 0)
        {
            foreach (var flag in rule.Flags)
            {
                sb.Append(" ");
                sb.Append(flag);
            }
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeValueSetRule(StringBuilder sb, ValueSetRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        sb.Append(" from ");
        sb.Append(rule.ValueSetName);
        
        if (rule.Strength != null)
        {
            sb.Append(" ");
            sb.Append(rule.Strength);
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeFixedValueRule(StringBuilder sb, FixedValueRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        sb.Append(" = ");
        SerializeValue(sb, rule.Value);
        
        if (rule.Exactly)
        {
            sb.Append(" (exactly)");
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeContainsRule(StringBuilder sb, ContainsRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        sb.Append(" contains ");
        
        if (rule.Items != null)
        {
            for (int i = 0; i < rule.Items.Count; i++)
            {
                if (i > 0)
                    sb.Append(" and ");
                SerializeContainsItem(sb, rule.Items[i]);
            }
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeContainsItem(StringBuilder sb, ContainsItem item)
    {
        OutputLeadingHiddenTokens(sb, item, string.Empty);
        
        sb.Append(item.Name);
        
        if (item.NamedAlias != null)
        {
            sb.Append(" named ");
            sb.Append(item.NamedAlias);
        }
        
        sb.Append(" ");
        sb.Append(item.Cardinality);
        
        if (item.Flags != null && item.Flags.Count > 0)
        {
            foreach (var flag in item.Flags)
            {
                sb.Append(" ");
                sb.Append(flag);
            }
        }
        
        OutputTrailingHiddenTokens(sb, item);
    }

    private static void SerializeOnlyRule(StringBuilder sb, OnlyRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        sb.Append(" only ");
        
        if (rule.TargetTypes != null)
        {
            for (int i = 0; i < rule.TargetTypes.Count; i++)
            {
                if (i > 0)
                    sb.Append(" or ");
                sb.Append(rule.TargetTypes[i]);
            }
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeObeysRule(StringBuilder sb, ObeysRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        if (rule.Path != null)
        {
            sb.Append(rule.Path);
            sb.Append(" ");
        }
        sb.Append("obeys ");
        
        if (rule.InvariantNames != null)
        {
            for (int i = 0; i < rule.InvariantNames.Count; i++)
            {
                if (i > 0)
                    sb.Append(" and ");
                sb.Append(rule.InvariantNames[i]);
            }
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeCaretValueRule(StringBuilder sb, CaretValueRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        if (rule.Path != null)
        {
            sb.Append(rule.Path);
            sb.Append(" ");
        }
        sb.Append(rule.CaretPath);
        sb.Append(" = ");
        SerializeValue(sb, rule.Value);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeInsertRule(StringBuilder sb, InsertRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        if (rule.Path != null)
        {
            sb.Append(rule.Path);
            sb.Append(" ");
        }
        sb.Append("insert ");
        sb.Append(rule.RuleSetReference);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializePathRule(StringBuilder sb, PathRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeAddElementRule(StringBuilder sb, AddElementRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        sb.Append(" ");
        sb.Append(rule.Cardinality);
        
        if (rule.Flags != null && rule.Flags.Count > 0)
        {
            foreach (var flag in rule.Flags)
            {
                sb.Append(" ");
                sb.Append(flag);
            }
        }
        
        if (rule.TargetTypes != null)
        {
            for (int i = 0; i < rule.TargetTypes.Count; i++)
            {
                if (i > 0)
                    sb.Append(" or ");
                else
                    sb.Append(" ");
                sb.Append(rule.TargetTypes[i]);
            }
        }
        
        if (rule.ShortDescription != null)
        {
            sb.Append(" ");
            SerializeQuotedString(sb, rule.ShortDescription);
        }
        
        if (rule.Definition != null)
        {
            sb.Append(" ");
            SerializeQuotedString(sb, rule.Definition);
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeAddCRElementRule(StringBuilder sb, AddCRElementRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        sb.Append(" ");
        sb.Append(rule.Cardinality);
        
        if (rule.Flags != null && rule.Flags.Count > 0)
        {
            foreach (var flag in rule.Flags)
            {
                sb.Append(" ");
                sb.Append(flag);
            }
        }
        
        sb.Append(" contentReference ");
        sb.Append(rule.ContentReference);
        
        if (rule.ShortDescription != null)
        {
            sb.Append(" ");
            SerializeQuotedString(sb, rule.ShortDescription);
        }
        
        if (rule.Definition != null)
        {
            sb.Append(" ");
            SerializeQuotedString(sb, rule.Definition);
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    // Instance rule variants
    private static void SerializeInstanceFixedValueRule(StringBuilder sb, InstanceFixedValueRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        sb.Append(" = ");
        SerializeValue(sb, rule.Value);
        
        if (rule.Exactly)
        {
            sb.Append(" (exactly)");
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeInstanceInsertRule(StringBuilder sb, InstanceInsertRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        if (rule.Path != null)
        {
            sb.Append(rule.Path);
            sb.Append(" ");
        }
        sb.Append("insert ");
        sb.Append(rule.RuleSetReference);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeInstancePathRule(StringBuilder sb, InstancePathRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    // Invariant rule variants
    private static void SerializeInvariantFixedValueRule(StringBuilder sb, InvariantFixedValueRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        sb.Append(" = ");
        SerializeValue(sb, rule.Value);
        
        if (rule.Exactly)
        {
            sb.Append(" (exactly)");
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeInvariantInsertRule(StringBuilder sb, InvariantInsertRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        if (rule.Path != null)
        {
            sb.Append(rule.Path);
            sb.Append(" ");
        }
        sb.Append("insert ");
        sb.Append(rule.RuleSetReference);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeInvariantPathRule(StringBuilder sb, InvariantPathRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    // ValueSet rules
    private static void SerializeVsComponentRule(StringBuilder sb, VsComponentRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        
        if (rule.IsInclude.HasValue)
        {
            sb.Append(rule.IsInclude.Value ? "include " : "exclude ");
        }
        
        if (rule.IsConceptComponent && rule.ConceptCode != null)
        {
            SerializeCode(sb, rule.ConceptCode);
            
            if (rule.FromSystem != null || rule.FromValueSets?.Count > 0)
            {
                sb.Append(" from ");
                
                if (rule.FromSystem != null)
                {
                    sb.Append("system ");
                    sb.Append(rule.FromSystem);
                }
                
                if (rule.FromValueSets != null && rule.FromValueSets.Count > 0)
                {
                    if (rule.FromSystem != null)
                        sb.Append(" and ");
                    
                    sb.Append("valueset ");
                    for (int i = 0; i < rule.FromValueSets.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(" and ");
                        sb.Append(rule.FromValueSets[i]);
                    }
                }
            }
        }
        else
        {
            // Filter component
            sb.Append("codes from ");
            
            if (rule.FromSystem != null)
            {
                sb.Append("system ");
                sb.Append(rule.FromSystem);
            }
            
            if (rule.FromValueSets != null && rule.FromValueSets.Count > 0)
            {
                if (rule.FromSystem != null)
                    sb.Append(" and ");
                
                sb.Append("valueset ");
                for (int i = 0; i < rule.FromValueSets.Count; i++)
                {
                    if (i > 0)
                        sb.Append(" and ");
                    sb.Append(rule.FromValueSets[i]);
                }
            }
            
            if (rule.Filters != null && rule.Filters.Count > 0)
            {
                sb.Append(" where ");
                for (int i = 0; i < rule.Filters.Count; i++)
                {
                    if (i > 0)
                        sb.Append(" and ");
                    SerializeVsFilterDefinition(sb, rule.Filters[i]);
                }
            }
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeVsFilterDefinition(StringBuilder sb, VsFilterDefinition filter)
    {
        sb.Append(filter.Property);
        sb.Append(" ");
        sb.Append(filter.Operator);
        
        if (filter.Value != null)
        {
            sb.Append(" ");
            SerializeValue(sb, filter.Value);
        }
    }

    private static void SerializeVsCaretValueRule(StringBuilder sb, VsCaretValueRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        if (rule.Path != null)
        {
            sb.Append(rule.Path);
            sb.Append(" ");
        }
        sb.Append(rule.CaretPath);
        sb.Append(" = ");
        SerializeValue(sb, rule.Value);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeCodeCaretValueRule(StringBuilder sb, CodeCaretValueRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        
        if (rule.Codes != null && rule.Codes.Count > 0)
        {
            foreach (var code in rule.Codes)
            {
                sb.Append(code);
                sb.Append(" ");
            }
        }
        
        sb.Append(rule.CaretPath);
        sb.Append(" = ");
        SerializeValue(sb, rule.Value);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeVsInsertRule(StringBuilder sb, VsInsertRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        if (rule.Path != null)
        {
            sb.Append(rule.Path);
            sb.Append(" ");
        }
        sb.Append("insert ");
        sb.Append(rule.RuleSetReference);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeCodeInsertRule(StringBuilder sb, CodeInsertRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        
        if (rule.Codes != null && rule.Codes.Count > 0)
        {
            foreach (var code in rule.Codes)
            {
                sb.Append(code);
                sb.Append(" ");
            }
        }
        
        sb.Append("insert ");
        sb.Append(rule.RuleSetReference);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    // CodeSystem rules
    private static void SerializeConcept(StringBuilder sb, Concept concept)
    {
        OutputLeadingHiddenTokens(sb, concept, string.Empty);
        
        sb.Append($"{concept.Indent}* ");
        
        if (concept.Codes != null)
        {
            foreach (var code in concept.Codes)
            {
                sb.Append(code);
                sb.Append(" ");
            }
        }
        
        if (concept.Display != null)
        {
            SerializeQuotedString(sb, concept.Display);
            sb.Append(" ");
        }
        
        if (concept.Definition != null)
        {
            SerializeQuotedString(sb, concept.Definition);
        }
        
        OutputTrailingHiddenTokens(sb, concept);
        sb.AppendLine();
    }

    private static void SerializeCsCaretValueRule(StringBuilder sb, CsCaretValueRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        
        if (rule.Codes != null && rule.Codes.Count > 0)
        {
            foreach (var code in rule.Codes)
            {
                sb.Append(code);
                sb.Append(" ");
            }
        }
        
        sb.Append(rule.CaretPath);
        sb.Append(" = ");
        SerializeValue(sb, rule.Value);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeCsInsertRule(StringBuilder sb, CsInsertRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        
        if (rule.Codes != null && rule.Codes.Count > 0)
        {
            foreach (var code in rule.Codes)
            {
                sb.Append(code);
                sb.Append(" ");
            }
        }
        
        sb.Append("insert ");
        sb.Append(rule.RuleSetReference);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    // Mapping rules
    private static void SerializeMappingMapRule(StringBuilder sb, MappingMapRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        if (rule.Path != null)
        {
            sb.Append(rule.Path);
            sb.Append(" ");
        }
        sb.Append("-> ");
        SerializeQuotedString(sb, rule.Target);
        
        if (rule.Language != null)
        {
            sb.Append(" ");
            SerializeQuotedString(sb, rule.Language);
        }
        
        if (rule.Code != null)
        {
            sb.Append(" ");
            sb.Append(rule.Code);
        }
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeMappingInsertRule(StringBuilder sb, MappingInsertRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        if (rule.Path != null)
        {
            sb.Append(rule.Path);
            sb.Append(" ");
        }
        sb.Append("insert ");
        sb.Append(rule.RuleSetReference);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    private static void SerializeMappingPathRule(StringBuilder sb, MappingPathRule rule)
    {
        OutputLeadingHiddenTokens(sb, rule, string.Empty);
        
        sb.Append($"{rule.Indent}* ");
        sb.Append(rule.Path);
        
        OutputTrailingHiddenTokens(sb, rule);
        sb.AppendLine();
    }

    #endregion

    #region Value Serialization

    private static void SerializeValue(StringBuilder sb, FshValue value)
    {
        switch (value)
        {
            case StringValue sv:
                SerializeQuotedString(sb, sv.Value);
                break;
            case NumberValue nv:
                sb.Append(nv.Value);
                break;
            case BooleanValue bv:
                sb.Append(bv.Value ? "true" : "false");
                break;
            case DateTimeValue dtv:
                sb.Append(dtv.Value);
                break;
            case TimeValue tv:
                sb.Append(tv.Value);
                break;
            case Code code:
                SerializeCode(sb, code);
                break;
            case Quantity quantity:
                SerializeQuantity(sb, quantity);
                break;
            case Ratio ratio:
                SerializeRatio(sb, ratio);
                break;
            case Reference reference:
                SerializeReference(sb, reference);
                break;
            case Canonical canonical:
                SerializeCanonical(sb, canonical);
                break;
            case NameValue nameValue:
                sb.Append(nameValue.Value);
                break;
            case RegexValue regexValue:
                sb.Append(regexValue.Pattern);
                break;
            default:
                throw new InvalidOperationException($"Unknown value type: {value.GetType().Name}");
        }
    }

    private static void SerializeCode(StringBuilder sb, Code code)
    {
        sb.Append(code.Value);
        
        if (code.Display != null)
        {
            sb.Append(" ");
            SerializeQuotedString(sb, code.Display);
        }
    }

    private static void SerializeQuantity(StringBuilder sb, Quantity quantity)
    {
        if (quantity.Value != 1)
        {
            sb.Append(quantity.Value);
            sb.Append(" ");
        }
        
        sb.Append(quantity.Unit);
        
        if (quantity.Display != null)
        {
            sb.Append(" ");
            SerializeQuotedString(sb, quantity.Display);
        }
    }

    private static void SerializeRatio(StringBuilder sb, Ratio ratio)
    {
        SerializeRatioPart(sb, ratio.Numerator);
        sb.Append(" : ");
        SerializeRatioPart(sb, ratio.Denominator);
    }

    private static void SerializeRatioPart(StringBuilder sb, RatioPart part)
    {
        if (part.QuantityValue != null)
        {
            SerializeQuantity(sb, part.QuantityValue);
        }
        else
        {
            sb.Append(part.Value);
        }
    }

    private static void SerializeReference(StringBuilder sb, Reference reference)
    {
        sb.Append("Reference(");
        sb.Append(reference.Type);
        sb.Append(")");
        
        if (reference.Display != null)
        {
            sb.Append(" ");
            SerializeQuotedString(sb, reference.Display);
        }
    }

    private static void SerializeCanonical(StringBuilder sb, Canonical canonical)
    {
        sb.Append("Canonical(");
        sb.Append(canonical.Url);
        
        if (canonical.Version != null)
        {
            sb.Append("|");
            sb.Append(canonical.Version);
        }
        
        sb.Append(")");
    }

    #endregion

    #region Helper Methods

    private static void SerializeQuotedString(StringBuilder sb, string value)
    {
        // Determine if we need multiline string
        if (value.Contains('\n') || value.Contains('\r'))
        {
            sb.Append("\"\"\"");
            sb.Append(value);
            sb.Append("\"\"\"");
        }
        else
        {
            sb.Append("\"");
            sb.Append(EscapeString(value));
            sb.Append("\"");
        }
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Outputs hidden tokens if present, otherwise outputs default formatting.
    /// </summary>
    private static void OutputLeadingHiddenTokens(StringBuilder sb, FshNode node, string defaultOutput)
    {
        if (node.LeadingHiddenTokens != null && node.LeadingHiddenTokens.Count > 0)
        {
            // Output captured tokens exactly as they were
            foreach (var token in node.LeadingHiddenTokens)
            {
                sb.Append(token.Text);
            }
        }
        else if (!string.IsNullOrEmpty(defaultOutput))
        {
            // Use default formatting
            sb.Append(defaultOutput);
        }
    }

    /// <summary>
    /// Outputs trailing hidden tokens if present.
    /// </summary>
    private static void OutputTrailingHiddenTokens(StringBuilder sb, FshNode node)
    {
        if (node.TrailingHiddenTokens != null && node.TrailingHiddenTokens.Count > 0)
        {
            // Output captured tokens exactly as they were
            foreach (var token in node.TrailingHiddenTokens)
            {
                sb.Append(token.Text);
            }
        }
    }

    #endregion
}
