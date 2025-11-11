using fml_processor.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;

namespace fml_processor;

public class ConceptMapConverter
{
	string? getFhirVersion(string? version)
	{
		return version?.Substring(0, 3) switch
		{
			"3.0" => "STU3",
			"4.0" => "R4",
			"4.3" => "R4B",
			"5.0" => "R5",
			"6.0" => "R6",
			_ => null,
		};	
	}
	private string? UseScope(FmlStructureMap map, DataType dt, StructureMode mode)
	{
		if (dt is Canonical == false)
			return null;
		Canonical canonical = (Canonical)dt;

		string resourceType = canonical.Uri.Substring(canonical.Uri.LastIndexOf("/") + 1);
		string? fhirVersion = getFhirVersion(canonical.Version);
		map.Structures.Add(new StructureDeclaration() 
		{
			Url = $"http://hl7.org/fhir/{canonical.Version?.Substring(0,3)}/StructureDefinition/{resourceType}",
			Alias = resourceType + fhirVersion,
			Mode = mode
		});
		return resourceType + fhirVersion;
	}

	// Convert the ConceptMap (which has different groups for each resource/backbone type)
	public FmlStructureMap ConvertToFml(ConceptMap conceptMap)
	{
		// create a new FmlStructureMap
		// and create a new group in each group of the ConceptMap
		// metadata can be populated with ConceptMap metadata if needed
		// (This isn't going into the Fml ConceptMap structure, as these are conceptual mappings that
		// are being converted into actual executable structure mappings)

		var structureMap = new FmlStructureMap();
		structureMap.Structures = new ();
		var sourceAlias = UseScope(structureMap, conceptMap.SourceScope, StructureMode.Source);
		var targetAlies = UseScope(structureMap, conceptMap.TargetScope, StructureMode.Target);

		// Populate metadata from ConceptMap if available
		//if (!string.IsNullOrEmpty(conceptMap.Url))
		//{
		//	structureMap.Metadata.Add(new MetadataDeclaration 
		//	{ 
		//		Path = "url", 
		//		Value = conceptMap.Url.Replace("ConceptMap", "StructureMap") 
		//	});
		//}
		
		if (!string.IsNullOrEmpty(conceptMap.Name))
		{
			structureMap.Metadata.Add(new MetadataDeclaration 
			{ 
				Path = "name", 
				Value = conceptMap.Name 
			});
		}
		
		if (!string.IsNullOrEmpty(conceptMap.Title))
		{
			structureMap.Metadata.Add(new MetadataDeclaration 
			{ 
				Path = "title", 
				Value = conceptMap.Title 
			});
		}
		
		if (!string.IsNullOrEmpty(conceptMap.Version))
		{
			structureMap.Metadata.Add(new MetadataDeclaration 
			{ 
				Path = "version", 
				Value = conceptMap.Version 
			});
		}
		
		if (conceptMap.Status.HasValue)
		{
			structureMap.Metadata.Add(new MetadataDeclaration 
			{ 
				Path = "status", 
				Value = conceptMap.Status.Value.ToString().ToLowerInvariant() 
			});
		}
		
		// Create map declaration if URL exists
		if (!string.IsNullOrEmpty(conceptMap.Url))
		{
			var identifier = conceptMap.Name ?? "ConceptMapToStructureMap";
			structureMap.MapDeclaration = new MapDeclaration
			{
				Url = conceptMap.Url.Replace("ConceptMap", "StructureMap"),
				Identifier = identifier
			};
		}
		
		// Convert each ConceptMap group into a StructureMap group
		foreach (var group in conceptMap.Group)
		{
			var fmlGroup = ConvertGroupToFml(group);
			if (fmlGroup != null)
			{
				structureMap.Groups.Add(fmlGroup);
			}
		}

		// patch the first group's source/target to use the overall source/target scope aliases
		if (structureMap.Groups.Count > 0)
		{
			var firstGroup = structureMap.Groups[0];
			if (sourceAlias != null && firstGroup.Parameters.Count > 0)
			{
				firstGroup.Parameters[0].Type = sourceAlias;
			}
			if (targetAlies != null && firstGroup.Parameters.Count > 1)
			{
				firstGroup.Parameters[1].Type = targetAlies;
			}
		}

		return structureMap;
	}
	
	private string PascalCase(string input)
	{
		if (string.IsNullOrEmpty(input))
			return input;
		
		var words = input.Split(new char[] { '_', ' ', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
		var pascalCased = string.Concat(words.Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
		return pascalCased;
	}
	private GroupDeclaration? ConvertGroupToFml(ConceptMap.GroupComponent group)
	{
		if (string.IsNullOrEmpty(group.Source) || string.IsNullOrEmpty(group.Target))
		{
			return null;
		}
		
		// Extract resource/backbone type names from the StructureDefinition URLs
		string sourceName = ExtractTypeName(group.Source);
		string targetName = ExtractTypeName(group.Target);
		
		// Create a group name based on the source and target types
		string groupName = $"{PascalCase(sourceName)}_To_{PascalCase(targetName)}";
		if (PascalCase(sourceName) == PascalCase(targetName))
			groupName = PascalCase(sourceName);
		
		var fmlGroup = new GroupDeclaration
		{
			Name = groupName,
		};
		
		// Add parameters for source and target
		fmlGroup.Parameters.Add(new GroupParameter
		{
			Mode = ParameterMode.Source,
			Type = !sourceName.Contains(".") ? sourceName : null,
			Name = "src"
		});
		
		fmlGroup.Parameters.Add(new GroupParameter
		{
			Mode = ParameterMode.Target,
			Type = !targetName.Contains(".") ? targetName : null,
			Name = "tgt"
		});
		
		// Convert each element mapping into a rule
		foreach (var element in group.Element)
		{
			var rule = ConvertElementToRule(element, sourceName, targetName);
			if (rule != null)
			{
				fmlGroup.Rules.Add(rule);
			}
		}
		
		return fmlGroup;
	}
	
	private Rule? ConvertElementToRule(ConceptMap.SourceElementComponent element, string sourceName, string targetName)
	{
		if (string.IsNullOrEmpty(element.Code))
		{
			return null;
		}
		
		var rule = new Rule();
		
		// Create source from element code
		rule.Sources.Add(new RuleSource
		{
			Context = "src",
			Element = element.Code.Replace("[x]", ""),
			// Variable = SanitizeVariableName(element.Code)
		});
		
		// Create targets from element targets
		if (element.Target != null && element.Target.Count > 0)
		{
			foreach (var target in element.Target)
			{
				if (!string.IsNullOrEmpty(target.Code))
				{
					var ruleTarget = new RuleTarget
					{
						Context = "tgt",
						Element = target.Code.Replace("[x]", "")
					};
					
					if (target.Code != element.Code)
					{
						// this was a rename, so lets add a comment to mark that after the rule
						var ht = new HiddenToken()
						{
							TokenType = FmlMappingLexer.LINE_COMMENT,
							Text = $"  // Renamed"
						};
						if (rule.TrailingHiddenTokens == null)
							rule.TrailingHiddenTokens = new List<HiddenToken>();
						rule.TrailingHiddenTokens.Add(ht);
					}

					if (target.Relationship != ConceptMap.ConceptMapRelationship.Equivalent)
					{
						var ht = new HiddenToken()
						{
							TokenType = FmlMappingLexer.LINE_COMMENT,
							Text = $"  // {target.Relationship.GetLiteral()}"
						};
						if (rule.TrailingHiddenTokens == null)
							rule.TrailingHiddenTokens = new List<HiddenToken>();
						rule.TrailingHiddenTokens.Add(ht);
					}


					// Add copy transform for the mapping
					//ruleTarget.Transform = new Transform
					//{
					//	Type = TransformType.Copy,
					//	Parameters = new List<TransformParameter>
					//	{
					//		new TransformParameter
					//		{
					//			Type = TransformParameterType.Identifier,
					//			Value = SanitizeVariableName(element.Code)
					//		}
					//	}
					//};

					rule.Targets.Add(ruleTarget);
				}
			}
		}
		else
		{
			// the no map code!
			// inject some comment tokens before our node to indicate this
			var ht = new HiddenToken()
			{
				TokenType = FmlMappingLexer.LINE_COMMENT,
				Text = "  // No target mapping for "
			};
			if (rule.LeadingHiddenTokens == null)
				rule.LeadingHiddenTokens = new List<HiddenToken>();
			rule.LeadingHiddenTokens.Add(ht);
		}

		return rule;
	}
	
	private string ExtractTypeName(string structureDefinitionUrl)
	{
		if (structureDefinitionUrl.StartsWith("http://hl7.org/fhir/StructureDefinition/"))
		{
			return structureDefinitionUrl.Substring("http://hl7.org/fhir/StructureDefinition/".Length);
		}
		
		// Fallback: try to get the last segment after the last /
		var lastSlash = structureDefinitionUrl.LastIndexOf('/');
		if (lastSlash >= 0 && lastSlash < structureDefinitionUrl.Length - 1)
		{
			return structureDefinitionUrl.Substring(lastSlash + 1);
		}
		
		return structureDefinitionUrl;
	}
	
	private string SanitizeVariableName(string elementCode)
	{
		// Replace dots and other special characters with underscores for variable names
		// Also make first letter lowercase for variable convention
		var sanitized = elementCode.Replace('.', '_').Replace('[', '_').Replace(']', '_');
		
		if (sanitized.Length > 0 && char.IsUpper(sanitized[0]))
		{
			sanitized = char.ToLowerInvariant(sanitized[0]) + sanitized.Substring(1);
		}
		
		return sanitized;
	}


	// Implementation of the ConceptMapConverter class
	public ConceptMap Convert(ConceptMap source)
	{
		var result = source.DeepCopy() as ConceptMap;
		result.UseContext = null;
		result.Group.Clear();

		// scan all the groups
		foreach (var group in source.Group)
		{
			// is this a resource level mapping?
			if (!group.Source.StartsWith("http://hl7.org/fhir/StructureDefinition/"))
				break;

			string sourceResourceType = group.Source.Substring("http://hl7.org/fhir/StructureDefinition/".Length);
			string targetResourceType = group.Target.Substring("http://hl7.org/fhir/StructureDefinition/".Length);

			// scan all the elements in the group
			// and split elements into separate groups based on resource/backbone element types (split on .)
			// e.g. Observation.component.valueQuantity will go into group http://hl7.org/fhir/StructureDefinition/Observation.component
			Stack<ConceptMap.GroupComponent> currentGroup = new Stack<ConceptMap.GroupComponent>();
			Stack<string> currentSourcePath = new Stack<string>();
			Stack<string> currentTargetPath = new Stack<string>();
			
			foreach (var element in group.Element.Skip(1))
			{
				// Determine the backbone element path for this element
				// e.g., "Account.balance.amount" -> backbone is "Account.balance"
				string elementSourcePath = GetBackboneElementPath(element.Code);
				
				// Determine the corresponding target path from the first target
				string elementTargetPath = elementSourcePath;
				if (element.Target != null && element.Target.Count > 0 && !string.IsNullOrEmpty(element.Target[0].Code))
				{
					elementTargetPath = GetBackboneElementPath(element.Target[0].Code);
				}
				
				// Pop groups until we're at the right level
				while (currentSourcePath.Count > 0 && !IsChildOf(elementSourcePath, currentSourcePath.Peek()))
				{
					currentGroup.Pop();
					currentSourcePath.Pop();
					currentTargetPath.Pop();
				}
				
				// Push new groups if we're entering a new backbone element
				if (currentSourcePath.Count == 0 || elementSourcePath != currentSourcePath.Peek())
				{
					var newGroup = new ConceptMap.GroupComponent()
					{
						Source = "http://hl7.org/fhir/StructureDefinition/" + elementSourcePath,
						Target = "http://hl7.org/fhir/StructureDefinition/" + elementTargetPath,
					};
					currentGroup.Push(newGroup);
					currentSourcePath.Push(elementSourcePath);
					currentTargetPath.Push(elementTargetPath);
					result.Group.Add(newGroup);
				}
				
				// Add this element to the current group if it's a direct child
				if (currentSourcePath.Count > 0 && currentGroup.Count > 0 && 
				    IsDirectChildOf(element.Code, currentSourcePath.Peek()))
				{
					// Create a new element with relative path (just the last component)
					var newElement = element.DeepCopy() as ConceptMap.SourceElementComponent;
					newElement.Code = GetLastPathComponent(element.Code);
				
				// Filter out FHIR infrastructure elements
				if (IsInfrastructureElement(newElement.Code))
					continue;
					
					// Update target codes to be relative as well
					if (newElement.Target != null)
					{
						foreach (var target in newElement.Target)
						{
							if (!string.IsNullOrEmpty(target.Code))
							{
								target.Code = GetLastPathComponent(target.Code);
							}
						}
					}
					
					currentGroup.Peek().Element.Add(newElement);
				}
			}
		}

		return result;
	}

	/// <summary>
	/// Gets the backbone element path for a given element code.
	/// e.g., "Account.balance.amount" -> "Account.balance"
	///       "Account.balance" -> "Account.balance"
	///       "Account" -> "Account"
	/// </summary>
	private string GetBackboneElementPath(string elementCode)
	{
		var lastDotIndex = elementCode.LastIndexOf('.');
		if (lastDotIndex == -1)
		{
			// No dot, this is the resource level
			return elementCode;
		}
		
		// Check if the part after the last dot starts with a lowercase letter
		// If so, it's a property, and we want the parent path
		var lastComponent = elementCode.Substring(lastDotIndex + 1);
		if (lastComponent.Length > 0 && char.IsLower(lastComponent[0]))
		{
			// This is a property, return the backbone element path
			return elementCode.Substring(0, lastDotIndex);
		}
		
		// This is itself a backbone element
		return elementCode;
	}

	/// <summary>
	/// Checks if childPath is a child of parentPath.
	/// e.g., "Account.balance.amount" is a child of "Account.balance"
	///       "Account.balance" is a child of "Account"
	/// </summary>
	private bool IsChildOf(string childPath, string parentPath)
	{
		if (childPath == parentPath)
			return true;
		
		return childPath.StartsWith(parentPath + ".");
	}

	/// <summary>
	/// Checks if elementPath is a direct child of parentPath.
	/// e.g., "Account.balance.amount" is a direct child of "Account.balance"
	///       "Account.balance" is a direct child of "Account.balance" (the backbone element itself)
	///       "Account.balance" is a direct child of "Account"
	///       "Account.balance.amount" is NOT a direct child of "Account"
	/// </summary>
	private bool IsDirectChildOf(string elementPath, string parentPath)
	{
		// If they're equal, this is the backbone element itself within its own group
		if (elementPath == parentPath)
			return true;
		
		if (!elementPath.StartsWith(parentPath + "."))
			return false;
		
		// Get the relative path
		var relativePath = elementPath.Substring(parentPath.Length + 1);
		
		// Check if there's no further nesting (no dots in the relative path)
		return !relativePath.Contains('.');
	}

	/// <summary>
	/// Gets the last path component.
	/// e.g., "Account.balance.amount" -> "amount"
	///       "Account" -> "Account"
	/// </summary>
	private string GetLastPathComponent(string path)
	{
		var lastDotIndex = path.LastIndexOf('.');
		if (lastDotIndex == -1)
		{
			return path;
		}
		
		return path.Substring(lastDotIndex + 1);
	}

	/// <summary>
	/// Checks if the element code represents a FHIR infrastructure element that should be filtered out.
	/// These are common elements like id, extension, modifierExtension, contained, implicitRules, and meta.
	/// </summary>
	private bool IsInfrastructureElement(string code)
	{
		return code is "id" or "extension" or "modifierExtension" or "contained" or "implicitRules" or "meta";
	}
}
