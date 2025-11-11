// <copyright file="CrossVersionMapCollection.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fml_processor.Models;
// using Microsoft.Health.Fhir.CodeGenCommon.Extensions;
// using Hl7.Fhir.FhirPath.Validator;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Model.CdsHooks;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Specification.Navigation;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using Hl7.FhirPath;
using System.Runtime.CompilerServices;
// using Microsoft.Health.Fhir.CodeGenCommon.Extensions;

namespace Microsoft.Health.Fhir.MappingLanguage;

public class FmlValidator
{
    public FmlValidator()
    {
    }

    public static async Task<OperationOutcome> VerifyFmlDataTypes(FmlStructureMap fml, ValidateMapOptions options)
    {
        Console.WriteLine($"Validating map {fml.MapDeclaration?.Url ?? fml.Metadata.FirstOrDefault(m => m.Path == "url")?.Value}");

        // TODO:
        // * All the function invocation return types
        // * source cardinality
        // * expressions that do nothing but populate variables eg: `x.y as s -> x.y as t "demo"`
        // * include constants in the variable processing/aliasing
        // * rule names must be unique within a group (spec says map, but we can start here)

        // scan the uses types
        Dictionary<string, StructureDefinition?> _aliasedTypes = new();
        foreach (var use in fml.Structures)
        {
            // Console.WriteLine($"Use {use.Key} as {use.Value?.Alias}");
            var sd = await options.resolveMapUseCrossVersionType(use.Url.Trim('\"'), use.Alias);
            if (use.Alias != null)
                _aliasedTypes.Add(use.Alias, sd);
            else if (sd != null && sd.Name != null)
                _aliasedTypes.Add(use.Alias ?? sd.Name, sd);
        }

        //// scan the imports
        //// pointless since everything is brought in through the wildcard stuff anyway!
        //foreach (var import in fml.ImportsByUrl.Keys)
        //{
        //    Console.WriteLine($"Import {import}");
        //    var maps = resolveMaps(import);
        //}

        // scan all the groups
        OperationOutcome outcome = new OperationOutcome();
        foreach (var group in fml.Groups)
        {
            var results = VerifyFmlDataTypesForGroup(fml, group, _aliasedTypes, options);
            outcome.Issue.AddRange(results);
        }
        Console.WriteLine();
        return outcome;
    }

    public static List<OperationOutcome.IssueComponent> VerifyFmlDataTypesForGroup(
		FmlStructureMap fml,
        GroupDeclaration group,
        Dictionary<string, StructureDefinition?> _aliasedTypes, ValidateMapOptions options
        )
    {
        List<OperationOutcome.IssueComponent> issues = new List<OperationOutcome.IssueComponent>();
        Console.Write($"  {group.Name}(");

        // Check the types in the group parameters
        Dictionary<string, PropertyOrTypeDetails?> parameterTypesByName = new();
        foreach (var gp in group.Parameters)
        {
            if (gp != group.Parameters.First())
                Console.Write(", ");
            PropertyOrTypeDetails? tp = null;
            string? type = gp.Type;
            // lookup the type in the aliases
            var resolver = gp.Mode == ParameterMode.Source ? options.source : options.target;
            if (type != null)
            {
                if (!type.Contains('/') && _aliasedTypes.ContainsKey(type))
                {
                    var sd = _aliasedTypes[type];
                    if (sd != null)
                    {
                        var sw = new FmlStructureDefinitionWalker(sd, resolver);
                        tp = new PropertyOrTypeDetails(sw.Current.Path, sw.Current, resolver);
                        type = $"{sd.Url}|{sd.Version}";
                        gp.ParameterElementDefinition = sw.Current;
                    }
                    else
                    {
                        string msg = $"Group {group.Name} parameter {gp.Name} type `{gp.Type}` is not imported in a use at @{gp.Position?.StartLine}:{gp.Position?.StartColumn}";
                        ReportIssue(issues, msg, OperationOutcome.IssueType.NotFound);
                    }
                }
                else
                {
                    tp = ResolveDataTypeFromName(group, resolver, issues, gp, type);
                    if (tp != null)
                    {
                        gp.ParameterElementDefinition = tp.Element;
                    }
                    else
                    {
                        string msg = $"Group {group.Name} parameter {gp.Name} has no type `{gp.Type}` at @{gp.Position?.StartLine}:{gp.Position?.StartColumn}";
                        ReportIssue(issues, msg, OperationOutcome.IssueType.NotFound);
                    }
                }
            }
            else if (gp.ParameterElementDefinition != null)
            {
                tp = new PropertyOrTypeDetails(gp.ParameterElementDefinition.Path, gp.ParameterElementDefinition, resolver);
            }
            parameterTypesByName.Add(gp.Name, tp);
            Console.Write($" {gp.Name}");
            if (gp.ParameterElementDefinition != null)
                Console.Write($" : {gp.ParameterElementDefinition.DebugString()}");
            else
                Console.Write($" : {type ?? "?"}");
        }
        Console.Write(" )\n  {\n");

        // Check if the group extends any other groups
        if (!string.IsNullOrEmpty(group.Extends))
        {
            // Check that the named group exists
            if (!options.namedGroups.ContainsKey(group.Extends!))
            {
                string msg = $"Unable to extends group `{group.Extends}` in {group.Name} at @{group.Position?.StartLine}:{group.Position?.StartColumn}";
                ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
            }

            // Check that the parameter values are compatible...

        }

        // Now scan for dependencies in rules
        foreach (var rule in group.Rules)
        {
            VerifyFmlGroupRule("     ", fml, group, _aliasedTypes, options, issues, parameterTypesByName, rule);
        }
        Console.WriteLine("  }");

        return issues;
    }

    private static void VerifyFmlGroupRule(string prefix, FmlStructureMap fml, GroupDeclaration group, Dictionary<string, StructureDefinition?> _aliasedTypes, ValidateMapOptions options, List<OperationOutcome.IssueComponent> issues, Dictionary<string, PropertyOrTypeDetails?> parameterTypesByName, Rule rule)
    {
        Console.Write(prefix);

        // deduce the datatypes for the variables
        Dictionary<string, PropertyOrTypeDetails?> parameterTypesByNameForRule = parameterTypesByName.ShallowCopy();
        PropertyOrTypeDetails? singleSourceVariable = null;
		var sce = rule.SimpleCopyExpression();
		if (sce == null) // this is really meant to be checking if this is a complex mapping thingo
        {
            foreach (var source in rule.Sources)
            {
                if (source != rule.Sources.First())
                    Console.Write(", ");

                Console.Write($"{source.Identifier}");
                PropertyOrTypeDetails? tpV = null;
                try
                {
                    tpV = ResolveIdentifierType(source.Identifier(), parameterTypesByNameForRule, source, issues);
                }
                catch (ApplicationException e)
                {
                    string msg = $"Can't resolve type of source identifier `{source.Identifier}`: {e.Message}";
                    ReportIssue(issues, msg, OperationOutcome.IssueType.Exception);
                }

                if (!string.IsNullOrEmpty(source.Type))
                {
                    // Cast down to this type
                    Console.Write($".ofType({source.Type})");
                    string typeName = source.Type!;
                    if (!typeName.Contains(':')) // assume this is a FHIR type
                        typeName = "http://hl7.org/fhir/StructureDefinition/" + typeName;
                    var sdCastType = options.source.FindStructureDefinitionAsync(typeName).WaitResult();
                    if (sdCastType == null)
                    {
                        string msg = $"Unable to resolve type cast `{typeName}` in {group.Name} at @{source.Position?.StartLine}:{source.Position?.StartColumn}";
                        ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
                    }
                    else
                    {
                        var sw = new FmlStructureDefinitionWalker(sdCastType, options.source);

                        // Bit of a hack for resource based property types
                        bool isResourceType = sdCastType.BaseDefinition == "http://hl7.org/fhir/StructureDefinition/DomainResource"
                            || sdCastType.BaseDefinition == "http://hl7.org/fhir/StructureDefinition/Resource";

                        // Check that the type being attempted is among the types in the actual property
                        // including hack around resource type profiles too
                        if (!tpV?.Element?.Current?.Type.Any(t => t.Code == source.Type || isResourceType && t.Code == "Resource") == true)
                        {
                            string msg = $"Type `{typeName}` is not a valid cast for `{source.Identifier}` in {group.Name} at @{source.Position?.StartLine}:{source.Position?.StartColumn}";
                            ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
                        }

                        // This will be permitted to continue, as the error for the base property/type not being
                        // found will already be reported above, however allowing this to pass through
                        // will "recover" and permit the remaining code to work with the casted type.
                        // And thus not start reporting more dud errors (based on the already reported one)
                        // - Yes, it's already broken by this stage, but at least will get meaningful errors
                        // further down the chain as they've declared they expect that it should be this type.
                        tpV = new PropertyOrTypeDetails(tpV?.PropertyPath ?? string.Empty, sw.Current, options.source);
                    }
                }

                // Cardinality constraints
                if (source.Min.HasValue || source.Max != null)
                {
                    Console.Write($"[{source.Min}..{source.Max}]");
                }

                // Source Default value
                if (!string.IsNullOrEmpty(source.DefaultValue))
                {
                    // Check the type of the default? - This is the legacy format
                    Console.Write($" DEFAULT({source.DefaultValue})");
                }

                Console.Write($" : {tpV?.Element?.DebugString() ?? "?"}");
                // Should this be moved to the end, as the JAVA code seems to do (feels wrong to me)
                if (source.Variable != null)
                {
                    Console.Write($" as {source.Variable}");
                    if (parameterTypesByNameForRule.ContainsKey(source.Variable))
                    {
                        string msg = $"Duplicate source parameter name `{source.Variable}` in {group.Name} at @{source.Position?.StartLine}:{source.Position?.StartColumn}";
                        ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
                    }
                    else
                    {
                        parameterTypesByNameForRule.Add(source.Variable, tpV);
                    }
                }

                if (source == rule.Sources.First())
                {
                    singleSourceVariable = tpV;
                }

                if (source.Condition != null)
                {
                    // just output the where clause
                    Console.Write($" where ({source.Condition})");
                    ValidateFhirPathExpression(source.Condition, group, options, issues, rule, parameterTypesByNameForRule, singleSourceVariable);
                }

                if (source.Check != null)
                {
                    // just output the check clause
                    Console.Write($" check ({source.Check})");
                    ValidateFhirPathExpression(source.Check, group, options, issues, rule, parameterTypesByNameForRule, singleSourceVariable);
                }

                if (source.Log != null)
                {
                    // just output the log clause
                    Console.Write($" log ({source.Log})");
                    ValidateFhirPathExpression(source.Log, group, options, issues, rule, parameterTypesByNameForRule, singleSourceVariable);
                }

                //if (source.Alias != null)
                //{
                //    Console.Write($" as {source.Alias}");
                //    if (parameterTypesByNameForRule.ContainsKey(source.Alias))
                //    {
                //        string msg = $"Duplicate source parameter name `{source.Alias}` in {group.Name} at @{source.Line}:{source.Column}";
                //        ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
                //    }
                //    else
                //    {
                //        parameterTypesByNameForRule.Add(source.Alias, tpV);
                //    }
                //}
            }

            Console.Write($"  -->  ");

            foreach (var target in rule.Targets)
            {
                if (target != rule.Targets.First())
                    Console.Write(", ");

                PropertyOrTypeDetails? tpV = null;
                if (!string.IsNullOrEmpty(target.Identifier()))
                {
                    Console.Write($"{target.Identifier}");
                    try
                    {
                        tpV = ResolveIdentifierType(target.Identifier(), parameterTypesByNameForRule, target, issues);
                        if (!target.Identifier().Contains('.') && target.Transform != null)
                        {
                            string msg = $"target in copy transform `{target.Identifier}`: must contain both context and element @{target.Position?.StartLine}:{target.Position?.StartColumn}";
                            ReportIssue(issues, msg, OperationOutcome.IssueType.Value, OperationOutcome.IssueSeverity.Warning);
                        }
                    }
                    catch (ApplicationException e)
                    {
                        string msg = $"Can't resolve type of target identifier `{target.Identifier}`: {e.Message}";
                        ReportIssue(issues, msg, OperationOutcome.IssueType.Exception);
                    }
                    Console.Write($" : {tpV?.Element?.DebugString() ?? "?"}");
                }
                //if (target.Invocation != null)
                //{
                //    tpV = VerifyInvocation(group, issues, parameterTypesByNameForRule, target.Invocation, options.target.Resolver);
                //}

                if (target.Transform != null)
                {
                    Console.Write(" = ");
                    PropertyOrTypeDetails? transformedSourceV = null;
                    //if (target.Transform.Literal != null)
                    //{
                    //    var literal = target.Transform.Literal;
                    //    Console.Write($" {literal.RawText}");
                    //    transformedSourceV = ResolveLiteralDataType(group, options.source.Resolver, issues, literal);
                    //}
                    if (!string.IsNullOrEmpty(target.Transform.Identifier()))
                    {
                        Console.Write($" {target.Transform.Identifier}");
                        try
                        {
                            if (target.Transform.Identifier().Contains('.'))
                            {
                                string msg = $"source in copy transform `{target.Transform.Identifier}`: cannot contain child properties @{target.Transform.Position?.StartLine}:{target.Transform.Position?.StartColumn} - consider then statement or fhirpath expression";
                                ReportIssue(issues, msg, OperationOutcome.IssueType.Value, OperationOutcome.IssueSeverity.Warning);
                            }
                            transformedSourceV = ResolveIdentifierType(target.Transform.Identifier()!, parameterTypesByNameForRule, target.Transform, issues);
                        }
                        catch (ApplicationException e)
                        {
                            string msg = $"Can't resolve type of target transform identifier `{target.Transform.Identifier}`: {e.Message}";
                            ReportIssue(issues, msg, OperationOutcome.IssueType.Exception);
                        }

                    }
                    //if (target.Transform.Invocation != null)
                    //    transformedSourceV = VerifyInvocation(group, issues, parameterTypesByNameForRule, target.Transform.Invocation, options.target.Resolver);

                    //if (target.Transform.fpExpression != null)
                    //{
                    //    // Need to process this!
                    //    Console.Write($"( {target.Transform.fpExpression.RawText} )");
                    //    var fpResultType = ValidateFhirPathExpression(target.Transform.fpExpression, group, options, issues, rule, parameterTypesByNameForRule, singleSourceVariable);
                    //    if (fpResultType != null)
                    //        transformedSourceV = fpResultType;
                    //}

                    //if (transformedSourceV?.Element == null && target.Transform.fpExpression == null) // skipping error for FP expression as that's known
                    //{
                    //    string msg = $"No type derived for transform `{target.Transform.RawText}` at @{target.Transform.Position?.StartLine}:{target.Transform.Position?.StartColumn} ";
                    //    ReportIssue(issues, msg, OperationOutcome.IssueType.Exception);
                    //}

                    Console.Write($" : {transformedSourceV?.Element?.DebugString() ?? "?"}");

                    // Validate that the datatype is compatible
                    VerifyMapBetweenDatatypes(options.typedGroups, issues, target.Transform, transformedSourceV, tpV);
                }

                if (target.Variable != null)
                {
                    Console.Write($" as {target.Variable}");
                    if (parameterTypesByNameForRule.ContainsKey(target.Variable))
                    {
                        string msg = $"Duplicate target parameter name `{target.Variable}` in {group.Name} at @{target.Position?.StartLine}:{target.Position?.StartColumn}";
                        ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
                    }
                    else
                    {
                        parameterTypesByNameForRule.Add(target.Variable, tpV);
                    }
                }
            }

            // Complex rules need a name! (logic from JAVA code)
            if (string.IsNullOrEmpty(rule.Name)
                && (rule.Sources.Count() == 0
                || rule.Sources.Count() > 1
                || string.IsNullOrEmpty(rule.Sources[0].Identifier()))
                )
            {
                string msg = $"Complex rules need a name in {group.Name} at @{rule.Position?.StartLine}:{rule.Position?.StartColumn}";
                ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
            }

            // Rule names need to be a valid ID type
            if (!string.IsNullOrEmpty(rule.Name) && !Hl7.Fhir.Model.Id.IsValidValue(rule.Name!))
            {
                string msg = $"Rule name `{rule.Name}` is invalid in {group.Name} at @{rule.Position?.StartLine}:{rule.Position?.StartColumn}";
                ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
            }
        }

		if (sce != null)
        {
            PropertyOrTypeDetails? sourceV = null;
            try
            {
                sourceV = ResolveIdentifierType(sce.Source.Identifier(), parameterTypesByNameForRule, sce.Source, issues);
                Console.Write($"{sce.Source} : {sourceV?.Element?.DebugString() ?? "?"}");
            }
            catch (ApplicationException ex)
            {
                string msg = $"Can't resolve simple source `{sce.Source}`: {ex.Message}";
                ReportIssue(issues, msg, OperationOutcome.IssueType.Exception);
            }

            Console.Write($"  -->  ");

            PropertyOrTypeDetails? targetV = null;
            try
            {
                if (sce.Target.Element == null)
                {
                    string msg = $"simple target `{sce.Target}`: does not contain an element in context.element @{sce.Target?.Position?.StartLine}:{sce.Target?.Position?.StartColumn}";
                    ReportIssue(issues, msg, OperationOutcome.IssueType.Value, OperationOutcome.IssueSeverity.Warning);
                }
                targetV = ResolveIdentifierType(sce.Target.Identifier(), parameterTypesByNameForRule, sce.Target, issues);
                Console.Write($"{sce.Target} : {targetV?.Element?.DebugString() ?? "?"}");
            }
            catch (ApplicationException ex)
            {
                string msg = $"Can't resolve simple target `{sce.Target}`: {ex.Message}";
                ReportIssue(issues, msg, OperationOutcome.IssueType.Exception);
            }

            // Verify that there exists a map that goes between these types
            VerifyMapBetweenDatatypes(options.typedGroups, issues, rule, sourceV, targetV);

            // If the source or target is a backbone element, then that shouldn't be a simple rule!
            if (sourceV?.Element?.Current?.Type.FirstOrDefault()?.Code == "BackboneElement")
            {
                string msg = $"Simple copy not applicable for BackboneElement properties `{sourceV.PropertyPath}` @{rule.Position?.StartLine}:{rule.Position?.StartColumn}";
                ReportIssue(issues, msg, OperationOutcome.IssueType.Value);
            }

            if (!string.IsNullOrEmpty(rule.Name) && !Hl7.Fhir.Model.Id.IsValidValue(rule.Name!))
            {
                string msg = $"Rule name `{rule.Name}` is invalid in {group.Name} at @{rule.Position?.StartLine}:{rule.Position?.StartColumn}";
                ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
            }
        }

        // Scan any dependent group calls
        var de = rule.Dependent;
        if (de != null)
        {
            foreach (var i in de.Invocations)
            {
                Console.Write("\n        " + prefix);
                Console.Write($" then {i.Name}( ");
                if (!fml.Groups.Exists(g => g.Name == i.Name) && !options.namedGroups.ContainsKey(i.Name))
                {
                    Console.WriteLine($"... )");
                    string msg = $"Calling non existent dependent group {i.Name} at @{i.Position?.StartLine}:{i.Position?.StartColumn}";
                    ReportIssue(issues, msg, OperationOutcome.IssueType.NotFound);
                }
                else
                {
                    var dg = fml.Groups.FirstOrDefault(g => g.Name == i.Name) ?? options.namedGroups[i.Name];
                    // walk the parameters
                    for (int nParam = 0; nParam < dg.Parameters.Count; nParam++)
                    {
                        if (nParam > 0)
                            Console.Write(", ");
                        var gp = dg.Parameters[nParam];
                        string? type = gp.Type;
                        // lookup the type in the aliases
                        if (type != null && !type.Contains('/') && _aliasedTypes.ContainsKey(type))
                        {
                            var sd = _aliasedTypes[type];
                            if (sd != null)
                                type = $"{sd.Url}|{sd.Version}";
                        }

                        // which value should we use
                        if (nParam < i.Parameters.Count)
                        {
                            var cp = i.Parameters[nParam];
                            Console.Write($"{cp.Type.ToString() ?? cp.Value?.ToString()}");
                            // Check in the rule source/target aliases
                            if (rule != null)
                            {
                                string? variableName = cp.Type.ToString() ?? cp.Value?.ToString();
                                if (variableName == null)
                                {
                                    string msg = $"No Variable name provided for parameter {i} calling dependent group {i.Name} at @{cp.Position?.StartLine}:{cp.Position?.StartColumn}";
                                    ReportIssue(issues, msg, OperationOutcome.IssueType.NotFound);
                                }
                                else
                                {
                                    if (!parameterTypesByNameForRule.ContainsKey(variableName))
                                    {
                                        string msg = $"Variable not found `{variableName}` calling dependent group {i.Name} at @{cp.Position?.StartLine}:{cp.Position?.StartColumn}";
                                        ReportIssue(issues, msg, OperationOutcome.IssueType.NotFound);
                                    }
                                    else
                                    {
                                        var gpv = parameterTypesByNameForRule[variableName];
                                        type = gpv?.ToString() ?? "??";
                                        // Console.Write($"({type})");
                                        if (gpv != null)
                                        {
                                            if (gp.ParameterElementDefinition == null)
                                            {
                                                gp.ParameterElementDefinition = gpv.Element;
                                            }
                                            else
                                            {
                                                // This is an invalid comparison for the types!
                                                if (gp.ParameterElementDefinition.ToString() != gpv.Element.ToString())
                                                {
                                                    string msg = $"Mismatched type `{cp.Type.ToString() ?? cp.Value?.ToString()}` calling dependent group {i.Name} at @{cp.Position?.StartLine}:{cp.Position?.StartColumn} - {gp.ParameterElementDefinition.DebugString()} != {gpv.Element.DebugString()}";
                                                    ReportIssue(issues, msg, OperationOutcome.IssueType.Conflict);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        Console.Write($" {gp.Name} : {type ?? gp.ParameterElementDefinition?.Path ?? "?"}");
                    }
                    Console.WriteLine(" )");
                }
            }

            if (de.Rules.Any())
            {
                Console.Write('\n');
                // process any expressions as a result of any
                foreach (var childRule in de.Rules)
                {
                    VerifyFmlGroupRule(prefix + "     ", fml, group, _aliasedTypes, options, issues, parameterTypesByNameForRule, childRule);
                }
            }
        }
        else
        {
            Console.WriteLine();
        }
    }

    private static PropertyOrTypeDetails? ValidateFhirPathExpression(string expressionNode, GroupDeclaration group, ValidateMapOptions options, List<OperationOutcome.IssueComponent> issues, Rule rule, Dictionary<string, PropertyOrTypeDetails?> parameterTypesByNameForRule, PropertyOrTypeDetails? singleSourceVariable)
    {
        PropertyOrTypeDetails? result = null;
        //BaseFhirPathExpressionVisitor fpv = new BaseFhirPathExpressionVisitor(options.source.MI, options.source.SupportedResources, options.source.OpenTypes);
        //fpv.UseVariableAsName = true;
        //FhirPathCompiler fhirPathCompiler = new FhirPathCompiler();
        //// TODO: Add in the resource/rootResource/context as inputs where they can be
        //if (rule.MappingExpression?.Sources.Count == 1 && singleSourceVariable != null)
        //{
        //    fpv.SetContext(singleSourceVariable.Element.Path);
        //}
        //foreach (var v in parameterTypesByNameForRule)
        //{
        //    if (v.Value?.Resolver == options.target.Resolver)
        //    {
        //        continue;
        //    }

        //    try
        //    {
        //        // TODO: @brianpos - I believe this will resolve the nullability warning and avoid throwing but give the same result, is that correct?
        //        if (v.Value == null)
        //        {
        //            fpv.RegisterVariable(v.Key, new FhirPathVisitorProps());
        //        }
        //        else
        //        {
        //            fpv.RegisterVariable(v.Key, v.Value.Element.Path);
        //        }
        //    }
        //    catch (Exception)
        //    {
        //        // This one really should be able to resolve here.
        //        // though the Static validator only understands the source
        //        // type space...
        //        fpv.RegisterVariable(v.Key, new FhirPathVisitorProps());
        //    }
        //}
        //var exprNode = fhirPathCompiler.Parse(expressionNode.RawText);
        //var r = exprNode.Accept(fpv);
        //if (r != null && r.Types.Count == 1)
        //{
        //    // we can leverage this type!
        //    result = ResolveDataTypeFromName(group, options.source.Resolver, issues, expressionNode, r.Types[0].ClassMapping.Name);
        //}
        //foreach (var issue in fpv.Outcome.Issue)
        //{
        //    if (issue.Details.Text.Contains("did you mean to use the variable"))
        //        continue;
        //    issues.Add(issue);
        //    Console.WriteLine($"\n{issue.Severity?.GetDocumentation()}: {issue.Details.Text} @{expressionNode.Line}:{expressionNode.Column}");
        //}
        return result;
    }

    private static void VerifyMapBetweenDatatypes(Dictionary<string, GroupDeclaration?> typedGroups, List<OperationOutcome.IssueComponent> issues, FmlNode node, PropertyOrTypeDetails? sourceV, PropertyOrTypeDetails? targetV)
    {
        if (sourceV != null && targetV != null)
        {
            IEnumerable<string> sourceTypeNames = GetTypeNames(sourceV);
            IEnumerable<string> targetTypeNames = GetTypeNames(targetV);
            // Just need to ensure that there exists a target type for each source that exists
            foreach (var stn in sourceTypeNames)
            {
                if (targetTypeNames.Contains(stn))
                    continue;

                IEnumerable<string> lookupsForSource;
                if (stn.StartsWith("http://hl7.org/fhirpath/"))
                {
                    bool sameTypeFound = false;
                    lookupsForSource = targetTypeNames.Select(ttn =>
                    {
                        int versionSeperatorIndex = ttn.IndexOf('|');
                        var versionLessSourceType = versionSeperatorIndex > 0 ? ttn.Substring(0, versionSeperatorIndex) : ttn;
                        if (FhirToFhirPathDataTypeMappings.TryGetValue(versionLessSourceType, out var fpTN) && !ttn.StartsWith("http://hl7.org/fhirpath/"))
                        {
                            if (stn == fpTN)
                                sameTypeFound = true;
                            return $"{stn} -> {fpTN}";
                        }
                        return $"{stn} -> {ttn}";
                    }).ToArray();
                    if (sameTypeFound)
                        continue;
                }
                else
                {
                    int versionSeperatorIndex = stn.IndexOf('|');
                    var versionLessSourceType = versionSeperatorIndex > 0 ? stn.Substring(0, versionSeperatorIndex) : stn;
                    if (FhirToFhirPathDataTypeMappings.TryGetValue(versionLessSourceType, out var fpTN) && targetTypeNames.Any(ttn => ttn.StartsWith("http://hl7.org/fhirpath/")))
                    {
                        if (targetTypeNames.Contains(fpTN))
                            continue;
                        lookupsForSource = targetTypeNames.Where(ttn => !ttn.StartsWith("http://hl7.org/fhirpath/")).Select(ttn => $"{stn} -> {ttn}");

                        lookupsForSource = lookupsForSource.Union(
                            targetTypeNames.Where(ttn => ttn.StartsWith("http://hl7.org/fhirpath/")).Select(ttn => $"{fpTN} -> {ttn}")
                            );
                    }
                    else
                    {
                        lookupsForSource = targetTypeNames.Select(ttn => $"{stn} -> {ttn}");
                    }
                }

                if (!lookupsForSource.Any(mapLookup => typedGroups.ContainsKey(mapLookup)))
                {
                    string msg = $"There is no target type for mapping from {stn} detected @{node.Position?.StartLine}:{node.Position?.StartColumn}";
                    msg += "\n        " + string.Join("\n        ", lookupsForSource);
                    ReportIssue(issues, msg, OperationOutcome.IssueType.Exception);
                }
            }
        }
    }

    private static Dictionary<string, string> FhirToFhirPathDataTypeMappings = new Dictionary<string, string>(){
            { "http://hl7.org/fhir/StructureDefinition/boolean", "http://hl7.org/fhirpath/System.Boolean" },
            { "http://hl7.org/fhir/StructureDefinition/string", "http://hl7.org/fhirpath/System.String" },
            { "http://hl7.org/fhir/StructureDefinition/uri", "http://hl7.org/fhirpath/System.String" },
            { "http://hl7.org/fhir/StructureDefinition/code", "http://hl7.org/fhirpath/System.String" },
            { "http://hl7.org/fhir/StructureDefinition/oid", "http://hl7.org/fhirpath/System.String" },
            { "http://hl7.org/fhir/StructureDefinition/id", "http://hl7.org/fhirpath/System.String" },
            { "http://hl7.org/fhir/StructureDefinition/uuid", "http://hl7.org/fhirpath/System.String" },
            { "http://hl7.org/fhir/StructureDefinition/markdown", "http://hl7.org/fhirpath/System.String" },
            { "http://hl7.org/fhir/StructureDefinition/base64Binary", "http://hl7.org/fhirpath/System.String" },
            { "http://hl7.org/fhir/StructureDefinition/integer", "http://hl7.org/fhirpath/System.Integer" },
            { "http://hl7.org/fhir/StructureDefinition/unsignedInt", "http://hl7.org/fhirpath/System.Integer" },
            { "http://hl7.org/fhir/StructureDefinition/positiveInt", "http://hl7.org/fhirpath/System.Integer" },
            { "http://hl7.org/fhir/StructureDefinition/integer64", "http://hl7.org/fhirpath/System.Long" },
            { "http://hl7.org/fhir/StructureDefinition/decimal", "http://hl7.org/fhirpath/System.Decimal" },
            { "http://hl7.org/fhir/StructureDefinition/date", "http://hl7.org/fhirpath/System.DateTime" },
            { "http://hl7.org/fhir/StructureDefinition/dateTime", "http://hl7.org/fhirpath/System.DateTime" },
            { "http://hl7.org/fhir/StructureDefinition/instant", "http://hl7.org/fhirpath/System.DateTime" },
            { "http://hl7.org/fhir/StructureDefinition/time", "http://hl7.org/fhirpath/System.Time" },
            { "http://hl7.org/fhir/StructureDefinition/Quantity", "http://hl7.org/fhirpath/System.Quantity" },
    };

    private static IEnumerable<string> GetTypeNames(PropertyOrTypeDetails ptd)
    {
        List<string> typeNames = new List<string>();

        // simple root case, we're not in a child property
        if (!ptd.Element.Path.Contains("."))
        {
            typeNames.Add($"{ptd.Element.StructureDefinition.Url}|{ptd.Element.StructureDefinition.Version}");
        }
        else
        {
            foreach (var t in ptd.Element.Current.Type)
            {
                var typeName = t.Code;
                if (typeName != null)
                {
                    if (!typeName.Contains(':')) // assume this is a FHIR type
                        typeName = "http://hl7.org/fhir/StructureDefinition/" + typeName;
                    if (typeName.StartsWith("http://hl7.org/fhirpath/System."))
                    {
                        if (!typeNames.Contains(typeName))
                            typeNames.Add(typeName);
                    }
                    else
                    {
                        var sdType = ptd.Resolver.FindStructureDefinitionAsync(typeName).WaitResult();
                        if (sdType != null)
                        {
                            var tn = $"{sdType.Url}|{sdType.Version}";
                            if (!typeNames.Contains(tn))
                                typeNames.Add(tn);
                        }
                    }
                }
                else
                {
                    var tn = t.Code + "|" + ptd.Element.StructureDefinition.Version;
                    if (!typeNames.Contains(tn))
                        typeNames.Add(tn);
                }
            }
        }
        return typeNames;
    }

    //private static PropertyOrTypeDetails? ResolveLiteralDataType(GroupDeclaration group, IAsyncResourceResolver sourceResolver, List<OperationOutcome.IssueComponent> issues, LiteralValue literal)
    //{
    //    if (literal.TokenType == FmlTokenTypeCodes.Id)
    //        ReportIssue(issues, $"Invalid token type `{literal.TokenType}` parsing literal - likely should have been a target.transform.identifier @{literal.Line}:{literal.Column}", OperationOutcome.IssueType.Invalid);
    //    // todo: resolve this type
    //    string? typeName = null;
    //    switch (literal.TokenType)
    //    {
    //        case FmlTokenTypeCodes.String: typeName = "string"; break;
    //        case FmlTokenTypeCodes.DoubleQuotedString: typeName = "string"; break;
    //        case FmlTokenTypeCodes.Bool: typeName = "boolean"; break;
    //        case FmlTokenTypeCodes.Date: typeName = "date"; break;
    //        case FmlTokenTypeCodes.Datetime: typeName = "dateTime"; break;
    //        case FmlTokenTypeCodes.Time: typeName = "time"; break;
    //        case FmlTokenTypeCodes.Integer: typeName = "integer"; break;
    //        case FmlTokenTypeCodes.Longnumber: typeName = "integer64"; break;
    //        case FmlTokenTypeCodes.Decimal: typeName = "decimal"; break;
    //            // quantity?
    //    }
    //    return ResolveDataTypeFromName(group, sourceResolver, issues, literal, typeName);
    //}

    public static PropertyOrTypeDetails? ResolveDataTypeFromName(GroupDeclaration group, IAsyncResourceResolver resolver, List<OperationOutcome.IssueComponent> issues, FmlNode literal, string? typeName)
    {
        if (typeName != null)
        {
            if (!typeName.Contains(':')) // assume this is a FHIR type
                typeName = "http://hl7.org/fhir/StructureDefinition/" + typeName;
            var sdCastType = resolver.FindStructureDefinitionAsync(typeName).WaitResult();
            if (sdCastType == null)
            {
                string msg = $"Unable to resolve type `{typeName}` in {group.Name} at @{literal.Position?.StartLine}:{literal.Position?.StartColumn}";
                ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
            }
            else
            {
                var sw = new FmlStructureDefinitionWalker(sdCastType, resolver);

                // TODO: @brianpos - the property type is not treated as nullable anywhere else, so I am using empty string here. Not sure if PropertyPath should be nullable instead.
                return new PropertyOrTypeDetails(string.Empty, sw.Current, resolver);
            }
        }

        return null;
    }

    //public static PropertyOrTypeDetails? VerifyInvocation(GroupDeclaration group, List<OperationOutcome.IssueComponent> issues, Dictionary<string, PropertyOrTypeDetails?> parameterTypesByNameForRule, FmlInvocation invocation, IAsyncResourceResolver targetResolver)
    //{
    //    // deduce the return type of the invocation
    //    Console.Write($" {invocation.Identifier}(");
    //    PropertyOrTypeDetails? parameterTypeForFirstParam = null;
    //    foreach (var p in invocation.Parameters)
    //    {
    //        if (p != invocation.Parameters.First())
    //            Console.Write(",");

    //        PropertyOrTypeDetails? parameterTypeV = null;
    //        if (!string.IsNullOrEmpty(p.Identifier))
    //        {
    //            Console.Write($"{p.Identifier}");
    //            try
    //            {
    //                parameterTypeV = ResolveIdentifierType(p.Identifier!, parameterTypesByNameForRule, p, issues); // is this the correct place?
    //                p.ParameterElementDefinition = parameterTypeV?.Element;
    //            }
    //            catch (ApplicationException e)
    //            {
    //                string msg = $"Can't resolve type of parameter identifier `{p.Identifier}`: {e.Message}";
    //                ReportIssue(issues, msg, OperationOutcome.IssueType.Exception);
    //            }
    //        }
    //        if (p.Literal != null)
    //        {
    //            // TODO: resolve the type of the literal
    //            Console.Write($" {p.Literal.RawText}");
    //            parameterTypeV = ResolveLiteralDataType(group, targetResolver, issues, p.Literal);
    //        }
    //        if (p == invocation.Parameters.First())
    //            parameterTypeForFirstParam = parameterTypeV;
    //        Console.Write($" : {parameterTypeV?.Element?.DebugString() ?? "?"}");
    //    }
    //    Console.Write(" )");
    //    switch (invocation.Identifier)
    //    {
    //        case "create":
    //            // Check that the first parameter is a string, and that it has resolved properly.
    //            var literal = invocation.Parameters.FirstOrDefault()?.Literal;
    //            if (literal != null)
    //            {
    //                string typeName = literal.ValueAsString;
    //                return ResolveDataTypeFromName(group, targetResolver, issues, literal, typeName);
    //            }
    //            else
    //            {
    //                string msg = $"No type parameter for create at @{invocation.Line}:{invocation.Column}";
    //                ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
    //            }
    //            break;

    //        case "copy":
    //            return parameterTypeForFirstParam;

    //        case "truncate":
    //            return ResolveDataTypeFromName(group, targetResolver, issues, invocation, "string");

    //        case "escape":
    //            return ResolveDataTypeFromName(group, targetResolver, issues, invocation, "string");

    //        // cast?

    //        case "append":
    //            return ResolveDataTypeFromName(group, targetResolver, issues, invocation, "string");

    //        case "translate":
    //            // Check that the parameters are correct for the translate operation
    //            if (invocation.Parameters.Count < 2)
    //            {
    //                string msg = $"Translate missing transform parameters at @{invocation.Line}:{invocation.Column}";
    //                ReportIssue(issues, msg, OperationOutcome.IssueType.Duplicate);
    //            }
    //            // This will just return the datatype of the first parameter (as it's meant to just convert that value with the definition in the second parameter)
    //            return parameterTypeForFirstParam;

    //        case "reference":
    //            return ResolveDataTypeFromName(group, targetResolver, issues, invocation, "string");

    //        // dateOp?

    //        case "uuid":
    //            return ResolveDataTypeFromName(group, targetResolver, issues, invocation, "uuid");

    //        case "pointer":
    //            return ResolveDataTypeFromName(group, targetResolver, issues, invocation, "string");

    //        // case "evaluate": // FHIRpath expression - wish I could use the Fhirpath static validator here

    //        case "cc":
    //            return ResolveDataTypeFromName(group, targetResolver, issues, invocation, "CodeableConcept");

    //        case "c":
    //            return ResolveDataTypeFromName(group, targetResolver, issues, invocation, "Coding");

    //        case "qty":
    //            return ResolveDataTypeFromName(group, targetResolver, issues, invocation, "Quantity");

    //        case "id":
    //            return ResolveDataTypeFromName(group, targetResolver, issues, invocation, "Identifier");

    //        case "cp":
    //            return ResolveDataTypeFromName(group, targetResolver, issues, invocation, "ContactPoint");

    //        default:
    //            string msgUnhandled = $"Invocation of `{invocation.Identifier}` not handled yet at @{invocation.Line}:{invocation.Column}";
    //            ReportIssue(issues, msgUnhandled, OperationOutcome.IssueType.Duplicate);
    //            break;
    //    }

    //    // unknown return type detected
    //    return null;
    //}

    private static void ReportIssue(List<OperationOutcome.IssueComponent> issues, string message, OperationOutcome.IssueType code, OperationOutcome.IssueSeverity severity = OperationOutcome.IssueSeverity.Error)
    {
        issues.Add(new OperationOutcome.IssueComponent()
        {
            Code = code,
            Severity = severity,
            Details = new CodeableConcept() { Text = message }
        });
        Console.WriteLine($"\n{severity.GetDocumentation()}: {message}");
    }

    public static PropertyOrTypeDetails? ResolveIdentifierType(string identifier, Dictionary<string, PropertyOrTypeDetails?> parameterTypesByNameForRule, FmlNode sourceOrTargetNode, List<OperationOutcome.IssueComponent> issues)
    {
        IEnumerable<string> parts = identifier.Split('.');
        // Get the base type for this variable
        if (parameterTypesByNameForRule.TryGetValue(parts.First(), out PropertyOrTypeDetails? tp))
        {
            if (tp != null)
            {
                var childProps = parts.Skip(1);
                while (childProps.Any())
                {
                    var sw = new FmlStructureDefinitionWalker(tp.Element, tp.Resolver);
                    try
                    {
                        var node = sw.Child(childProps.First());
                        if (node != null)
                        {
                            if (node.Current.Current.ContentReference != null)
                            {
                                // Need to walk into the node further
                                if (FmlStructureDefinitionWalker.TryFollowContentReference(node.Current, s => tp.Resolver.FindStructureDefinitionAsync(s).WaitResult(), out var r))
                                {
                                    node = new FmlStructureDefinitionWalker(r, tp.Resolver);
                                }
                            }
                            tp = new PropertyOrTypeDetails(tp.PropertyPath + "." + childProps.First(), node.Current, tp.Resolver);
                        }
                    }
                    catch (Exception ex)
                    {
                        // if (ex is StructureDefinitionWalkerException swe && !sw.Current.Elements.Any(e => e.Path == sw.Current.Path + "." + childProps.First() && e.IsPrimitiveValueConstraint()))
                        throw new ApplicationException($"{ex.Message} @{sourceOrTargetNode.Position?.StartLine}:{sourceOrTargetNode.Position?.StartColumn}", ex);
                    }

                    childProps = childProps.Skip(1);
                }
                return tp;
            }
            return null;
        }
        throw new ApplicationException($"Identifier `{parts.First()}` is not in scope @{sourceOrTargetNode.Position?.StartLine}:{sourceOrTargetNode.Position?.StartColumn}");
    }
}

public static class FmlValidatorExtensions
{
	public static string Identifier(this RuleTarget? me) 
	{
		return me?.Context + (me?.Element != null ? "." + me?.Element : ""); 
	}

	public static string Identifier(this RuleSource? me)
	{
		return me?.Context + (me?.Element != null ? "." + me?.Element : "");
	}

	/// <summary>
	/// Simulating the old parsers data
	/// </summary>
	public static string Identifier(this Transform me)
	{
		return me.Type;
	}

	public static SimpleCopyExpression? SimpleCopyExpression(this Rule me)
	{
		if (me.Sources.Count == 1 && me.Targets.Count == 1)
		{
			var result = new SimpleCopyExpression()
			{
				Source = me.Sources[0],
				Target = me.Targets[0]
			};
			if (result.Source.Variable != null || result.Target.Variable != null)
				return null;
			if (me.Dependent != null)
				return null;
			if (result.Target.Transform != null)
				return null;
			if (result.Target.ListMode != null)
				return null;
			return result;
		}
		return null;
	}

}

public class SimpleCopyExpression
{
	public RuleSource Source { get; set; } = new();

	/// <summary>
	/// Target transformations (optional - rules can have no targets)
	/// </summary>
	public RuleTarget Target { get; set; } = new();
}

public record ValidateMapOptions
{
    public required Func<string, string?, Task<StructureDefinition?>> resolveMapUseCrossVersionType { get; init; }
    public required Func<string, IEnumerable<FmlStructureMap>> resolveMaps { get; init; }
    public required IAsyncResourceResolver source { get; init; }
    public required IAsyncResourceResolver target { get; init; }
    public required Dictionary<string, GroupDeclaration> namedGroups { get; init; }
    public required Dictionary<string, GroupDeclaration?> typedGroups { get; init; }
}

internal static class ElementDefinitionNavigatorExtensions
{
    public static string DebugString(this ElementDefinitionNavigator Element, bool includeTypes = true)
    {
        // return $"{Definition.Url}|{Definition.Version} # {Element.Path} ({String.Join(",", Element.Current.Type.Select(t => t.Code))})";
        if (includeTypes)
            return $"{Element.Path}|{Element.StructureDefinition.Version} ({String.Join(",", Element.Current.Type.Select(t =>
            {
                if (string.IsNullOrWhiteSpace(t.Code))
                {
                    System.Diagnostics.Trace.WriteLine($"Element {Element.Path}|{Element.StructureDefinition.Version ?? Element.StructureDefinition.FhirVersion.GetLiteral()} has no type data");
                }
                return t.Code;
            }))})";
        return $"{Element.Path}|{Element.StructureDefinition.Version ?? Element.StructureDefinition.FhirVersion.GetLiteral()}";
    }
}

/// <summary>
/// Copied from the LinqExtensions.cs file in the CodeGenCommon project (removing the dependency)
/// </summary>
internal static class ShallowCopyExtensions
{
    /// <summary>
    /// A Dictionary&lt;KT,VT&gt; extension method that shallow copies the given source.
    /// </summary>
    /// <typeparam name="KT">Key Type.</typeparam>
    /// <typeparam name="VT">Value Type.</typeparam>
    /// <param name="source">The source dictionary to copy.</param>
    /// <returns>A Dictionary&lt;KT,VT&gt;</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<KT, VT> ShallowCopy<KT, VT>(this Dictionary<KT, VT> source)
        where KT : notnull
    {
        Dictionary<KT, VT> dest = [];

        foreach (KeyValuePair<KT, VT> kvp in source)
        {
            dest.Add(kvp.Key, kvp.Value);
        }

        return dest;
    }
}
