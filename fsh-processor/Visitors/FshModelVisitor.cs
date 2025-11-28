using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using fsh_processor.antlr;
using fsh_processor.Models;

namespace fsh_processor.Visitors;

/// <summary>
/// Visitor implementation that builds the FSH object model from the ANTLR parse tree
/// with full source position tracking and hidden token preservation.
/// 
/// This is a minimal working implementation that demonstrates the pattern.
/// Additional entity types and rules can be added incrementally following these patterns.
/// </summary>
public class FshModelVisitor : FSHBaseVisitor<object?>
{
    private readonly CommonTokenStream _tokenStream;
    private readonly HashSet<int> _claimedTokenIndexes = new();

    public FshModelVisitor(CommonTokenStream tokenStream)
    {
        _tokenStream = tokenStream ?? throw new ArgumentNullException(nameof(tokenStream));
    }

    #region Document and Entity Root

    public override object? VisitDoc([NotNull] FSHParser.DocContext context)
    {
        var doc = new FshDoc
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
        };

        // Visit all entities
        foreach (var entityContext in context.entity())
        {
            var entity = Visit(entityContext) as FshEntity;
            if (entity != null)
            {
                doc.Entities.Add(entity);
            }
        }

        doc.TrailingHiddenTokens = GetEofHiddenTokens(context);
        return doc;
    }

    public override object? VisitEntity([NotNull] FSHParser.EntityContext context)
    {
        // Delegate to the specific entity type visitor
        if (context.alias() != null)
            return Visit(context.alias());
        if (context.profile() != null)
            return Visit(context.profile());
        if (context.extension() != null)
            return Visit(context.extension());
        if (context.logical() != null)
            return Visit(context.logical());
        if (context.resource() != null)
            return Visit(context.resource());
        if (context.instance() != null)
            return Visit(context.instance());
        if (context.invariant() != null)
            return Visit(context.invariant());
        if (context.valueSet() != null)
            return Visit(context.valueSet());
        if (context.codeSystem() != null)
            return Visit(context.codeSystem());
        if (context.ruleSet() != null)
            return Visit(context.ruleSet());
        if (context.mapping() != null)
            return Visit(context.mapping());

        return null;
    }

    #endregion

    #region Entity Visitors

    public override object? VisitAlias([NotNull] FSHParser.AliasContext context)
    {
        // Grammar: KW_ALIAS name EQUAL (SEQUENCE | CODE)
        var value = context.SEQUENCE() != null 
            ? context.SEQUENCE().GetText() 
            : context.CODE().GetText();

        return new Alias
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Name = context.name().GetText(),
            Value = value
        };
    }

    public override object? VisitProfile([NotNull] FSHParser.ProfileContext context)
    {
        // Grammar: KW_PROFILE name sdMetadata+ sdRule*
        var profile = new Profile
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context.name()),
            Name = context.name().GetText()
        };

        // Process metadata
        foreach (var metadata in context.sdMetadata())
        {
            ProcessSdMetadata(profile, metadata);
        }

        // Process rules
        FshRule? previousRule = null;
        foreach (var rule in context.sdRule())
        {
            var sdRule = Visit(rule) as FshRule;
            if (sdRule != null)
            {
                ExtractAndAttachInlineComment(rule, previousRule, sdRule);
                profile.Rules.Add(sdRule);
                previousRule = sdRule;
            }
        }

        return profile;
    }

    public override object? VisitExtension([NotNull] FSHParser.ExtensionContext context)
    {
        // Grammar: KW_EXTENSION name (sdMetadata | context)* sdRule*
        var extension = new Extension
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context.name()),
            Name = context.name().GetText()
        };

        // Process metadata
        foreach (var metadata in context.sdMetadata())
        {
            ProcessSdMetadata(extension, metadata);
        }

        // Process context
        foreach (var ctx in context.context())
        {
            ProcessExtensionContext(extension, ctx);
        }

        // Process rules
        FshRule? previousRule = null;
        foreach (var rule in context.sdRule())
        {
            var sdRule = Visit(rule) as FshRule;
            if (sdRule != null)
            {
                ExtractAndAttachInlineComment(rule, previousRule, sdRule);
                extension.Rules.Add(sdRule);
                previousRule = sdRule;
            }
        }

        return extension;
    }

    public override object? VisitLogical([NotNull] FSHParser.LogicalContext context)
    {
        // Grammar: KW_LOGICAL name (sdMetadata | characteristics)* lrRule*
        var logical = new Logical
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context.name()),
            Name = context.name().GetText()
        };

        // Process metadata
        foreach (var metadata in context.sdMetadata())
        {
            ProcessSdMetadata(logical, metadata);
        }

        // Process characteristics
        foreach (var chars in context.characteristics())
        {
            ProcessCharacteristics(logical, chars);
        }

        // Process rules
        FshRule? previousRule = null;
        foreach (var rule in context.lrRule())
        {
            var lrRule = Visit(rule) as LrRule;
            if (lrRule != null)
            {
                ExtractAndAttachInlineComment(rule, previousRule, lrRule);
                logical.Rules.Add(lrRule);
                previousRule = lrRule;
            }
        }

        return logical;
    }

    public override object? VisitResource([NotNull] FSHParser.ResourceContext context)
    {
        // Grammar: KW_RESOURCE name sdMetadata* lrRule*
        var resource = new Resource
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context.name()),
            Name = context.name().GetText()
        };

        // Process metadata
        foreach (var metadata in context.sdMetadata())
        {
            ProcessSdMetadata(resource, metadata);
        }

        // Process rules
        FshRule? previousRule = null;
        foreach (var rule in context.lrRule())
        {
            var lrRule = Visit(rule) as LrRule;
            if (lrRule != null)
            {
                ExtractAndAttachInlineComment(rule, previousRule, lrRule);
                resource.Rules.Add(lrRule);
                previousRule = lrRule;
            }
        }

        return resource;
    }

    public override object? VisitInstance([NotNull] FSHParser.InstanceContext context)
    {
        // Grammar: KW_INSTANCE name instanceMetadata* instanceRule*
        var instance = new Instance
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context.name()),
            Name = context.name().GetText()
        };

        // Process metadata
        foreach (var metadata in context.instanceMetadata())
        {
            ProcessInstanceMetadata(instance, metadata);
        }

        // Process rules
        foreach (var rule in context.instanceRule())
        {
            var instanceRule = Visit(rule) as InstanceRule;
            if (instanceRule != null)
            {
                instance.Rules.Add(instanceRule);
            }
        }

        return instance;
    }

    public override object? VisitInvariant([NotNull] FSHParser.InvariantContext context)
    {
        // Grammar: KW_INVARIANT name invariantMetadata* invariantRule*
        var invariant = new Invariant
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context.name()),
            Name = context.name().GetText()
        };

        // Process metadata
        foreach (var metadata in context.invariantMetadata())
        {
            ProcessInvariantMetadata(invariant, metadata);
        }

        // Process rules
        foreach (var rule in context.invariantRule())
        {
            var invariantRule = Visit(rule) as InvariantRule;
            if (invariantRule != null)
            {
                invariant.Rules.Add(invariantRule);
            }
        }

        return invariant;
    }

    public override object? VisitValueSet([NotNull] FSHParser.ValueSetContext context)
    {
        // Grammar: KW_VALUESET name vsMetadata* vsRule*
        var valueSet = new ValueSet
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context.name()),
            Name = context.name().GetText()
        };

        // Process metadata
        foreach (var metadata in context.vsMetadata())
        {
            ProcessVsMetadata(valueSet, metadata);
        }

        // Process rules
        foreach (var rule in context.vsRule())
        {
            var vsRule = Visit(rule) as VsRule;
            if (vsRule != null)
            {
                valueSet.Rules.Add(vsRule);
            }
        }

        return valueSet;
    }

    public override object? VisitCodeSystem([NotNull] FSHParser.CodeSystemContext context)
    {
        // Grammar: KW_CODESYSTEM name csMetadata* csRule*
        var codeSystem = new CodeSystem
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context.name()),
            Name = context.name().GetText()
        };

        // Process metadata
        foreach (var metadata in context.csMetadata())
        {
            ProcessCsMetadata(codeSystem, metadata);
        }

        // Process rules
        foreach (var rule in context.csRule())
        {
            var csRule = Visit(rule) as CsRule;
            if (csRule != null)
            {
                codeSystem.Rules.Add(csRule);
            }
        }

        return codeSystem;
    }

    public override object? VisitRuleSet([NotNull] FSHParser.RuleSetContext context)
    {
        // Grammar: KW_RULESET name (LPAREN ruleSetParamList? RPAREN paramRuleSetContent | ruleSetRule+);
        var ruleSet = new RuleSet
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            Name = context.name().GetText()
        };

        // Check if this is a parameterized ruleset (has LPAREN)
        if (context.LPAREN() != null)
        {
            // TODO: comments to the right of the ruleset parameters
            // ruleSet.TrailingHiddenTokens = GetTrailingHiddenTokens(context.RPAREN());

            // Parameterized RuleSet
            ruleSet.IsParameterized = true;
            
            // Extract parameters from ruleSetParamList
            var paramList = context.ruleSetParamList();
            if (paramList != null)
            {
                ruleSet.Parameters = ExtractRuleSetParameters(paramList);
            }
        }
        else
        {
            ruleSet.TrailingHiddenTokens = GetTrailingHiddenTokens(context.name());

            // Non-parameterized RuleSet
            // Process rules
            foreach (var rule in context.ruleSetRule())
            {
                var fshRule = Visit(rule) as FshRule;
                if (fshRule != null)
                {
                    ruleSet.Rules.Add(fshRule);
                }
            }
        }

        // Capture the unparsed content
        var contentContext = context.paramRuleSetContent();
        if (contentContext != null)
        {
            // Get the text from the first token to the last token of paramRuleSetContent
            var startToken = contentContext.Start;
            var stopToken = contentContext.Stop;

            if (startToken != null && stopToken != null)
            {
                // Use the token stream to get the exact text including whitespace
                ruleSet.UnparsedContent = _tokenStream.GetText(new Antlr4.Runtime.Misc.Interval(startToken.TokenIndex, stopToken.TokenIndex));
            }
        }
        return ruleSet;
    }
    
    
    public override object? VisitRuleSetParam([NotNull] FSHParser.RuleSetParamContext context)
    {
        // Grammar: ruleSetParam: MULTILINE_STRING | DOUBLE_BRACKET_STRING | STRING | ruleSetParamText;
        string value;
        bool isBracketed = false;
        
        if (context.MULTILINE_STRING() != null)
        {
            value = context.MULTILINE_STRING().GetText();
        }
        else if (context.DOUBLE_BRACKET_STRING() != null)
        {
            var text = context.DOUBLE_BRACKET_STRING().GetText();
            value = text.Substring(2, text.Length - 4); // Remove [[ and ]]
            // Handle escape sequences per spec: ]],\, or ]]\) should have the \ removed
            // "the ) or , following the ]] MUST be escaped with a backslash"
            // So we need to unescape ]],\ to ]],, and ]]\) to ]])
            value = value.Replace("]],\\", "]],").Replace("]]\\)", "]])");
            isBracketed = true;
        }
        else if (context.STRING() != null)
        {
            value = context.STRING().GetText();
        }
        else if (context.ruleSetParamText() != null)
        {
            value = ProcessRuleSetParamText(context.ruleSetParamText());
        }
        else
        {
            value = context.GetText();
        }
        
        return new RuleSetParameter
        {
            Position = GetPosition(context),
            Value = value,
            IsBracketed = isBracketed
        };
    }

    /// <summary>
    /// Extracts parameters from a ruleSetParamList, handling empty parameters between commas.
    /// Grammar: ruleSetParamList: (ruleSetParam | COMMA)+;
    /// Example: (param1,,param3) has 3 parameters where param2 is empty.
    /// </summary>
    private List<RuleSetParameter> ExtractRuleSetParameters(FSHParser.RuleSetParamListContext context)
    {
        var parameters = new List<RuleSetParameter>();
        var children = context.children;
        if (children == null) return parameters;

        RuleSetParameter? currentParam = null;
        
        foreach (var child in children)
        {
            if (child is ITerminalNode terminal && terminal.Symbol.Type == FSHLexer.COMMA)
            {
                // Hit a comma - finalize current parameter
                if (currentParam != null)
                {
                    parameters.Add(currentParam);
                    currentParam = null;
                }
                else
                {
                    // Empty parameter before this comma
                    parameters.Add(new RuleSetParameter
                    {
                        Position = GetPosition(terminal as ParserRuleContext),
                        Value = string.Empty,
                        IsBracketed = false
                    });
                }
            }
            else if (child is FSHParser.RuleSetParamContext paramContext)
            {
                currentParam = Visit(paramContext) as RuleSetParameter;
            }
        }
        
        // Add the last parameter (or empty if list ends with comma)
        if (currentParam != null)
        {
            parameters.Add(currentParam);
        }
        else if (children.Count > 0 && children[children.Count - 1] is ITerminalNode lastTerminal && lastTerminal.Symbol.Type == FSHLexer.COMMA)
        {
            // List ends with comma - add empty parameter
            parameters.Add(new RuleSetParameter
            {
                Value = string.Empty,
                IsBracketed = false
            });
        }
        
        return parameters;
    }

    /// <summary>
    /// Processes ruleSetParamText by concatenating tokens and handling escape sequences.
    /// Escape sequences: \) and \, where the backslash indicates the following character
    /// should be treated as literal content rather than a delimiter.
    /// </summary>
    private string ProcessRuleSetParamText(FSHParser.RuleSetParamTextContext context)
    {
        var result = new System.Text.StringBuilder();
        var parts = context.ruleSetParamPart();
        
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var text = part.GetText();
            
            // Check if this is an escape sequence pattern: SEQUENCE RPAREN or SEQUENCE COMMA
            // These appear as two children in the parse tree
            if (part.ChildCount == 2)
            {
                var firstChild = part.GetChild(0);
                var secondChild = part.GetChild(1);
                
                // If first child is \ and second is ) or ,
                if (firstChild?.GetText() == "\\")
                {
                    var secondText = secondChild?.GetText();
                    if (secondText == ")" || secondText == ",")
                    {
                        // This is an escape sequence - include the escaped character without the backslash
                        result.Append(secondText);
                        continue;
                    }
                }
            }
            
            // Normal token - just append its text
            result.Append(text);
        }
        
        return result.ToString();
    }

    public override object? VisitMapping([NotNull] FSHParser.MappingContext context)
    {
        // Grammar: KW_MAPPING name mappingMetadata* mappingEntityRule*
        var mapping = new Mapping
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context.name()),
            Name = context.name().GetText()
        };

        // Process metadata
        foreach (var metadata in context.mappingMetadata())
        {
            ProcessMappingMetadata(mapping, metadata);
        }

        // Process rules
        foreach (var rule in context.mappingEntityRule())
        {
            var mappingRule = Visit(rule) as MappingRule;
            if (mappingRule != null)
            {
                mapping.Rules.Add(mappingRule);
            }
        }

        return mapping;
    }

    #endregion

    #region Metadata Processors

    private void ProcessSdMetadata(Profile profile, FSHParser.SdMetadataContext context)
    {
        // Grammar: sdMetadata: parent | id | title | description;
        if (context.parent() != null)
        {
            profile.Parent = new Metadata() 
            {
                Value = context.parent().name().GetText(),
                TrailingHiddenTokens = GetTrailingHiddenTokens(context.parent().name()),
            };
            return;
        }
        if (context.id() != null)
        {
            profile.Id = new Metadata()
            {
                Value = context.id().name().GetText(),
                TrailingHiddenTokens = GetTrailingHiddenTokens(context.id().name()),
            };
            return;
        }
        if (context.title() != null)
        {
            profile.Title = new Metadata()
            {
                Value = ExtractString(context.title().STRING().GetText()),
                TrailingHiddenTokens = GetTrailingHiddenTokens(context.title()),
            };
            return;
        }
        var desc = context.description();
        if (desc != null)
        {
            string value;
            if (context.description().STRING() != null)
            {
                value = ExtractString(context.description().STRING().GetText());
            }
            else
            {
                value = ExtractMulitLineString(context.description().MULTILINE_STRING().GetText());
            }
            profile.Description = new Metadata()
            {
                Value = value
            };
        }
    }

    private void ProcessSdMetadata(Extension extension, FSHParser.SdMetadataContext context)
    {
        if (context.parent() != null)
        {
            extension.Parent = context.parent().name().GetText();
        }
        else if (context.id() != null)
        {
            extension.Id = context.id().name().GetText();
        }
        else if (context.title() != null)
        {
            extension.Title = ExtractString(context.title().STRING().GetText());
        }
        else if (context.description() != null)
        {
            extension.Description = context.description().STRING() != null
                ? ExtractString(context.description().STRING().GetText())
                : ExtractMulitLineString(context.description().MULTILINE_STRING().GetText());
        }
    }

    private void ProcessSdMetadata(Logical logical, FSHParser.SdMetadataContext context)
    {
        if (context.parent() != null)
        {
            logical.Parent = context.parent().name().GetText();
        }
        else if (context.id() != null)
        {
            logical.Id = context.id().name().GetText();
        }
        else if (context.title() != null)
        {
            logical.Title = ExtractString(context.title().STRING().GetText());
        }
        else if (context.description() != null)
        {
            logical.Description = context.description().STRING() != null
                ? ExtractString(context.description().STRING().GetText())
                : ExtractMulitLineString(context.description().MULTILINE_STRING().GetText());
        }
    }

    private void ProcessSdMetadata(Resource resource, FSHParser.SdMetadataContext context)
    {
        if (context.parent() != null)
        {
            resource.Parent = context.parent().name().GetText();
        }
        else if (context.id() != null)
        {
            resource.Id = context.id().name().GetText();
        }
        else if (context.title() != null)
        {
            resource.Title = ExtractString(context.title().STRING().GetText());
        }
        else if (context.description() != null)
        {
            resource.Description = context.description().STRING() != null
                ? ExtractString(context.description().STRING().GetText())
                : ExtractMulitLineString(context.description().MULTILINE_STRING().GetText());
        }
    }

    private void ProcessExtensionContext(Extension extension, FSHParser.ContextContext context)
    {
        // Grammar: context: KW_CONTEXT contextItem (COMMA contextItem)*;
        // Grammar: contextItem: STRING | SEQUENCE | CODE;
        var contexts = new List<Context>();
        
        foreach (var item in context.contextItem())
        {
            var isQuoted = item.STRING() != null;
            var text = isQuoted
                ? ExtractString(item.STRING().GetText())
                : item.SEQUENCE() != null 
                    ? item.SEQUENCE().GetText()
                    : item.CODE().GetText();
            contexts.Add(new Context { Value = text, IsQuoted = isQuoted });
        }

        if (contexts.Count > 0)
        {
            extension.Contexts = contexts;
        }
    }

    private void ProcessCharacteristics(Logical logical, FSHParser.CharacteristicsContext context)
    {
        // Grammar: characteristics: KW_CHARACTERISTICS code (COMMA code)*;
        var characteristics = new List<string>();

        foreach (var codeContext in context.code())
        {
            var code = Visit(codeContext) as Code;
            if (code != null)
            {
                characteristics.Add(code.Value);
            }
        }

        logical.Characteristics = characteristics.Count > 0 ? characteristics : null;
    }

    private void ProcessInstanceMetadata(Instance instance, FSHParser.InstanceMetadataContext context)
    {
        // Grammar: instanceMetadata: instanceOf | title | description | usage;
        if (context.instanceOf() != null)
        {
            instance.InstanceOf = context.instanceOf().name().GetText();
        }
        else if (context.title() != null)
        {
            instance.Title = ExtractString(context.title().STRING().GetText());
        }
        else if (context.description() != null)
        {
            instance.Description = context.description().STRING() != null
                ? ExtractString(context.description().STRING().GetText())
                : ExtractMulitLineString(context.description().MULTILINE_STRING().GetText());
        }
        else if (context.usage() != null)
        {
            instance.Usage = context.usage().CODE().GetText();
        }
    }

    private void ProcessInvariantMetadata(Invariant invariant, FSHParser.InvariantMetadataContext context)
    {
        // Grammar: invariantMetadata: description | expression | xpath | severity;
        if (context.description() != null)
        {
            invariant.Description = context.description().STRING() != null
                ? ExtractString(context.description().STRING().GetText())
                : ExtractMulitLineString(context.description().MULTILINE_STRING().GetText());
        }
        else if (context.expression() != null)
        {
            invariant.Expression = ExtractString(context.expression().STRING().GetText());
        }
        else if (context.xpath() != null)
        {
            invariant.XPath = ExtractString(context.xpath().STRING().GetText());
        }
        else if (context.severity() != null)
        {
            invariant.Severity = context.severity().CODE().GetText();
        }
    }

    private void ProcessVsMetadata(ValueSet valueSet, FSHParser.VsMetadataContext context)
    {
        // Grammar: vsMetadata: id | title | description;
        if (context.id() != null)
        {
            valueSet.Id = context.id().name().GetText();
        }
        else if (context.title() != null)
        {
            valueSet.Title = ExtractString(context.title().STRING().GetText());
        }
        else if (context.description() != null)
        {
            valueSet.Description = context.description().STRING() != null
                ? ExtractString(context.description().STRING().GetText())
                : ExtractMulitLineString(context.description().MULTILINE_STRING().GetText());
        }
    }

    private void ProcessCsMetadata(CodeSystem codeSystem, FSHParser.CsMetadataContext context)
    {
        // Grammar: csMetadata: id | title | description;
        if (context.id() != null)
        {
            codeSystem.Id = context.id().name().GetText();
        }
        else if (context.title() != null)
        {
            codeSystem.Title = ExtractString(context.title().STRING().GetText());
        }
        else if (context.description() != null)
        {
            codeSystem.Description = context.description().STRING() != null
                ? ExtractString(context.description().STRING().GetText())
                : ExtractMulitLineString(context.description().MULTILINE_STRING().GetText());
        }
    }

    private void ProcessMappingMetadata(Mapping mapping, FSHParser.MappingMetadataContext context)
    {
        // Grammar: mappingMetadata: id | source | target | description | title;
        if (context.id() != null)
        {
            mapping.Id = context.id().name().GetText();
        }
        else if (context.source() != null)
        {
            mapping.Source = context.source().name().GetText();
        }
        else if (context.target() != null)
        {
            mapping.Target = ExtractString(context.target().STRING().GetText());
        }
        else if (context.title() != null)
        {
            mapping.Title = ExtractString(context.title().STRING().GetText());
        }
        else if (context.description() != null)
        {
            mapping.Description = context.description().STRING() != null
                ? ExtractString(context.description().STRING().GetText())
                : ExtractMulitLineString(context.description().MULTILINE_STRING().GetText());
        }
    }

    #endregion

    #region SD Rule Visitors

    public override object? VisitSdRule([NotNull] FSHParser.SdRuleContext context)
    {
        // Grammar: sdRule: cardRule | flagRule | valueSetRule | fixedValueRule | containsRule | onlyRule | obeysRule | caretValueRule | insertRule | pathRule;
        if (context.cardRule() != null)
            return Visit(context.cardRule());
        if (context.flagRule() != null)
            return Visit(context.flagRule());
        if (context.valueSetRule() != null)
            return Visit(context.valueSetRule());
        if (context.fixedValueRule() != null)
            return Visit(context.fixedValueRule());
        if (context.containsRule() != null)
            return Visit(context.containsRule());
        if (context.onlyRule() != null)
            return Visit(context.onlyRule());
        if (context.obeysRule() != null)
            return Visit(context.obeysRule());
        if (context.caretValueRule() != null)
            return Visit(context.caretValueRule());
        if (context.insertRule() != null)
            return Visit(context.insertRule());
        if (context.pathRule() != null)
            return Visit(context.pathRule());

        return null;
    }

    private string GetRuleIndent(ITerminalNode node)
    {
        var text = node.GetText();
        var newlineIndex = text.IndexOfAny(new[] { '\r', '\n' });
        text = text.TrimEnd(); // remove any trailing whitespace
        text = text.TrimEnd('*'); // remove the star token
        if (newlineIndex >= 0)
        {
            text = text.Substring(newlineIndex).TrimStart('\r','\n'); // remove any leading newlines
        }
        return text;
    }

    public override object? VisitCardRule([NotNull] FSHParser.CardRuleContext context)
    {
        // Grammar: cardRule: STAR path CARD flag*;
        var flags = new List<string>();
        foreach (var flag in context.flag())
        {
            flags.Add(flag.GetText());
        }
        var lastFlag = context.flag().LastOrDefault();
        return new CardRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = lastFlag != null ? GetTrailingHiddenTokens(lastFlag) : GetTrailingHiddenTokens(context),
            Path = context.path().GetText(),
            Indent = GetRuleIndent(context.STAR()),
            Cardinality = context.CARD().GetText(),
            Flags = flags
        };
    }

    public override object? VisitFlagRule([NotNull] FSHParser.FlagRuleContext context)
    {
        // Grammar: flagRule: STAR path (KW_AND path)* flag+;
        var additionalPaths = new List<string>();
        foreach (var pathContext in context.path())
        {
            if (pathContext != context.path(0)) // Skip first path (that's the main Path property)
            {
                additionalPaths.Add(pathContext.GetText());
            }
        }

        var flags = new List<string>();
        foreach (var flag in context.flag())
        {
            flags.Add(flag.GetText());
        }

        return new FlagRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path(0).GetText(),
            Indent = GetRuleIndent(context.STAR()),
            AdditionalPaths = additionalPaths,
            Flags = flags
        };
    }

    public override object? VisitValueSetRule([NotNull] FSHParser.ValueSetRuleContext context)
    {
        // Grammar: valueSetRule: STAR path KW_FROM name strength?;
        return new ValueSetRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path().GetText(),
            Indent = GetRuleIndent(context.STAR()),
            ValueSetName = context.name().GetText(),
            Strength = context.strength()?.GetText()
        };
    }

    public override object? VisitFixedValueRule([NotNull] FSHParser.FixedValueRuleContext context)
    {
        // Grammar: fixedValueRule: STAR path EQUAL value KW_EXACTLY?;
        var value = Visit(context.value()) as FshValue ?? new StringValue { Value = context.value().GetText() };
        
        return new FixedValueRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path().GetText(),
            Indent = GetRuleIndent(context.STAR()),
            Value = value,
            Exactly = context.KW_EXACTLY() != null
        };
    }

    public override object? VisitContainsRule([NotNull] FSHParser.ContainsRuleContext context)
    {
        // Grammar: containsRule: STAR path KW_CONTAINS item (KW_AND item)*;
        var items = new List<ContainsItem>();
        foreach (var itemContext in context.item())
        {
            var item = Visit(itemContext) as ContainsItem;
            if (item != null)
            {
                items.Add(item);
            }
        }

        return new ContainsRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path().GetText(),
            Indent = GetRuleIndent(context.STAR()),
            Items = items
        };
    }

    public override object? VisitItem([NotNull] FSHParser.ItemContext context)
    {
        // Grammar: item: name (KW_NAMED name)? CARD flag*;
        var flags = new List<string>();
        foreach (var flag in context.flag())
        {
            flags.Add(flag.GetText());
        }

        return new ContainsItem
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Name = context.name(0).GetText(),
            NamedAlias = context.KW_NAMED() != null ? context.name(1).GetText() : null,
            Cardinality = context.CARD().GetText(),
            Flags = flags
        };
    }

    public override object? VisitOnlyRule([NotNull] FSHParser.OnlyRuleContext context)
    {
        // Grammar: onlyRule: STAR path KW_ONLY targetType (KW_OR targetType)*;
        var types = new List<string>();
        foreach (var targetType in context.targetType())
        {
            types.Add(targetType.GetText());
        }

        return new OnlyRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path().GetText(),
            Indent = GetRuleIndent(context.STAR()),
            TargetTypes = types
        };
    }

    public override object? VisitObeysRule([NotNull] FSHParser.ObeysRuleContext context)
    {
        // Grammar: obeysRule: STAR path? KW_OBEYS name (KW_AND name)*;
        var invariants = new List<string>();
        foreach (var nameContext in context.name())
        {
            invariants.Add(nameContext.GetText());
        }

        return new ObeysRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path()?.GetText(),
            Indent = GetRuleIndent(context.STAR()),
            InvariantNames = invariants
        };
    }

    public override object? VisitCaretValueRule([NotNull] FSHParser.CaretValueRuleContext context)
    {
        // Grammar: caretValueRule: STAR path? caretPath EQUAL value;
        var value = Visit(context.value()) as FshValue ?? new StringValue { Value = context.value().GetText() };

        return new CaretValueRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path()?.GetText(),
            Indent = GetRuleIndent(context.STAR()),
            CaretPath = context.caretPath().GetText(),
            Value = value
        };
    }

    public override object? VisitInsertRule([NotNull] FSHParser.InsertRuleContext context)
    {
        // Grammar: insertRule: STAR path? KW_INSERT ruleSetInsert;
        // Grammar: ruleSetInsert: name (LPAREN ruleSetParamList? RPAREN)?;
        var ruleSetInsert = context.ruleSetInsert();
        var name = ruleSetInsert.name().GetText();
        
        List<string>? parameters = null;
        var paramList = ruleSetInsert.ruleSetParamList();
        if (paramList != null)
        {
            var ruleSetParams = ExtractRuleSetParameters(paramList);
            parameters = ruleSetParams.Select(p => p.Value).ToList();
        }

        return new InsertRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path()?.GetText(),
            Indent = GetRuleIndent(context.STAR()),
            RuleSetReference = name,
            Parameters = parameters,
            IsParameterized = parameters != null && parameters.Count > 0
        };
    }

    public override object? VisitPathRule([NotNull] FSHParser.PathRuleContext context)
    {
        // Grammar: pathRule: STAR path;
        return new PathRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path().GetText(),
            Indent = GetRuleIndent(context.STAR()),
        };
    }

    #endregion

    #region LR Rule Visitors

    public override object? VisitLrRule([NotNull] FSHParser.LrRuleContext context)
    {
        // Grammar: lrRule: sdRule | addElementRule | addCRElementRule;
        if (context.sdRule() != null)
        {
            // For LR rules, we need to convert SD rules to LR equivalents
            var sdRule = Visit(context.sdRule());
            
            // Convert CardRule to LrCardRule
            if (sdRule is CardRule cardRule)
            {
                return new LrCardRule
                {
                    Position = cardRule.Position,
                    LeadingHiddenTokens = cardRule.LeadingHiddenTokens,
                    TrailingHiddenTokens = cardRule.TrailingHiddenTokens,
                    Path = cardRule.Path,
                    Indent = cardRule.Indent,
                    Cardinality = cardRule.Cardinality,
                    Flags = cardRule.Flags
                };
            }
            
            // Convert FlagRule to LrFlagRule
            if (sdRule is FlagRule flagRule)
            {
                return new LrFlagRule
                {
                    Position = flagRule.Position,
                    LeadingHiddenTokens = flagRule.LeadingHiddenTokens,
                    TrailingHiddenTokens = flagRule.TrailingHiddenTokens,
                    Path = flagRule.Path,
                    Indent = flagRule.Indent,
                    AdditionalPaths = flagRule.AdditionalPaths,
                    Flags = flagRule.Flags
                };
            }

            // Other SD rules can be returned as-is (they're valid in LR context too)
            return sdRule;
        }

        if (context.addElementRule() != null)
            return Visit(context.addElementRule());
        if (context.addCRElementRule() != null)
            return Visit(context.addCRElementRule());

        return null;
    }

    public override object? VisitAddElementRule([NotNull] FSHParser.AddElementRuleContext context)
    {
        // Grammar: addElementRule: STAR path CARD flag* targetType (KW_OR targetType)* STRING (STRING | MULTILINE_STRING)?;
        var flags = new List<string>();
        foreach (var flag in context.flag())
        {
            flags.Add(flag.GetText());
        }

        var targetTypes = new List<string>();
        foreach (var targetType in context.targetType())
        {
            targetTypes.Add(targetType.GetText());
        }

        string? shortDescription = null;
        string? definition = null;

        var strings = context.STRING();
        if (strings != null && strings.Length > 0)
        {
            shortDescription = ExtractString(strings[0].GetText());
            if (strings.Length > 1)
            {
                definition = ExtractString(strings[1].GetText());
            }
        }

        if (definition == null && context.MULTILINE_STRING() != null)
        {
            definition = ExtractMulitLineString(context.MULTILINE_STRING().GetText());
        }

        return new AddElementRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path().GetText(),
            Indent = GetRuleIndent(context.STAR()),
            Cardinality = context.CARD().GetText(),
            Flags = flags,
            TargetTypes = targetTypes,
            ShortDescription = shortDescription,
            Definition = definition
        };
    }

    public override object? VisitAddCRElementRule([NotNull] FSHParser.AddCRElementRuleContext context)
    {
        // Grammar: addCRElementRule: STAR path CARD flag* KW_CONTENTREFERENCE (SEQUENCE | CODE) STRING (STRING | MULTILINE_STRING)?;
        var flags = new List<string>();
        foreach (var flag in context.flag())
        {
            flags.Add(flag.GetText());
        }

        var contentReference = context.SEQUENCE() != null
            ? context.SEQUENCE().GetText()
            : context.CODE().GetText();

        string? shortDescription = null;
        string? definition = null;

        var strings = context.STRING();
        if (strings != null && strings.Length > 0)
        {
            shortDescription = ExtractString(strings[0].GetText());
            if (strings.Length > 1)
            {
                definition = ExtractString(strings[1].GetText());
            }
        }

        if (definition == null && context.MULTILINE_STRING() != null)
        {
            definition = ExtractMulitLineString(context.MULTILINE_STRING().GetText());
        }

        return new AddCRElementRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path().GetText(),
            Indent = GetRuleIndent(context.STAR()),
            Cardinality = context.CARD().GetText(),
            Flags = flags,
            ContentReference = contentReference,
            ShortDescription = shortDescription,
            Definition = definition
        };
    }

    #endregion

    #region Instance/Invariant Rule Visitors

    public override object? VisitInstanceRule([NotNull] FSHParser.InstanceRuleContext context)
    {
        // Grammar: instanceRule: fixedValueRule | insertRule | pathRule;
        var rule = Visit(context.GetChild(0));
        
        // Convert to instance-specific rule types
        if (rule is FixedValueRule fvr)
        {
            return new InstanceFixedValueRule
            {
                Position = fvr.Position,
                LeadingHiddenTokens = fvr.LeadingHiddenTokens,
                TrailingHiddenTokens = fvr.TrailingHiddenTokens,
                Path = fvr.Path,
                Indent = fvr.Indent,
                Value = fvr.Value,
                Exactly = fvr.Exactly
            };
        }
        if (rule is InsertRule ir)
        {
            return new InstanceInsertRule
            {
                Position = ir.Position,
                LeadingHiddenTokens = ir.LeadingHiddenTokens,
                TrailingHiddenTokens = ir.TrailingHiddenTokens,
                Path = ir.Path,
                Indent = ir.Indent,
                RuleSetReference = ir.RuleSetReference,
                Parameters = ir.Parameters,
                IsParameterized = ir.IsParameterized
            };
        }
        if (rule is PathRule pr)
        {
            return new InstancePathRule
            {
                Position = pr.Position,
                LeadingHiddenTokens = pr.LeadingHiddenTokens,
                TrailingHiddenTokens = pr.TrailingHiddenTokens,
                Path = pr.Path,
                Indent = pr.Indent
            };
        }

        return rule;
    }

    public override object? VisitInvariantRule([NotNull] FSHParser.InvariantRuleContext context)
    {
        // Grammar: invariantRule: fixedValueRule | insertRule | pathRule;
        var rule = Visit(context.GetChild(0));
        
        // Convert to invariant-specific rule types
        if (rule is FixedValueRule fvr)
        {
            return new InvariantFixedValueRule
            {
                Position = fvr.Position,
                LeadingHiddenTokens = fvr.LeadingHiddenTokens,
                TrailingHiddenTokens = fvr.TrailingHiddenTokens,
                Path = fvr.Path,
                Indent = fvr.Indent,
                Value = fvr.Value,
                Exactly = fvr.Exactly
            };
        }
        if (rule is InsertRule ir)
        {
            return new InvariantInsertRule
            {
                Position = ir.Position,
                LeadingHiddenTokens = ir.LeadingHiddenTokens,
                TrailingHiddenTokens = ir.TrailingHiddenTokens,
                Path = ir.Path,
                Indent = ir.Indent,
                RuleSetReference = ir.RuleSetReference,
                Parameters = ir.Parameters,
                IsParameterized = ir.IsParameterized
            };
        }
        if (rule is PathRule pr)
        {
            return new InvariantPathRule
            {
                Position = pr.Position,
                LeadingHiddenTokens = pr.LeadingHiddenTokens,
                TrailingHiddenTokens = pr.TrailingHiddenTokens,
                Path = pr.Path,
                Indent = pr.Indent
            };
        }

        return rule;
    }

    #endregion

    #region ValueSet Rule Visitors

    public override object? VisitVsRule([NotNull] FSHParser.VsRuleContext context)
    {
        // Grammar: vsRule: vsComponent | caretValueRule | codeCaretValueRule | insertRule | codeInsertRule;
        if (context.vsComponent() != null)
            return Visit(context.vsComponent());
        if (context.caretValueRule() != null)
        {
            // Convert to VsCaretValueRule
            var caretRule = Visit(context.caretValueRule()) as CaretValueRule;
            if (caretRule != null)
            {
                return new VsCaretValueRule
                {
                    Position = caretRule.Position,
                    LeadingHiddenTokens = caretRule.LeadingHiddenTokens,
                    TrailingHiddenTokens = caretRule.TrailingHiddenTokens,
                    Path = caretRule.Path,
                    Indent = caretRule.Indent,
                    CaretPath = caretRule.CaretPath,
                    Value = caretRule.Value
                };
            }
        }
        if (context.codeCaretValueRule() != null)
            return Visit(context.codeCaretValueRule());
        if (context.insertRule() != null)
        {
            // Convert to VsInsertRule
            var insertRule = Visit(context.insertRule()) as InsertRule;
            if (insertRule != null)
            {
                return new VsInsertRule
                {
                    Position = insertRule.Position,
                    LeadingHiddenTokens = insertRule.LeadingHiddenTokens,
                    TrailingHiddenTokens = insertRule.TrailingHiddenTokens,
                    Path = insertRule.Path,
                    Indent = insertRule.Indent,
                    RuleSetReference = insertRule.RuleSetReference,
                    Parameters = insertRule.Parameters,
                    IsParameterized = insertRule.IsParameterized
                };
            }
        }
        if (context.codeInsertRule() != null)
            return Visit(context.codeInsertRule());

        return null;
    }

    public override object? VisitCodeCaretValueRule([NotNull] FSHParser.CodeCaretValueRuleContext context)
    {
        // Grammar: codeCaretValueRule: STAR CODE* caretPath EQUAL value;
        var codes = new List<string>();
        foreach (var code in context.CODE())
        {
            codes.Add(code.GetText());
        }

        var value = Visit(context.value()) as FshValue ?? new StringValue { Value = context.value().GetText() };

        return new CodeCaretValueRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Codes = codes,
            CaretPath = context.caretPath().GetText(),
            Indent = GetRuleIndent(context.STAR()),
            Value = value
        };
    }

    public override object? VisitCodeInsertRule([NotNull] FSHParser.CodeInsertRuleContext context)
    {
        // Grammar: codeInsertRule: STAR CODE* KW_INSERT ruleSetInsert;
        // Grammar: ruleSetInsert: name (LPAREN ruleSetParamList? RPAREN)?;
        var codes = new List<string>();
        foreach (var code in context.CODE())
        {
            codes.Add(code.GetText());
        }

        var ruleSetInsert = context.ruleSetInsert();
        var name = ruleSetInsert.name().GetText();
        
        List<string>? parameters = null;
        var paramList = ruleSetInsert.ruleSetParamList();
        if (paramList != null)
        {
            var ruleSetParams = ExtractRuleSetParameters(paramList);
            parameters = ruleSetParams.Select(p => p.Value).ToList();
        }

        return new CodeInsertRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Indent = GetRuleIndent(context.STAR()),
            Codes = codes,
            RuleSetReference = name,
            Parameters = parameters,
            IsParameterized = parameters != null && parameters.Count > 0
        };
    }

    public override object? VisitVsComponent([NotNull] FSHParser.VsComponentContext context)
    {
        // Grammar: vsComponent: STAR (KW_INCLUDE | KW_EXCLUDE)? (vsConceptComponent | vsFilterComponent);
        bool? isInclude = null;
        if (context.KW_INCLUDE() != null)
            isInclude = true;
        else if (context.KW_EXCLUDE() != null)
            isInclude = false;

        var indent = GetRuleIndent(context.STAR());

        if (context.vsConceptComponent() != null)
            return VisitVsConceptComponent(context.vsConceptComponent(), isInclude, indent);
        if (context.vsFilterComponent() != null)
            return VisitVsFilterComponent(context.vsFilterComponent(), isInclude, indent);

        return null;
    }

    private VsComponentRule? VisitVsConceptComponent(FSHParser.VsConceptComponentContext context, bool? isInclude, string indent)
    {
        // Grammar: vsConceptComponent: code vsComponentFrom?;
        var code = Visit(context.code()) as Code;
        
        string? fromSystem = null;
        List<string>? fromValueSets = null;

        if (context.vsComponentFrom() != null)
        {
            ExtractVsComponentFrom(context.vsComponentFrom(), out fromSystem, out fromValueSets);
        }

        return new VsComponentRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Indent = indent,
            IsInclude = isInclude,
            IsConceptComponent = true,
            ConceptCode = code,
            FromSystem = fromSystem,
            FromValueSets = fromValueSets ?? new List<string>()
        };
    }

    private VsComponentRule? VisitVsFilterComponent(FSHParser.VsFilterComponentContext context, bool? isInclude, string indent)
    {
        // Grammar: vsFilterComponent: KW_CODES vsComponentFrom (KW_WHERE vsFilterList)?;
        string? fromSystem = null;
        List<string>? fromValueSets = null;

        if (context.vsComponentFrom() != null)
        {
            ExtractVsComponentFrom(context.vsComponentFrom(), out fromSystem, out fromValueSets);
        }

        var filters = new List<VsFilterDefinition>();
        if (context.vsFilterList() != null)
        {
            foreach (var filterDef in context.vsFilterList().vsFilterDefinition())
            {
                var property = filterDef.name().GetText();
                var op = filterDef.vsFilterOperator().GetText();
                var value = filterDef.vsFilterValue() != null
                    ? Visit(filterDef.vsFilterValue()) as FshValue
                    : null;

                filters.Add(new VsFilterDefinition
                {
                    Position = GetPosition(filterDef),
                    Property = property,
                    Operator = op,
                    Value = value
                });
            }
        }

        return new VsComponentRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Indent = indent,
            IsInclude = isInclude,
            IsConceptComponent = false,
            FromSystem = fromSystem,
            FromValueSets = fromValueSets ?? new List<string>(),
            Filters = filters
        };
    }

    private void ExtractVsComponentFrom(FSHParser.VsComponentFromContext context, out string? fromSystem, out List<string>? fromValueSets)
    {
        // Grammar: vsComponentFrom: KW_FROM (vsFromSystem (KW_AND vsFromValueset)? | vsFromValueset (KW_AND vsFromSystem)?);
        fromSystem = null;
        fromValueSets = null;

        if (context.vsFromSystem() != null)
        {
            fromSystem = context.vsFromSystem().name().GetText();
        }

        if (context.vsFromValueset() != null)
        {
            fromValueSets = new List<string>();
            foreach (var name in context.vsFromValueset().name())
            {
                fromValueSets.Add(name.GetText());
            }
        }
    }

    public override object? VisitVsFilterValue([NotNull] FSHParser.VsFilterValueContext context)
    {
        // Grammar: vsFilterValue: code | KW_TRUE | KW_FALSE | REGEX | STRING;
        if (context.code() != null)
            return Visit(context.code());
        if (context.KW_TRUE() != null)
            return new BooleanValue { Position = GetPosition(context), Value = true };
        if (context.KW_FALSE() != null)
            return new BooleanValue { Position = GetPosition(context), Value = false };
        if (context.REGEX() != null)
            return new RegexValue { Position = GetPosition(context), Pattern = context.REGEX().GetText() };
        if (context.STRING() != null)
            return new StringValue { Position = GetPosition(context), Value = ExtractString(context.STRING().GetText()) };

        return null;
    }

    #endregion

    #region CodeSystem Rule Visitors

    public override object? VisitCsRule([NotNull] FSHParser.CsRuleContext context)
    {
        // Grammar: csRule: concept | codeCaretValueRule | codeInsertRule;
        if (context.concept() != null)
            return Visit(context.concept());
        if (context.codeCaretValueRule() != null)
        {
            // Convert to CsCaretValueRule
            var codeCaretRule = Visit(context.codeCaretValueRule()) as CodeCaretValueRule;
            if (codeCaretRule != null)
            {
                return new CsCaretValueRule
                {
                    Position = codeCaretRule.Position,
                    LeadingHiddenTokens = codeCaretRule.LeadingHiddenTokens,
                    TrailingHiddenTokens = codeCaretRule.TrailingHiddenTokens,
                    Indent = codeCaretRule.Indent,
                    Codes = codeCaretRule.Codes,
                    CaretPath = codeCaretRule.CaretPath,
                    Value = codeCaretRule.Value
                };
            }
        }
        if (context.codeInsertRule() != null)
        {
            // Convert to CsInsertRule
            var codeInsertRule = Visit(context.codeInsertRule()) as CodeInsertRule;
            if (codeInsertRule != null)
            {
                return new CsInsertRule
                {
                    Position = codeInsertRule.Position,
                    LeadingHiddenTokens = codeInsertRule.LeadingHiddenTokens,
                    TrailingHiddenTokens = codeInsertRule.TrailingHiddenTokens,
                    Indent = codeInsertRule.Indent,
                    Codes = codeInsertRule.Codes,
                    RuleSetReference = codeInsertRule.RuleSetReference,
                    Parameters = codeInsertRule.Parameters,
                    IsParameterized = codeInsertRule.IsParameterized
                };
            }
        }

        return null;
    }

    public override object? VisitConcept([NotNull] FSHParser.ConceptContext context)
    {
        // Grammar: concept: STAR CODE+ STRING? (STRING | MULTILINE_STRING)?;
        var codes = new List<string>();
        foreach (var code in context.CODE())
        {
            codes.Add(code.GetText());
        }

        string? display = null;
        string? definition = null;

        var strings = context.STRING();
        if (strings != null && strings.Length > 0)
        {
            display = ExtractString(strings[0].GetText());
            if (strings.Length > 1)
            {
                definition = ExtractString(strings[1].GetText());
            }
        }

        if (definition == null && context.MULTILINE_STRING() != null)
        {
            definition = ExtractMulitLineString(context.MULTILINE_STRING().GetText());
        }

        return new Concept
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Indent = GetRuleIndent(context.STAR()),
            Codes = codes,
            Display = display,
            Definition = definition
        };
    }

    #endregion

    #region Mapping Rule Visitors

    public override object? VisitMappingEntityRule([NotNull] FSHParser.MappingEntityRuleContext context)
    {
        // Grammar: mappingEntityRule: mappingRule | insertRule | pathRule;
        if (context.mappingRule() != null)
            return Visit(context.mappingRule());
        if (context.insertRule() != null)
        {
            // Convert to MappingInsertRule
            var insertRule = Visit(context.insertRule()) as InsertRule;
            if (insertRule != null)
            {
                return new MappingInsertRule
                {
                    Position = insertRule.Position,
                    LeadingHiddenTokens = insertRule.LeadingHiddenTokens,
                    TrailingHiddenTokens = insertRule.TrailingHiddenTokens,
                    Path = insertRule.Path,
                    Indent = insertRule.Indent,
                    RuleSetReference = insertRule.RuleSetReference,
                    Parameters = insertRule.Parameters,
                    IsParameterized = insertRule.IsParameterized
                };
            }
        }
        if (context.pathRule() != null)
        {
            // Convert to MappingPathRule
            var pathRule = Visit(context.pathRule()) as PathRule;
            if (pathRule != null)
            {
                return new MappingPathRule
                {
                    Position = pathRule.Position,
                    LeadingHiddenTokens = pathRule.LeadingHiddenTokens,
                    TrailingHiddenTokens = pathRule.TrailingHiddenTokens,
                    Path = pathRule.Path,
                    Indent = pathRule.Indent
                };
            }
        }

        return null;
    }

    public override object? VisitMappingRule([NotNull] FSHParser.MappingRuleContext context)
    {
        // Grammar: mappingRule: STAR path? ARROW STRING STRING? CODE?;
        var target = ExtractString(context.STRING(0).GetText());
        string? language = null;
        string? code = null;

        if (context.STRING().Length > 1)
        {
            language = ExtractString(context.STRING(1).GetText());
        }

        if (context.CODE() != null)
        {
            code = context.CODE().GetText();
        }

        return new MappingMapRule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.path()?.GetText(),
            Indent = GetRuleIndent(context.STAR()),
            Target = target,
            Language = language,
            Code = code
        };
    }

    #endregion

    #region RuleSet Rule Visitors

    public override object? VisitRuleSetRule([NotNull] FSHParser.RuleSetRuleContext context)
    {
        // Grammar: ruleSetRule: sdRule | addElementRule | addCRElementRule | concept | codeCaretValueRule | codeInsertRule | vsComponent | mappingRule;
        // RuleSet rules can contain any type - delegate to appropriate visitor
        return Visit(context.GetChild(0));
    }

    #endregion

    #region Value Visitors

    public override object? VisitValue([NotNull] FSHParser.ValueContext context)
    {
        // Grammar: value: STRING | MULTILINE_STRING | NUMBER | DATETIME | TIME | reference | canonical | code | quantity | ratio | bool | name;
        if (context.STRING() != null)
        {
            return new StringValue
            {
                Position = GetPosition(context),
                Value = ExtractString(context.STRING().GetText())
            };
        }
        if (context.MULTILINE_STRING() != null)
        {
            return new StringValue
            {
                Position = GetPosition(context),
                Value = ExtractMulitLineString(context.MULTILINE_STRING().GetText()),
                IsMultiline = true
            };
        }
        if (context.NUMBER() != null)
        {
            return new NumberValue
            {
                Position = GetPosition(context),
                Value = decimal.Parse(context.NUMBER().GetText())
            };
        }
        if (context.DATETIME() != null)
        {
            return new DateTimeValue
            {
                Position = GetPosition(context),
                Value = context.DATETIME().GetText()
            };
        }
        if (context.TIME() != null)
        {
            return new TimeValue
            {
                Position = GetPosition(context),
                Value = context.TIME().GetText()
            };
        }
        if (context.code() != null)
        {
            return Visit(context.code());
        }
        if (context.quantity() != null)
        {
            return Visit(context.quantity());
        }
        if (context.ratio() != null)
        {
            return Visit(context.ratio());
        }
        if (context.reference() != null)
        {
            return Visit(context.reference());
        }
        if (context.canonical() != null)
        {
            return Visit(context.canonical());
        }
        if (context.name() != null)
        {
            var nameText = context.name().GetText();
            // Check if this is true/false boolean value
            if (nameText.Equals("true", StringComparison.OrdinalIgnoreCase) || nameText.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return new BooleanValue
                {
                    Position = GetPosition(context),
                    Value = nameText.Equals("true", StringComparison.OrdinalIgnoreCase)
                };
            }
            
            return new NameValue
            {
                Position = GetPosition(context),
                Value = nameText
            };
        }

        return new StringValue { Position = GetPosition(context), Value = context.GetText() };
    }

    public override object? VisitCode([NotNull] FSHParser.CodeContext context)
    {
        // Grammar: code: CODE STRING?;
        return new Code
        {
            Position = GetPosition(context),
            Value = context.CODE().GetText(),
            Display = context.STRING() != null ? ExtractString(context.STRING().GetText()) : null
        };
    }

    public override object? VisitQuantity([NotNull] FSHParser.QuantityContext context)
    {
        // Grammar: quantity: NUMBER? (UNIT | CODE) STRING?;
        var number = context.NUMBER() != null ? context.NUMBER().GetText() : "1";
        var unit = context.UNIT() != null ? context.UNIT().GetText() : context.CODE().GetText();
        
        return new Quantity
        {
            Position = GetPosition(context),
            Value = decimal.Parse(number),
            Unit = unit,
            Display = context.STRING() != null ? ExtractString(context.STRING().GetText()) : null
        };
    }

    public override object? VisitRatio([NotNull] FSHParser.RatioContext context)
    {
        // Grammar: ratio: ratioPart COLON ratioPart;
        var numerator = Visit(context.ratioPart(0)) as RatioPart ?? new RatioPart();
        var denominator = Visit(context.ratioPart(1)) as RatioPart ?? new RatioPart();

        return new Ratio
        {
            Position = GetPosition(context),
            Numerator = numerator,
            Denominator = denominator
        };
    }

    public override object? VisitRatioPart([NotNull] FSHParser.RatioPartContext context)
    {
        // Grammar: ratioPart: NUMBER | quantity;
        if (context.NUMBER() != null)
        {
            return new RatioPart
            {
                Value = decimal.Parse(context.NUMBER().GetText())
            };
        }
        if (context.quantity() != null)
        {
            var quantity = Visit(context.quantity()) as Quantity;
            if (quantity != null)
            {
                return new RatioPart
                {
                    QuantityValue = quantity
                };
            }
        }

        return new RatioPart();
    }

    public override object? VisitReference([NotNull] FSHParser.ReferenceContext context)
    {
        // Grammar: reference: REFERENCE STRING?;
        var refText = context.REFERENCE().GetText();
        
        // REFERENCE token format: Reference(Type) or Reference(url)
        // Extract the content between parentheses
        var startParen = refText.IndexOf('(');
        var endParen = refText.LastIndexOf(')');
        var referenceType = startParen >= 0 && endParen > startParen
            ? refText.Substring(startParen + 1, endParen - startParen - 1)
            : refText;

        return new Reference
        {
            Position = GetPosition(context),
            Type = referenceType,
            Display = context.STRING() != null ? ExtractString(context.STRING().GetText()) : null
        };
    }

    public override object? VisitCanonical([NotNull] FSHParser.CanonicalContext context)
    {
        // Grammar: canonical: CANONICAL;
        var canonicalText = context.CANONICAL().GetText();
        
        // CANONICAL token format: Canonical(url) or Canonical(url|version)
        var startParen = canonicalText.IndexOf('(');
        var endParen = canonicalText.LastIndexOf(')');
        var content = startParen >= 0 && endParen > startParen
            ? canonicalText.Substring(startParen + 1, endParen - startParen - 1)
            : canonicalText;

        // Check for version separator
        var parts = content.Split('|', 2);
        
        return new Canonical
        {
            Position = GetPosition(context),
            Url = parts[0],
            Version = parts.Length > 1 ? parts[1] : null
        };
    }

    #endregion

    #region Helper Methods

    private static SourcePosition? GetPosition(ParserRuleContext? context)
    {
        if (context == null) return null;

        var start = context.Start;
        var stop = context.Stop ?? start;

        return new SourcePosition
        {
            StartLine = start.Line,
            StartColumn = start.Column,
            EndLine = stop.Line,
            EndColumn = stop.Column + (stop.Text?.Length ?? 0),
            StartIndex = start.StartIndex,
            EndIndex = stop.StopIndex
        };
    }

    private static string ExtractString(string quotedString)
    {
        // Remove quotes from strings
        if (quotedString.StartsWith("\"") && quotedString.EndsWith("\"") && quotedString.Length >= 2)
        {
            return quotedString[1..^1];
        }
        return quotedString;
    }

    private static string ExtractMulitLineString(string quotedString)
    {
        // Remove quotes from strings
        if (quotedString.StartsWith("\"\"\"") && quotedString.EndsWith("\"\"\"") && quotedString.Length >= 6)
        {
            return quotedString[3..^3];
        }
        return quotedString;
    }

    /// <summary>
    /// Gets hidden tokens (comments, whitespace) that appear before this context.
    /// Tracks claimed tokens to prevent duplication across parent/child elements.
    /// </summary>
    private List<HiddenToken>? GetLeadingHiddenTokens(ParserRuleContext context)
    {
        if (context?.Start == null) return null;

        var tokenIndex = context.Start.TokenIndex;
        if (tokenIndex <= 0) return null;

        // Get all hidden tokens to the left of this token
        var hiddenTokens = _tokenStream.GetHiddenTokensToLeft(tokenIndex, Lexer.Hidden);
        if (hiddenTokens == null || hiddenTokens.Count == 0) return null;

        // Convert ANTLR tokens to our HiddenToken model, skipping already claimed tokens
        var result = new List<HiddenToken>();
        foreach (IToken token in hiddenTokens)
        {
            // Skip if this token was already claimed by another element
            if (_claimedTokenIndexes.Contains(token.TokenIndex))
            {
                continue;
            }

            // Claim this token
            _claimedTokenIndexes.Add(token.TokenIndex);

            result.Add(new HiddenToken
            {
                TokenType = token.Type,
                Text = token.Text ?? string.Empty
            });
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Gets hidden tokens that appear after this context on the same line.
    /// Stops at newline - tokens after newline become leading tokens for the next element.
    /// </summary>
    private List<HiddenToken>? GetTrailingHiddenTokens(ParserRuleContext context)
    {
        if (context?.Stop == null) return null;

        var tokenIndex = context.Stop.TokenIndex;

        // Get hidden tokens to the right of this token
        var hiddenTokens = _tokenStream.GetHiddenTokensToRight(tokenIndex, Lexer.Hidden);
        if (hiddenTokens == null || hiddenTokens.Count == 0) return null;

        // Only include tokens until we hit a newline, and skip already claimed tokens
        var result = new List<HiddenToken>();
        foreach (IToken token in hiddenTokens)
        {
            var text = token.Text ?? string.Empty;
            
            // If token contains newline, don't include it (or anything after)
            if (text.Contains('\n') || text.Contains('\r'))
            {
                break;
            }

            // Skip if this token was already claimed
            if (_claimedTokenIndexes.Contains(token.TokenIndex))
            {
                continue;
            }

            // Claim this token
            _claimedTokenIndexes.Add(token.TokenIndex);

            result.Add(new HiddenToken
            {
                TokenType = token.Type,
                Text = text
            });
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Gets hidden tokens that appear after the root context (EOF comments/whitespace).
    /// Unlike GetTrailingHiddenTokens, this captures ALL remaining tokens including those after newlines.
    /// </summary>
    private List<HiddenToken>? GetEofHiddenTokens(ParserRuleContext context)
    {
        if (context?.Stop == null) return null;

        var tokenIndex = context.Stop.TokenIndex;

        // Get ALL hidden tokens to the right (including after newlines)
        var hiddenTokens = _tokenStream.GetHiddenTokensToRight(tokenIndex, Lexer.Hidden);
        if (hiddenTokens == null || hiddenTokens.Count == 0) return null;

        // Include all unclaimed tokens (including comments after newlines)
        var result = new List<HiddenToken>();
        foreach (IToken token in hiddenTokens)
        {
            // Skip if already claimed
            if (_claimedTokenIndexes.Contains(token.TokenIndex))
            {
                continue;
            }

            // Claim this token
            _claimedTokenIndexes.Add(token.TokenIndex);

            result.Add(new HiddenToken
            {
                TokenType = token.Type,
                Text = token.Text ?? string.Empty
            });
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Extracts inline comment from a STAR token if present.
    /// STAR tokens can contain LINE_COMMENT at the start due to the pattern: ([\r\n] | LINE_COMMENT) WS* '*'
    /// Example STAR token: '// inline comment\r\n* '
    /// Returns the comment text (including //) or null if no comment present.
    /// </summary>
    private static string? ExtractInlineCommentFromStarToken(IToken starToken)
    {
        if (starToken == null) return null;
        
        var text = starToken.Text;
        if (string.IsNullOrEmpty(text) || !text.StartsWith("//")) return null;
        
        // Extract everything from "//" up to (but not including) the newline
        var newlineIndex = text.IndexOfAny(new[] { '\r', '\n' });
        if (newlineIndex < 0) return null;
        
        return text[..newlineIndex];
    }

    /// <summary>
    /// Extracts inline comment from the STAR token of the current rule context
    /// and attaches it to the previous rule's trailing tokens.
    /// This handles the case where inline comments after rules (e.g., "* path // comment")
    /// are absorbed into the NEXT rule's STAR token due to lexer pattern: ([\r\n] | LINE_COMMENT) WS* '*'
    /// </summary>
    private void ExtractAndAttachInlineComment(ParserRuleContext currentRuleContext, FshRule? previousRule, FshRule current)
    {
        if (currentRuleContext?.Start == null) return;
        
        // The first token of any sd rule is the STAR token
        var starToken = currentRuleContext.Start;
        if (starToken.Type != FSHLexer.STAR) return;
        
        // Extract inline comment if present
        var comment = ExtractInlineCommentFromStarToken(starToken);
        if (string.IsNullOrEmpty(comment)) return;

        if (previousRule != null)
        {
            // Attach to previous rule as trailing hidden token
            // We need to add a space before the comment to separate it from the rule content
            previousRule.TrailingHiddenTokens ??= new List<HiddenToken>();
            previousRule.TrailingHiddenTokens.Add(new HiddenToken
            {
                TokenType = FSHLexer.LINE_COMMENT,
                Text = " " + comment  // Prepend space to separate comment from rule content
            });
        }
        else
        {
            // attach it to the current rule
            current.LeadingHiddenTokens ??= new List<HiddenToken>();
            current.LeadingHiddenTokens.Add(new HiddenToken
            {
                TokenType = FSHLexer.LINE_COMMENT,
                Text = comment + "\r\n"  // newline at the end to preserve formatting
            });
        }
    }

    #endregion
}
