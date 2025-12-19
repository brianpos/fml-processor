using System.Text;
using fml_processor.Models;

namespace fml_processor;

/// <summary>
/// FML Serializer - Serializes a <see cref="FmlStructureMap"/> object model back into FML text format.
/// </summary>
/// <remarks>
/// This serializer converts a structured <see cref="FmlStructureMap"/> object model back to
/// valid FHIR Mapping Language (FML) text that can be parsed by <see cref="FmlParser"/>.
/// The output is formatted with proper indentation and follows FML grammar conventions.
/// </remarks>
public static class FmlSerializer
{
    private const string Indent = "  "; // Two spaces for indentation

    /// <summary>
    /// Serialize a <see cref="FmlStructureMap"/> to FML text format.
    /// </summary>
    /// <param name="structureMap">The structure map to serialize</param>
    /// <returns>FML text representation of the structure map</returns>
    /// <example>
    /// <code>
    /// var structureMap = FmlParser.ParseOrThrow(fmlText);
    /// 
    /// // Modify the structure map...
    /// structureMap.Metadata.Add(new MetadataDeclaration 
    /// { 
    ///     Path = "version", 
    ///     Value = "1.0.0" 
    /// });
    /// 
    /// // Serialize back to FML
    /// var outputFml = FmlSerializer.Serialize(structureMap);
    /// Console.WriteLine(outputFml);
    /// </code>
    /// </example>
    public static string Serialize(FmlStructureMap structureMap)
    {
        if (structureMap == null)
        {
            throw new ArgumentNullException(nameof(structureMap));
        }

        var sb = new StringBuilder();

        // Output leading hidden tokens for the structure map (header comments)
        OutputLeadingHiddenTokens(sb, structureMap, string.Empty);

        // Metadata declarations
        foreach (var metadata in structureMap.Metadata)
        {
            SerializeMetadata(sb, metadata);
        }

        // Add blank line after metadata if present
        if (structureMap.Metadata.Any())
        {
            sb.AppendLine();
        }

        // ConceptMap declarations
        foreach (var conceptMap in structureMap.ConceptMaps)
        {
            SerializeConceptMap(sb, conceptMap);
            sb.AppendLine();
        }

        // Map declaration
        if (structureMap.MapDeclaration != null)
        {
            SerializeMapDeclaration(sb, structureMap.MapDeclaration);
            sb.AppendLine();
        }

        // Structure declarations
        foreach (var structure in structureMap.Structures)
        {
            SerializeStructure(sb, structure);
        }

        // Add blank line after structures if present
        if (structureMap.Structures.Any())
        {
            sb.AppendLine();
        }

        // Import declarations
        foreach (var import in structureMap.Imports)
        {
            SerializeImport(sb, import);
        }

        // Add blank line after imports if present
        if (structureMap.Imports.Any())
        {
            sb.AppendLine();
        }

        // Constant declarations
        foreach (var constant in structureMap.Constants)
        {
            SerializeConstant(sb, constant);
        }

        // Add blank line after constants if present
        if (structureMap.Constants.Any())
        {
            sb.AppendLine();
        }

        // Group declarations
        for (int i = 0; i < structureMap.Groups.Count; i++)
        {
            SerializeGroup(sb, structureMap.Groups[i], 0);
            
            // Add blank line between groups (but not after the last one)
            if (i < structureMap.Groups.Count - 1)
            {
                sb.AppendLine();
            }
        }

        // Output trailing hidden tokens (end-of-file comments)
        OutputTrailingHiddenTokens(sb, structureMap);

        return sb.ToString();
    }

    public static void SerializeMetadata(StringBuilder sb, MetadataDeclaration metadata)
    {
        // Output leading hidden tokens (comments before metadata)
        OutputLeadingHiddenTokens(sb, metadata, string.Empty);
        
        sb.Append("/// ");
        sb.Append(metadata.Path);
        sb.Append(" = ");

        if (metadata.Value != null)
        {
            if (metadata.IsMarkdown)
            {
                // Triple-quoted string for markdown
                sb.AppendLine("\"\"\"");
                sb.AppendLine(metadata.Value);
                sb.Append("\"\"\"");
            }
            else
            {
                // Regular value (may need quoting depending on content)
                SerializeLiteral(sb, metadata.Value);
            }
        }

        // Output trailing hidden tokens (inline comments)
        OutputTrailingHiddenTokens(sb, metadata);
        
        sb.AppendLine();
    }

    public static void SerializeConceptMap(StringBuilder sb, ConceptMapDeclaration conceptMap)
    {
        sb.Append("conceptmap ");
        SerializeUrl(sb, conceptMap.Url);
        sb.AppendLine(" {");

        // Prefixes
        foreach (var prefix in conceptMap.Prefixes)
        {
            sb.Append(Indent);
            sb.Append("prefix ");
            sb.Append(prefix.Id);
            sb.Append(" = ");
            SerializeUrl(sb, prefix.Url);
            sb.AppendLine();
        }

        // Code maps
        foreach (var codeMap in conceptMap.CodeMaps)
        {
            sb.Append(Indent);
            sb.Append(codeMap.Source.Prefix);
            sb.Append(" : ");
            SerializeCode(sb, codeMap.Source.Code);
            sb.Append(" - ");
            sb.Append(codeMap.Target.Prefix);
            sb.Append(" : ");
            SerializeCode(sb, codeMap.Target.Code);
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }

    public static void SerializeMapDeclaration(StringBuilder sb, MapDeclaration mapDecl)
    {
        // Output leading hidden tokens
        OutputLeadingHiddenTokens(sb, mapDecl, string.Empty);
        
        sb.Append("map ");
        SerializeUrl(sb, mapDecl.Url);
        sb.Append(" = ");
        sb.Append(mapDecl.Identifier);
        
        // Output trailing hidden tokens (inline comments)
        OutputTrailingHiddenTokens(sb, mapDecl);
    }

    public static void SerializeStructure(StringBuilder sb, StructureDeclaration structure)
    {
        // Output leading hidden tokens
        OutputLeadingHiddenTokens(sb, structure, string.Empty);
        
        sb.Append("uses ");
        SerializeUrl(sb, structure.Url);

        if (!string.IsNullOrEmpty(structure.Alias))
        {
            sb.Append(" alias ");
            sb.Append(structure.Alias);
        }

        sb.Append(" as ");
        sb.Append(SerializeStructureMode(structure.Mode));
        
        // Output trailing hidden tokens
        OutputTrailingHiddenTokens(sb, structure);
        
        sb.AppendLine();
    }

    public static void SerializeImport(StringBuilder sb, ImportDeclaration import)
    {
        // Output leading hidden tokens
        OutputLeadingHiddenTokens(sb, import, string.Empty);
        
        sb.Append("imports ");
        SerializeUrl(sb, import.Url);
        
        // Output trailing hidden tokens
        OutputTrailingHiddenTokens(sb, import);
        
        sb.AppendLine();
    }

    public static void SerializeConstant(StringBuilder sb, ConstantDeclaration constant)
    {
        // Output leading hidden tokens
        OutputLeadingHiddenTokens(sb, constant, string.Empty);
        
        sb.Append("let ");
        sb.Append(constant.Name);
        sb.Append(" = ");
        sb.Append(constant.Expression);
        sb.Append(";");
        
        // Output trailing hidden tokens
        OutputTrailingHiddenTokens(sb, constant);
        
        sb.AppendLine();
    }

    public static void SerializeGroup(StringBuilder sb, GroupDeclaration group, int indentLevel)
    {
        var indent = GetIndent(indentLevel);

        // Output leading hidden tokens (or default blank line + indent)
        OutputLeadingHiddenTokens(sb, group, "\n" + indent);

        sb.Append("group ");
        sb.Append(group.Name);

        // Parameters
        sb.Append("(");
        for (int i = 0; i < group.Parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            SerializeGroupParameter(sb, group.Parameters[i]);
        }
        sb.Append(")");

        // Extends
        if (!string.IsNullOrEmpty(group.Extends))
        {
            sb.Append(" extends ");
            sb.Append(group.Extends);
        }

        // Type mode
        if (group.TypeMode.HasValue)
        {
            sb.Append(" <<");
            sb.Append(SerializeGroupTypeMode(group.TypeMode.Value));
            sb.Append(">>");
        }

        sb.Append(" {");
        
        // Output trailing hidden tokens (inline comments after opening brace)
        OutputTrailingHiddenTokens(sb, group);
        
        sb.AppendLine();

        // Rules
        foreach (var rule in group.Rules)
        {
            SerializeRule(sb, rule, indentLevel + 1);
        }

        sb.Append(indent);
        sb.AppendLine("}");
    }

    public static void SerializeGroupParameter(StringBuilder sb, GroupParameter parameter)
    {
        // Output leading hidden tokens (comments before parameter)
        OutputLeadingHiddenTokens(sb, parameter, string.Empty);
        
        sb.Append(SerializeParameterMode(parameter.Mode));
        sb.Append(" ");
        sb.Append(parameter.Name);

        if (!string.IsNullOrEmpty(parameter.Type))
        {
            sb.Append(" : ");
            sb.Append(parameter.Type);
        }
        
        // Output trailing hidden tokens (inline comments after parameter)
        OutputTrailingHiddenTokens(sb, parameter);
    }

    public static void SerializeRule(StringBuilder sb, Rule rule, int indentLevel)
    {
        var indent = GetIndent(indentLevel);

        // Output leading hidden tokens (or default indent)
        OutputLeadingHiddenTokens(sb, rule, indent);

        // Sources
        for (int i = 0; i < rule.Sources.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            SerializeRuleSource(sb, rule.Sources[i]);
        }

        // Targets
        if (rule.Targets.Any())
        {
            sb.Append(" -> ");

            for (int i = 0; i < rule.Targets.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                SerializeRuleTarget(sb, rule.Targets[i]);
            }
        }

        // Dependent
        if (rule.Dependent != null)
        {
            SerializeDependent(sb, rule.Dependent, indentLevel);
        }

        // Rule name
        if (!string.IsNullOrEmpty(rule.Name))
        {
            sb.Append(" ");
            SerializeDoubleQuotedString(sb, rule.Name);
        }

        sb.Append(";");
        
        // Output trailing hidden tokens (inline comments)
        OutputTrailingHiddenTokens(sb, rule);
        
        sb.AppendLine();
    }

    public static void SerializeRuleSource(StringBuilder sb, RuleSource source)
    {
        // Output leading hidden tokens (comments before source)
        OutputLeadingHiddenTokens(sb, source, string.Empty);
        
        sb.Append(source.Context);

        if (!string.IsNullOrEmpty(source.Element))
        {
            sb.Append(".");
            sb.Append(source.Element);
        }

        if (!string.IsNullOrEmpty(source.Type))
        {
            sb.Append(" : ");
            sb.Append(source.Type);
        }

        if (source.Min.HasValue)
        {
            sb.Append(" ");
            sb.Append(source.Min.Value);
            sb.Append("..");
            if (source.Max != null)
            {
                sb.Append(source.Max);
            }
            else
            {
                sb.Append("*");
            }
        }

        if (!string.IsNullOrEmpty(source.DefaultValue))
        {
            sb.Append(" default ");
            SerializeDoubleQuotedString(sb, source.DefaultValue);
        }

        if (source.ListMode.HasValue)
        {
            sb.Append(" ");
            sb.Append(SerializeSourceListMode(source.ListMode.Value));
        }

        if (!string.IsNullOrEmpty(source.Variable))
        {
            sb.Append(" as ");
            sb.Append(source.Variable);
        }

        if (!string.IsNullOrEmpty(source.Condition))
        {
            sb.Append(" where (");
            sb.Append(source.Condition);
            sb.Append(")");
        }

        if (!string.IsNullOrEmpty(source.Check))
        {
            sb.Append(" check (");
            sb.Append(source.Check);
            sb.Append(")");
        }

        if (!string.IsNullOrEmpty(source.Log))
        {
            sb.Append(" log (");
            sb.Append(source.Log);
            sb.Append(")");
        }
        
        // Output trailing hidden tokens (inline comments after source)
        OutputTrailingHiddenTokens(sb, source);
    }

    public static void SerializeRuleTarget(StringBuilder sb, RuleTarget target)
    {
        // Output leading hidden tokens (comments before this target)
        OutputLeadingHiddenTokens(sb, target, string.Empty);
        
        // Check if this is an expression-only target (no context/element, just a transform with expression)
        if (string.IsNullOrEmpty(target.Context) && 
            target.Transform != null && 
            target.Transform.Type == TransformType.Evaluate &&
            target.Transform.Parameters.Count == 1 &&
            target.Transform.Parameters[0].Type == TransformParameterType.Expression)
        {
            // Serialize as (expression)
            sb.Append("(");
            sb.Append(target.Transform.Parameters[0].Value);
            sb.Append(")");
            
            if (!string.IsNullOrEmpty(target.Variable))
            {
                sb.Append(" as ");
                sb.Append(target.Variable);
            }

            if (target.ListMode.HasValue)
            {
                sb.Append(" ");
                sb.Append(SerializeTargetListMode(target.ListMode.Value));
            }
            
            // Output trailing hidden tokens (whitespace/comments after this target)
            OutputTrailingHiddenTokens(sb, target);
            
            return;
        }

        // Normal target with context and optional element
        if (!string.IsNullOrEmpty(target.Context))
        {
            sb.Append(target.Context);

            if (!string.IsNullOrEmpty(target.Element))
            {
                sb.Append(".");
                sb.Append(target.Element);
            }
        }

        if (target.Transform != null)
        {
            sb.Append(" = ");
            SerializeTransform(sb, target.Transform);
        }

        if (!string.IsNullOrEmpty(target.Variable))
        {
            sb.Append(" as ");
            sb.Append(target.Variable);
        }

        if (target.ListMode.HasValue)
        {
            sb.Append(" ");
            sb.Append(SerializeTargetListMode(target.ListMode.Value));
        }
        
        // Output trailing hidden tokens (whitespace/comments after this target)
        OutputTrailingHiddenTokens(sb, target);
    }

    public static void SerializeTransform(StringBuilder sb, Transform transform)
    {
        // Check if it's a simple identifier (like variable name) for copy transforms
        if (transform.Type == TransformType.Copy && transform.Parameters.Count == 1)
        {
            var param = transform.Parameters[0];
            if (param.Type == TransformParameterType.Identifier)
            {
                sb.Append(param.Value);
                return;
            }
            else if (param.Type == TransformParameterType.Literal)
            {
                SerializeLiteral(sb, param.Value?.ToString() ?? string.Empty);
                return;
            }
        }

        // Check if it's an expression (for evaluate transforms)
        if (transform.Type == TransformType.Evaluate && 
            transform.Parameters.Count == 1 &&
            transform.Parameters[0].Type == TransformParameterType.Expression)
        {
            sb.Append("(");
            sb.Append(transform.Parameters[0].Value);
            sb.Append(")");
            return;
        }

        // Check if it's a function with no parameters
        if (transform.Parameters.Count == 0)
        {
            sb.Append(transform.Type);
            sb.Append("()");
            return;
        }

        // Otherwise, it's a function call with parameters
        sb.Append(transform.Type);
        sb.Append("(");

        for (int i = 0; i < transform.Parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            SerializeTransformParameter(sb, transform.Parameters[i]);
        }

        sb.Append(")");
    }

    public static void SerializeTransformParameter(StringBuilder sb, TransformParameter parameter)
    {
        switch (parameter.Type)
        {
            case TransformParameterType.Literal:
                if (parameter.Value != null)
                {
                    SerializeLiteral(sb, parameter.Value.ToString()!);
                }
                break;

            case TransformParameterType.Identifier:
                sb.Append(parameter.Value);
                break;

            case TransformParameterType.Expression:
                sb.Append("(");
                sb.Append(parameter.Value);
                sb.Append(")");
                break;
        }
    }

    public static void SerializeDependent(StringBuilder sb, RuleDependent dependent, int indentLevel)
    {
        // Output leading hidden tokens (comments before the 'then')
        OutputLeadingHiddenTokens(sb, dependent, string.Empty);
        
        sb.Append(" then");

        // Group invocations
        if (dependent.Invocations.Any())
        {
            sb.Append(" ");

            for (int i = 0; i < dependent.Invocations.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                SerializeGroupInvocation(sb, dependent.Invocations[i]);
            }
        }

        // Nested rules
        if (dependent.Rules.Any())
        {
            sb.AppendLine(" {");

            foreach (var rule in dependent.Rules)
            {
                SerializeRule(sb, rule, indentLevel + 1);
            }

            sb.Append(GetIndent(indentLevel));
            sb.Append("}");
        }
    }

    public static void SerializeGroupInvocation(StringBuilder sb, GroupInvocation invocation)
    {
        // Output leading hidden tokens (comments before invocation)
        OutputLeadingHiddenTokens(sb, invocation, string.Empty);
        
        sb.Append(invocation.Name);
        sb.Append("(");

        for (int i = 0; i < invocation.Parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            SerializeInvocationParameter(sb, invocation.Parameters[i]);
        }

        sb.Append(")");
        
        // Output trailing hidden tokens (inline comments after invocation)
        OutputTrailingHiddenTokens(sb, invocation);
    }

    public static void SerializeInvocationParameter(StringBuilder sb, InvocationParameter parameter)
    {
        if (parameter.Type == InvocationParameterType.Literal && parameter.Value != null)
        {
            SerializeLiteral(sb, parameter.Value.ToString()!);
        }
        else
        {
            sb.Append(parameter.Value);
        }
    }

    public static void SerializeUrl(StringBuilder sb, string url)
    {
        // URLs are typically enclosed in double quotes in FML
        SerializeDoubleQuotedString(sb, url);
    }

    public static void SerializeCode(StringBuilder sb, string code)
    {
        // Codes can be identifiers or strings
        if (NeedsQuoting(code))
        {
            SerializeLiteral(sb, code);
        }
        else
        {
            sb.Append(code);
        }
    }

    public static void SerializeLiteral(StringBuilder sb, string value)
    {
        // Determine if we should use single or double quotes
        // FML typically uses single quotes for literals
        if (value.Contains('\'') && !value.Contains('"'))
        {
            SerializeDoubleQuotedString(sb, value);
        }
        else
        {
            SerializeSingleQuotedString(sb, value);
        }
    }

    public static void SerializeSingleQuotedString(StringBuilder sb, string value)
    {
        sb.Append("'");
        sb.Append(EscapeString(value, '\''));
        sb.Append("'");
    }

    public static void SerializeDoubleQuotedString(StringBuilder sb, string value)
    {
        sb.Append('"');
        sb.Append(EscapeString(value, '"'));
        sb.Append('"');
    }

    public static string EscapeString(string value, char quoteChar)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(quoteChar.ToString(), $"\\{quoteChar}")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    public static bool NeedsQuoting(string value)
    {
        // Simple heuristic: if it's not a valid identifier, it needs quoting
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (!char.IsLetter(value[0]) && value[0] != '_')
        {
            return true;
        }

        return value.Any(c => !char.IsLetterOrDigit(c) && c != '_');
    }

    public static string SerializeParameterMode(ParameterMode mode)
    {
        return mode switch
        {
            ParameterMode.Source => "source",
            ParameterMode.Target => "target",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown parameter mode")
        };
    }

    public static string SerializeStructureMode(StructureMode mode)
    {
        return mode switch
        {
            StructureMode.Source => "source",
            StructureMode.Queried => "queried",
            StructureMode.Target => "target",
            StructureMode.Produced => "produced",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown structure mode")
        };
    }

    public static string SerializeGroupTypeMode(GroupTypeMode mode)
    {
        return mode switch
        {
            GroupTypeMode.Types => "types",
            GroupTypeMode.TypePlus => "type+",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown group type mode")
        };
    }

    public static string SerializeSourceListMode(SourceListMode mode)
    {
        return mode switch
        {
            SourceListMode.First => "first",
            SourceListMode.Last => "last",
            SourceListMode.NotFirst => "not_first",
            SourceListMode.NotLast => "not_last",
            SourceListMode.OnlyOne => "only_one",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown source list mode")
        };
    }

    public static string SerializeTargetListMode(TargetListMode mode)
    {
        return mode switch
        {
            TargetListMode.First => "first",
            TargetListMode.Share => "share",
            TargetListMode.Last => "last",
            TargetListMode.Single => "single",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown target list mode")
        };
    }

    public static string GetIndent(int level)
    {
        return string.Concat(Enumerable.Repeat(Indent, level));
    }

    /// <summary>
    /// Outputs hidden tokens if present, otherwise outputs default formatting.
    /// </summary>
    public static void OutputLeadingHiddenTokens(StringBuilder sb, FmlNode node, string defaultOutput)
    {
        if (node.LeadingHiddenTokens != null && node.LeadingHiddenTokens.Count > 0)
        {
            // Output captured tokens exactly as they were
            foreach (var token in node.LeadingHiddenTokens)
            {
                sb.Append(token.Text);
            }
        }
        else
        {
            // Use default formatting
            sb.Append(defaultOutput);
        }
    }

    /// <summary>
    /// Outputs trailing hidden tokens if present.
    /// </summary>
    public static void OutputTrailingHiddenTokens(StringBuilder sb, FmlNode node)
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
}

