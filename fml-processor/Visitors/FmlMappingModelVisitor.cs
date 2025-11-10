using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using fml_processor.Models;

namespace fml_processor.Visitors;

/// <summary>
/// Visitor implementation that builds the FML object model from the ANTLR parse tree
/// with full source position tracking
/// </summary>
public class FmlMappingModelVisitor : FmlMappingBaseVisitor<object?>
{
    private readonly CommonTokenStream _tokenStream;
    private readonly HashSet<int> _claimedTokenIndexes = new();

    public FmlMappingModelVisitor(CommonTokenStream tokenStream)
    {
        _tokenStream = tokenStream ?? throw new ArgumentNullException(nameof(tokenStream));
    }

    public override object? VisitStructureMap([NotNull] FmlMappingParser.StructureMapContext context)
    {
        var structureMap = new FmlStructureMap
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetEofHiddenTokens(context) // Special handling for EOF
        };

        // Visit metadata declarations
        foreach (var metadata in context.metadataDeclaration())
        {
            var metadataDecl = (MetadataDeclaration?)Visit(metadata);
            if (metadataDecl != null)
            {
                structureMap.Metadata.Add(metadataDecl);
            }
        }

        // Visit concept map declarations
        foreach (var conceptMap in context.conceptMapDeclaration())
        {
            var conceptMapDecl = (ConceptMapDeclaration?)Visit(conceptMap);
            if (conceptMapDecl != null)
            {
                structureMap.ConceptMaps.Add(conceptMapDecl);
            }
        }

        // Visit map declaration
        if (context.mapDeclaration() != null)
        {
            structureMap.MapDeclaration = (MapDeclaration?)Visit(context.mapDeclaration());
        }

        // Visit structure declarations
        foreach (var structure in context.structureDeclaration())
        {
            var structureDecl = (StructureDeclaration?)Visit(structure);
            if (structureDecl != null)
            {
                structureMap.Structures.Add(structureDecl);
            }
        }

        // Visit import declarations
        foreach (var import in context.importDeclaration())
        {
            var importDecl = (ImportDeclaration?)Visit(import);
            if (importDecl != null)
            {
                structureMap.Imports.Add(importDecl);
            }
        }

        // Visit constant declarations
        foreach (var constant in context.constantDeclaration())
        {
            var constantDecl = (ConstantDeclaration?)Visit(constant);
            if (constantDecl != null)
            {
                structureMap.Constants.Add(constantDecl);
            }
        }

        // Visit group declarations
        foreach (var group in context.groupDeclaration())
        {
            var groupDecl = (GroupDeclaration?)Visit(group);
            if (groupDecl != null)
            {
                structureMap.Groups.Add(groupDecl);
            }
        }

        return structureMap;
    }

    public override object? VisitMetadataDeclaration([NotNull] FmlMappingParser.MetadataDeclarationContext context)
    {
        var metadata = new MetadataDeclaration
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Path = context.qualifiedIdentifier().GetText()
        };

        if (context.literal() != null)
        {
            metadata.Value = Visit(context.literal())?.ToString();
            metadata.IsMarkdown = false;
        }
        else if (context.markdownLiteral() != null)
        {
            metadata.Value = ExtractString(context.markdownLiteral().GetText());
            metadata.IsMarkdown = true;
        }

        return metadata;
    }

    public override object? VisitConceptMapDeclaration([NotNull] FmlMappingParser.ConceptMapDeclarationContext context)
    {
        var conceptMap = new ConceptMapDeclaration
        {
            Position = GetPosition(context),
            Url = ExtractString(context.url().GetText())
        };

        foreach (var prefix in context.conceptMapPrefix())
        {
            var prefixDecl = (ConceptMapPrefix?)Visit(prefix);
            if (prefixDecl != null)
            {
                conceptMap.Prefixes.Add(prefixDecl);
            }
        }

        foreach (var codeMap in context.conceptMapCodeMap())
        {
            var codeMapDecl = (ConceptMapCodeMap?)Visit(codeMap);
            if (codeMapDecl != null)
            {
                conceptMap.CodeMaps.Add(codeMapDecl);
            }
        }

        return conceptMap;
    }

    public override object? VisitConceptMapPrefix([NotNull] FmlMappingParser.ConceptMapPrefixContext context)
    {
        return new ConceptMapPrefix
        {
            Position = GetPosition(context),
            Id = context.ID().GetText(),
            Url = ExtractString(context.url().GetText())
        };
    }

    public override object? VisitConceptMapCodeMap([NotNull] FmlMappingParser.ConceptMapCodeMapContext context)
    {
        return new ConceptMapCodeMap
        {
            Position = GetPosition(context),
            Source = (ConceptMapCode?)Visit(context.conceptMapSource()) ?? new ConceptMapCode(),
            Target = (ConceptMapCode?)Visit(context.conceptMapTarget()) ?? new ConceptMapCode()
        };
    }

    public override object? VisitConceptMapSource([NotNull] FmlMappingParser.ConceptMapSourceContext context)
    {
        return new ConceptMapCode
        {
            Prefix = context.ID().GetText(),
            Code = ExtractString(context.code().GetText())
        };
    }

    public override object? VisitConceptMapTarget([NotNull] FmlMappingParser.ConceptMapTargetContext context)
    {
        return new ConceptMapCode
        {
            Prefix = context.ID().GetText(),
            Code = ExtractString(context.code().GetText())
        };
    }

    public override object? VisitMapDeclaration([NotNull] FmlMappingParser.MapDeclarationContext context)
    {
        return new MapDeclaration
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Url = ExtractString(context.url().GetText()),
            Identifier = context.identifier().GetText()
        };
    }

    public override object? VisitStructureDeclaration([NotNull] FmlMappingParser.StructureDeclarationContext context)
    {
        var structure = new StructureDeclaration
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Url = ExtractString(context.url().GetText()),
            Mode = ParseStructureMode(context.modelMode().GetText())
        };

        if (context.identifier() != null)
        {
            structure.Alias = context.identifier().GetText();
        }

        return structure;
    }

    public override object? VisitImportDeclaration([NotNull] FmlMappingParser.ImportDeclarationContext context)
    {
        return new ImportDeclaration
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Url = ExtractString(context.url().GetText())
        };
    }

    public override object? VisitConstantDeclaration([NotNull] FmlMappingParser.ConstantDeclarationContext context)
    {
        return new ConstantDeclaration
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Name = context.ID().GetText(),
            Expression = GetSourceText(context.fpExpression())
        };
    }

    public override object? VisitGroupDeclaration([NotNull] FmlMappingParser.GroupDeclarationContext context)
    {
        var group = new GroupDeclaration
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Name = context.ID().GetText()
        };

        // Parse parameters
        var parametersCtx = context.parameters();
        foreach (var paramCtx in parametersCtx.parameter())
        {
            var parameter = (GroupParameter?)Visit(paramCtx);
            if (parameter != null)
            {
                group.Parameters.Add(parameter);
            }
        }

        // Parse extends clause
        if (context.extends() != null)
        {
            group.Extends = context.extends().ID().GetText();
        }

        // Parse type mode
        if (context.typeMode() != null)
        {
            group.TypeMode = ParseGroupTypeMode(context.typeMode().groupTypeMode().GetText());
        }

        // Parse rules
        var rulesCtx = context.mapRules();
        foreach (var ruleCtx in rulesCtx.mapRule())
        {
            var rule = (Rule?)Visit(ruleCtx);
            if (rule != null)
            {
                group.Rules.Add(rule);
            }
        }

        return group;
    }

    public override object? VisitParameter([NotNull] FmlMappingParser.ParameterContext context)
    {
        var parameter = new GroupParameter
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Mode = ParseParameterMode(context.parameterMode().GetText()),
            Name = context.ID().GetText()
        };

        if (context.typeIdentifier() != null)
        {
            parameter.Type = context.typeIdentifier().identifier().GetText();
        }

        return parameter;
    }

    public override object? VisitMapSimpleCopy([NotNull] FmlMappingParser.MapSimpleCopyContext context)
    {
        var rule = new Rule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context)
        };

        // Parse as a simple source -> target pattern
        var sourcePath = context.qualifiedIdentifier(0).GetText();
        var targetPath = context.qualifiedIdentifier(1).GetText();

        // Split into context.element
        var (sourceContext, sourceElement) = SplitPath(sourcePath);
        var (targetContext, targetElement) = SplitPath(targetPath);

        rule.Sources.Add(new RuleSource
        {
            Context = sourceContext,
            Element = sourceElement
        });

        rule.Targets.Add(new RuleTarget
        {
            Context = targetContext,
            Element = targetElement
        });

        if (context.ruleName() != null)
        {
            rule.Name = ExtractString(context.ruleName().GetText());
        }

        return rule;
    }

    public override object? VisitMapFhirMarkup([NotNull] FmlMappingParser.MapFhirMarkupContext context)
    {
        return Visit(context.mapTransformationRule());
    }

    public override object? VisitMapTransformationRule([NotNull] FmlMappingParser.MapTransformationRuleContext context)
    {
        var rule = new Rule
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context)
        };

        // Parse sources
        var sourcesCtx = context.ruleSources();
        foreach (var sourceCtx in sourcesCtx.ruleSource())
        {
            var source = (RuleSource?)Visit(sourceCtx);
            if (source != null)
            {
                rule.Sources.Add(source);
            }
        }

        // Parse targets (optional)
        if (context.ruleTargets() != null)
        {
            var targetsCtx = context.ruleTargets();
            foreach (var targetCtx in targetsCtx.ruleTarget())
            {
                var target = (RuleTarget?)Visit(targetCtx);
                if (target != null)
                {
                    rule.Targets.Add(target);
                }
            }
        }

        // Parse dependent expression (optional)
        if (context.dependentExpression() != null)
        {
            rule.Dependent = (RuleDependent?)Visit(context.dependentExpression());
        }

        // Parse rule name (optional)
        if (context.ruleName() != null)
        {
            rule.Name = ExtractString(context.ruleName().GetText());
        }

        return rule;
    }

    public override object? VisitRuleSource([NotNull] FmlMappingParser.RuleSourceContext context)
    {
        var qualifiedId = context.qualifiedIdentifier().GetText();
        var (sourceContext, sourceElement) = SplitPath(qualifiedId);

        var source = new RuleSource
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Context = sourceContext,
            Element = sourceElement
        };

        if (context.typeIdentifier() != null)
        {
            source.Type = context.typeIdentifier().identifier().GetText();
        }

        if (context.sourceCardinality() != null)
        {
            var cardCtx = context.sourceCardinality();
            source.Min = int.Parse(cardCtx.INTEGER().GetText());
            var upperBoundText = cardCtx.upperBound().GetText();
            source.Max = upperBoundText == "*" ? "*" : int.Parse(upperBoundText);
        }

        if (context.sourceDefault() != null)
        {
            source.DefaultValue = GetSourceText(context.sourceDefault());
        }

        if (context.sourceListMode() != null)
        {
            source.ListMode = ParseSourceListMode(context.sourceListMode().GetText());
        }

        if (context.alias() != null)
        {
            source.Variable = context.alias().identifier().GetText();
        }

        if (context.whereClause() != null)
        {
            source.Condition = GetSourceText(context.whereClause().fpExpression());
        }

        if (context.checkClause() != null)
        {
            source.Check = GetSourceText(context.checkClause().fpExpression());
        }

        if (context.log() != null)
        {
            source.Log = GetSourceText(context.log().fpExpression());
        }

        return source;
    }

    public override object? VisitRuleTarget([NotNull] FmlMappingParser.RuleTargetContext context)
    {
        var target = new RuleTarget
        {
            Position = GetPosition(context)
        };

        // Handle different target patterns
        if (context.qualifiedIdentifier() != null)
        {
            var qualifiedId = context.qualifiedIdentifier().GetText();
            var (targetContext, targetElement) = SplitPath(qualifiedId);
            
            target.Context = targetContext;
            target.Element = targetElement;

            if (context.transform() != null)
            {
                target.Transform = (Transform?)Visit(context.transform());
            }
        }
        else if (context.fpExpression() != null)
        {
            // Expression-based target - store as transform
            target.Context = string.Empty;
            target.Transform = new Transform
            {
                Position = GetPosition(context.fpExpression()),
                Type = TransformType.Evaluate,
                Parameters = new List<TransformParameter>
                {
                    new TransformParameter
                    {
                        Type = TransformParameterType.Expression,
                        Value = GetSourceText(context.fpExpression())
                    }
                }
            };
        }
        else if (context.groupInvocation() != null)
        {
            var invocation = (GroupInvocation?)Visit(context.groupInvocation());
            if (invocation != null)
            {
                target.Context = string.Empty;
                target.Transform = new Transform
                {
                    Position = invocation.Position,
                    Type = invocation.Name,
                    Parameters = invocation.Parameters.Select(p => new TransformParameter
                    {
                        Type = p.Type == InvocationParameterType.Literal 
                            ? TransformParameterType.Literal 
                            : TransformParameterType.Identifier,
                        Value = p.Value
                    }).ToList()
                };
            }
        }

        if (context.alias() != null)
        {
            target.Variable = context.alias().identifier().GetText();
        }

        if (context.targetListMode() != null)
        {
            target.ListMode = ParseTargetListMode(context.targetListMode().GetText());
        }

        return target;
    }

    public override object? VisitTransform([NotNull] FmlMappingParser.TransformContext context)
    {
        var transform = new Transform
        {
            Position = GetPosition(context)
        };

        if (context.literal() != null)
        {
            transform.Type = TransformType.Copy;
            transform.Parameters.Add(new TransformParameter
            {
                Type = TransformParameterType.Literal,
                Value = Visit(context.literal()) ?? string.Empty
            });
        }
        else if (context.qualifiedIdentifier() != null)
        {
            transform.Type = TransformType.Copy;
            transform.Parameters.Add(new TransformParameter
            {
                Type = TransformParameterType.Identifier,
                Value = context.qualifiedIdentifier().GetText()
            });
        }
        else if (context.groupInvocation() != null)
        {
            var invocation = (GroupInvocation?)Visit(context.groupInvocation());
            if (invocation != null)
            {
                transform.Type = invocation.Name;
                transform.Parameters = invocation.Parameters.Select(p => new TransformParameter
                {
                    Type = p.Type == InvocationParameterType.Literal 
                        ? TransformParameterType.Literal 
                        : TransformParameterType.Identifier,
                    Value = p.Value
                }).ToList();
            }
        }
        else if (context.fpExpression() != null)
        {
            transform.Type = TransformType.Evaluate;
            transform.Parameters.Add(new TransformParameter
            {
                Type = TransformParameterType.Expression,
                Value = GetSourceText(context.fpExpression())
            });
        }

        return transform;
    }

    public override object? VisitDependentExpression([NotNull] FmlMappingParser.DependentExpressionContext context)
    {
        var dependent = new RuleDependent
        {
            Position = GetPosition(context)
        };

        // Parse group invocations
        foreach (var invocationCtx in context.groupInvocation())
        {
            var invocation = (GroupInvocation?)Visit(invocationCtx);
            if (invocation != null)
            {
                dependent.Invocations.Add(invocation);
            }
        }

        // Parse nested rules
        if (context.mapRules() != null)
        {
            foreach (var ruleCtx in context.mapRules().mapRule())
            {
                var rule = (Rule?)Visit(ruleCtx);
                if (rule != null)
                {
                    dependent.Rules.Add(rule);
                }
            }
        }

        return dependent;
    }

    public override object? VisitGroupInvocation([NotNull] FmlMappingParser.GroupInvocationContext context)
    {
        var invocation = new GroupInvocation
        {
            Position = GetPosition(context),
            LeadingHiddenTokens = GetLeadingHiddenTokens(context),
            TrailingHiddenTokens = GetTrailingHiddenTokens(context),
            Name = context.identifier().GetText()
        };

        if (context.groupParamList() != null)
        {
            foreach (var paramCtx in context.groupParamList().groupParam())
            {
                var param = (InvocationParameter?)Visit(paramCtx);
                if (param != null)
                {
                    invocation.Parameters.Add(param);
                }
            }
        }

        return invocation;
    }

    public override object? VisitGroupParam([NotNull] FmlMappingParser.GroupParamContext context)
    {
        if (context.literal() != null)
        {
            return new InvocationParameter
            {
                Type = InvocationParameterType.Literal,
                Value = Visit(context.literal()) ?? string.Empty
            };
        }
        else if (context.ID() != null)
        {
            return new InvocationParameter
            {
                Type = InvocationParameterType.Identifier,
                Value = context.ID().GetText()
            };
        }

        return null;
    }

    // Literal visitors
    public override object? VisitNullLiteral([NotNull] FmlMappingParser.NullLiteralContext context)
    {
        return null;
    }

    public override object? VisitBooleanLiteral([NotNull] FmlMappingParser.BooleanLiteralContext context)
    {
        return bool.Parse(context.BOOL().GetText());
    }

    public override object? VisitNumberLiteral([NotNull] FmlMappingParser.NumberLiteralContext context)
    {
        var text = context.GetText();
        if (context.DECIMAL() != null)
        {
            return decimal.Parse(text);
        }
        else if (context.INTEGER() != null)
        {
            return int.Parse(text);
        }
        return null;
    }

    public override object? VisitStringLiteral([NotNull] FmlMappingParser.StringLiteralContext context)
    {
        return ExtractString(context.STRING().GetText());
    }

    public override object? VisitQuotedStringLiteral([NotNull] FmlMappingParser.QuotedStringLiteralContext context)
    {
        return ExtractString(context.DOUBLE_QUOTED_STRING().GetText());
    }

    public override object? VisitDateLiteral([NotNull] FmlMappingParser.DateLiteralContext context)
    {
        return context.DATE().GetText();
    }

    public override object? VisitDateTimeLiteral([NotNull] FmlMappingParser.DateTimeLiteralContext context)
    {
        return context.DATETIME().GetText();
    }

    public override object? VisitTimeLiteral([NotNull] FmlMappingParser.TimeLiteralContext context)
    {
        return context.TIME().GetText();
    }

    // Helper methods
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

    private static (string context, string? element) SplitPath(string qualifiedIdentifier)
    {
        var parts = qualifiedIdentifier.Split('.', 2);
        return parts.Length == 1 
            ? (parts[0], null) 
            : (parts[0], parts[1]);
    }

    private static string ExtractString(string quotedString)
    {
        // Remove quotes from strings
        if (quotedString.StartsWith("'") && quotedString.EndsWith("'"))
        {
            return quotedString[1..^1];
        }
        if (quotedString.StartsWith("\"") && quotedString.EndsWith("\""))
        {
            return quotedString[1..^1];
        }
        if (quotedString.StartsWith("```") && quotedString.EndsWith("```"))
        {
            return quotedString[3..^3];
        }
        return quotedString;
    }

    private static StructureMode ParseStructureMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "source" => StructureMode.Source,
            "target" => StructureMode.Target,
            "queried" => StructureMode.Queried,
            "produced" => StructureMode.Produced,
            _ => StructureMode.Source
        };
    }

    private static ParameterMode ParseParameterMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "source" => ParameterMode.Source,
            "target" => ParameterMode.Target,
            _ => ParameterMode.Source
        };
    }

    private static GroupTypeMode ParseGroupTypeMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "types" => GroupTypeMode.Types,
            "type+" => GroupTypeMode.TypePlus,
            _ => GroupTypeMode.Types
        };
    }

    private static SourceListMode? ParseSourceListMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "first" => SourceListMode.First,
            "last" => SourceListMode.Last,
            "not_first" => SourceListMode.NotFirst,
            "not_last" => SourceListMode.NotLast,
            "only_one" => SourceListMode.OnlyOne,
            _ => null
        };
    }

    private static TargetListMode? ParseTargetListMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "first" => TargetListMode.First,
            "last" => TargetListMode.Last,
            "share" => TargetListMode.Share,
            "single" => TargetListMode.Single,
            _ => null
        };
    }

    /// <summary>
    /// Gets the source text for a parser rule context, preserving whitespace.
    /// This is important for FHIRPath expressions where whitespace matters.
    /// </summary>
    private static string GetSourceText(ParserRuleContext context)
    {
        if (context == null || context.Start == null || context.Stop == null)
        {
            return string.Empty;
        }

        var inputStream = context.Start.InputStream;
        if (inputStream == null)
        {
            return context.GetText();
        }

        var startIndex = context.Start.StartIndex;
        var stopIndex = context.Stop.StopIndex;

        if (startIndex < 0 || stopIndex < 0 || stopIndex < startIndex)
        {
            return context.GetText();
        }

        return inputStream.GetText(new Interval(startIndex, stopIndex));
    }

    /// <summary>
    /// Gets hidden tokens (comments, whitespace) that appear before this context.
    /// Only returns tokens if they differ from what the serializer would output by default.
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
    /// Tracks claimed tokens to prevent duplication.
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
}
