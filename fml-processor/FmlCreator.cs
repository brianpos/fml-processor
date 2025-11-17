using Antlr4.Runtime.Dfa;
using fml_processor.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Hl7.FhirPath.Sprache;
using Microsoft.Health.Fhir.MappingLanguage;
using System.Collections.Specialized;
using System.Security.AccessControl;

namespace fml_processor;

/// <summary>
/// Custom FML creator based on input StructureDefinitions only
/// </summary>
public class FmlCreator
{
    // Canonical indexed
    public Dictionary<string, StructureDefinition> Source = new Dictionary<string, StructureDefinition>();
    public Dictionary<string, StructureDefinition> Target = new Dictionary<string, StructureDefinition>();

    // ResourceType indexed
    // public Dictionary<string, StructureDefinition> SourceResources = new Dictionary<string, StructureDefinition>();
    // public Dictionary<string, StructureDefinition> TargetResources = new Dictionary<string, StructureDefinition>();

    public HashSet<string> KnownMappings = [
        // R4 to R6 specific known mappings
        "SampledData.period -> SampledData.interval",

        "Appointment.cancelationReason -> Appointment.cancellationReason",
        "Appointment.reasonCode -> Appointment.reason",
        "Appointment.reasonReference -> Appointment.reason",
        "Appointment.comment -> Appointment.note",
        "Account.partOf -> Account.parent",

        "AuditEvent.period -> AuditEvent.occurred",

        "BiologicallyDerivedProduct.collection.source -> BiologicallyDerivedProduct.collection.sourcePatient",
        "AdverseEvent.event -> AdverseEvent.code",
        "AdverseEvent.date -> AdverseEvent.effect",
        "AdverseEvent.resultingCondition -> AdverseEvent.resultingEffect",
        "AdverseEvent.suspectEntity.causality.assessment -> AdverseEvent.suspectEntity.causality.assessmentMethod",

        "CareTeam.participant.period -> CareTeam.participant.effective",
        "CareTeam.reasonCode -> CareTeam.reason",
        "CareTeam.reasonReference -> CareTeam.reason",

        "CarePlan.activity.reference -> CarePlan.activity.plannedActivityReference",

        "Claim.careTeam.qualification -> Claim.careTeam.specialty",

        "ClaimResponse.item.adjudication.value -> ClaimResponse.item.adjudication.quantity",

        "Communication.reasonCode -> Communication.reason",
        "Communication.reasonReference -> Communication.reason",

        "CommunicationRequest.reasonCode -> CommunicationRequest.reason",
        "CommunicationRequest.reasonReference -> CommunicationRequest.reason",
        "CommunicationRequest.sender -> CommunicationRequest.informationProvider",

        "Composition.relatesTo.code -> Composition.relatesTo.type",

        "ConceptMap.source -> ConceptMap.sourceScope",
        "ConceptMap.target -> ConceptMap.targetScope",
        "ConceptMap.group.element.target.dependsOn.property -> ConceptMap.group.element.target.dependsOn.attribute",
        "ConceptMap.group.unmapped.url -> ConceptMap.group.unmapped.otherMap",

        "Consent.dateTime -> Consent.date",
        "Consent.patient -> Consent.subject",
        "Consent.policy -> Consent.policyBasis",
        "Consent.policy.uri -> Consent.policyBasis.uri",
        // "Consent.provision.class -> Consent.provision.documentType", // goes to both documentType and resourceType

        "DetectedIssue.patient -> DetectedIssue.subject",

        "Device.deviceName -> Device.name",
        "Device.deviceName.name -> Device.name.value",
        "Device.deviceName.type -> Device.name.type",
        "Device.property.valueQuantity -> Device.property.value",
        "Device.property.valueCode -> Device.property.value",

        "DeviceDefinition.property.valueQuantity -> DeviceDefinition.property.value",
        "DeviceDefinition.property.valueCode -> DeviceDefinition.property.value",
        "DeviceMetric.parent -> DeviceMetric.device",

        "DeviceRequest.reasonCode -> DeviceRequest.reason",
        "DeviceRequest.reasonReference -> DeviceRequest.reason",
        "DeviceRequest.code -> DeviceRequest.product",
        "DeviceRequest.priorRequest -> DeviceRequest.replaces",
        "DeviceRequest.performerType -> tgt.performer",

        "DiagnosticReport.imagingStudy -> DiagnosticReport.study",

        "DocumentReference.masterIdentifier -> DocumentReference.identifier",
        // "DocumentReference.content.format -> DocumentReference.content.profile.value", // nested property and why is this a backbone element! should just be a value ticket please...

        "Encounter.participant.individual -> Encounter.participant.actor",
        "Encounter.period -> Encounter.actualPeriod",
        "Encounter.hospitalization -> Encounter.admission",
        "Encounter.hospitalization.preAdmissionIdentifier -> Encounter.admission.preAdmissionIdentifier",
        "Encounter.hospitalization.origin -> Encounter.admission.origin",
        "Encounter.hospitalization.admitSource -> Encounter.admission.admitSource",
        "Encounter.hospitalization.reAdmission -> Encounter.admission.reAdmission",
        "Encounter.hospitalization.destination -> Encounter.admission.destination",
        "Encounter.hospitalization.dischargeDisposition -> Encounter.admission.dischargeDisposition",
        "Encounter.location.physicalType -> Encounter.location.form",

        "EpisodeOfCare.team -> EpisodeOfCare.careTeam",
        "EpisodeOfCare.diagnosis.role -> EpisodeOfCare.diagnosis.use",

        "ExampleScenario.actor.actorId -> ExampleScenario.actor.key",
        "ExampleScenario.actor.name -> ExampleScenario.actor.title",
        "ExampleScenario.instance.name -> ExampleScenario.instance.title",
        "ExampleScenario.instance.resourceId -> ExampleScenario.instance.key",
        "ExampleScenario.instance.resourceType -> ExampleScenario.instance.structureType",
        "ExampleScenario.instance.version.versionId -> ExampleScenario.instance.version.key",
        "ExampleScenario.process.step.operation.name -> ExampleScenario.process.step.operation.title",
        "ExampleScenario.instance.containedInstance.resourceId -> ExampleScenario.instance.containedInstance.instanceReference",
        "ExampleScenario.instance.containedInstance.versionId -> ExampleScenario.instance.containedInstance.versionReference",

        "ExplanationOfBenefit.careTeam.qualification -> ExplanationOfBenefit.careTeam.specialty",
        "ExplanationOfBenefit.item.adjudication.value -> ExplanationOfBenefit.item.adjudication.quantity",

        "FamilyMemberHistory.reasonCode -> FamilyMemberHistory.reason",
        "FamilyMemberHistory.reasonReference -> FamilyMemberHistory.reason",

        "GuidanceResponse.reasonCode -> GuidanceResponse.reason",
        "GuidanceResponse.reasonReference -> GuidanceResponse.reason",

        "HealthcareService.telecom -> HealthcareService.contact",
        "HealthcareService.availableTime -> HealthcareService.availability", // yes these 3 will need some "massaging"
        "HealthcareService.notAvailable -> HealthcareService.availability",
        "HealthcareService.availabilityExceptions -> HealthcareService.availability",

        "ImagingStudy.procedureReference -> ImagingStudy.procedure",
        "ImagingStudy.procedureCode -> ImagingStudy.procedure",
        "ImagingStudy.reasonCode -> ImagingStudy.reason",
        "ImagingStudy.reasonReference -> ImagingStudy.reason",

        "Immunization.reasonCode -> Immunization.reason",
        "Immunization.reasonReference -> Immunization.reason",
        "Immunization.reaction.detail -> Immunization.reaction.manifestation",

        "ImplementationGuide.definition.resource.example -> ImplementationGuide.definition.resource.isExample",
        "ImplementationGuide.manifest.resource.example -> ImplementationGuide.manifest.resource.isExample",

        "Invoice.date -> Invoice.period", // this one is a jira issue for the ballot too

        "Location.telecom -> Location.contact",
        "Location.physicalType -> Location.form",

        "Medication.form -> Medication.doseForm",
        "Medication.manufacturer -> Medication.marketingAuthorizationHolder",
        "Medication.batch -> Medication.instance",
        "Medication.batch.lotNumber -> Medication.instance.lotNumber",
        "Medication.batch.expirationDate -> Medication.instance.expirationDate",

        "MedicationAdministration.effective -> MedicationAdministration.occurrence",
        "MedicationAdministration.reasonCode -> MedicationAdministration.reason",
        "MedicationAdministration.reasonReference -> MedicationAdministration.reason",
        "MedicationAdministration.context -> MedicationAdministration.encounter",

        "MedicationDispense.context -> MedicationDispense.encounter",

        "MedicationRequest.reasonCode -> MedicationRequest.reason",
        "MedicationRequest.reasonReference -> MedicationRequest.reason",
        "MedicationRequest.dispenseRequest.performer -> MedicationRequest.dispenseRequest.dispenser",

        "MedicationStatement.reasonCode -> MedicationStatement.reason",
        "MedicationStatement.reasonReference -> MedicationStatement.reason",
        "MedicationStatement.context -> MedicationStatement.encounter",

        "MedicationRequest.reported -> MedicationRequest.isRecordOfRequest",

        "NutritionOrder.patient -> NutritionOrder.subject",
        "NutritionOrder.enteralFormula.maxVolumeToDeliver -> NutritionOrder.enteralFormula.maxVolumeToAdminister",
        "NutritionOrder.enteralFormula.routeofAdministration -> NutritionOrder.enteralFormula.routeOfAdministration",

        "OrganizationAffiliation.telecom -> OrganizationAffiliation.contact",

        "PaymentNotice.provider -> PaymentNotice.reporter",
        "PaymentReconciliation.paymentAmount -> PaymentReconciliation.amount",
        "PaymentReconciliation.paymentDate -> PaymentReconciliation.date",
        "PaymentReconciliation.detail -> PaymentReconciliation.allocation",
        "PaymentReconciliation.detail.identifier -> PaymentReconciliation.allocation.identifier",
        "PaymentReconciliation.detail.predecessor -> PaymentReconciliation.allocation.predecessor",
        "PaymentReconciliation.detail.type -> PaymentReconciliation.allocation.type",
        "PaymentReconciliation.detail.request -> PaymentReconciliation.allocation.target",
        "PaymentReconciliation.detail.submitter -> PaymentReconciliation.allocation.submitter",
        "PaymentReconciliation.detail.response -> PaymentReconciliation.allocation.response",
        "PaymentReconciliation.detail.date -> PaymentReconciliation.allocation.date",
        "PaymentReconciliation.detail.responsible -> PaymentReconciliation.allocation.responsible",
        "PaymentReconciliation.detail.payee -> PaymentReconciliation.allocation.payee",
        "PaymentReconciliation.detail.amount -> PaymentReconciliation.allocation.amount",

        "PlanDefinition.action.relatedAction.actionId -> PlanDefinition.action.relatedAction.targetId",

        "PractitionerRole.telecom -> PractitionerRole.contact",
        "PractitionerRole.availableTime -> PractitionerRole.availability", // yes these 3 will need some "massaging"
        "PractitionerRole.notAvailable -> PractitionerRole.availability",
        "PractitionerRole.availabilityExceptions -> PractitionerRole.availability",

        "Procedure.performed -> Procedure.occurrence",
        "Procedure.reasonCode -> Procedure.reason",
        "Procedure.reasonReference -> Procedure.reason",
        "Procedure.usedReference -> Procedure.used",
        "Procedure.usedCode -> Procedure.used",
        "Procedure.asserter -> Procedure.reported",

        "ResearchSubject.individual -> ResearchSubject.subject",

        "RiskAssessment.reasonCode -> RiskAssessment.reason",
        "RiskAssessment.reasonReference -> RiskAssessment.reason",

        "ServiceRequest.locationCode -> ServiceRequest.location",
        "ServiceRequest.locationReference -> ServiceRequest.location",
        "ServiceRequest.reasonCode -> ServiceRequest.reason",
        "ServiceRequest.reasonReference -> ServiceRequest.reason",

        "StructureMap.group.rule.dependent.variable -> StructureMap.group.rule.dependent.parameter",

        "Task.reasonCode -> Task.reason",
        "Task.reasonReference -> Task.reason",

        // R5 to R6 specific known mappings
        "ActorDefinition.derivedFrom -> ActorDefinition.baseDefinition",

        "AdverseEvent.occurrence -> AdverseEvent.effect",

        "AllergyIntolerance.lastOccurrence -> AllergyIntolerance.lastReactionOccurrence",

        "AuditEvent.category -> AuditEvent.type",
        "AuditEvent.code -> AuditEvent.subtype",

        "CareTeam.participant.coverage -> CareTeam.participant.effective",

        "Claim.patient -> Claim.subject",

        "ClaimResponse.patient -> ClaimResponse.subject",

        "Consent.policyBasis.url -> Consent.policyBasis.uri",
        "Consent.verification.verificationType -> Consent.verification.type",
        "Consent.verification.verificationDate -> Consent.verification.date",

        "Device.version -> Device.deviceVersion",
        "Device.version.type -> Device.deviceVersion.type",
        "Device.version.component -> Device.deviceVersion.component",
        "Device.version.installDate -> Device.deviceVersion.installDate",
        "Device.version.value -> Device.deviceVersion.value",

        "DeviceDefinition.version -> DeviceDefinition.deviceVersion",
        "DeviceDefinition.version.type -> DeviceDefinition.deviceVersion.type",
        "DeviceDefinition.version.component -> DeviceDefinition.deviceVersion.component",
        "DeviceDefinition.version.value -> DeviceDefinition.deviceVersion.value",

        "DocumentReference.bodySite -> DocumentReference.bodyStructure",

        "EpisodeOfCare.patient -> EpisodeOfCare.subject",

        "ExplanationOfBenefit.patient -> ExplanationOfBenefit.subject",

        "MedicationAdministration.occurence -> MedicationAdministration.occurrence",

        "MedicationRequest.effectiveDosePeriod -> MedicationRequest.effectiveTiming",

        // "MessageHeader.sender -> MessageHeader.source.sender", // need to handle nested paths

        "NutritionOrder.orderer -> NutritionOrder.requester",
        "NutritionOrder.oralDiet.texture.foodType -> NutritionOrder.oralDiet.texture.type",
        "NutritionOrder.enteralFormula.baseFormulaType -> NutritionOrder.enteralFormula.type",
        "NutritionOrder.enteralFormula.baseFormulaProductName -> NutritionOrder.enteralFormula.productName",

        "ObservationDefinition.bodySite -> ObservationDefinition.bodyStructure",
        "ObservationDefinition.qualifiedValue.gender -> ObservationDefinition.qualifiedValue.sexParameterForClinicalUse",

        "Subscription.filterBy.resourceType -> Subscription.filterBy.resource",

        ];

    public List<FmlStructureMap> GenerateMaps()
    {
        var result = new List<FmlStructureMap>();

        // iterate over all the source resources
        foreach (var sourceR in Source)
        {
            if (Target.ContainsKey(sourceR.Key))
            {
                FmlStructureMap fml = CreateMap(sourceR.Value, Target[sourceR.Key]);
                if (fml != null)
                    result.Add(fml);
            }
            else
            {
                Console.WriteLine($"No target mapping for source type {sourceR.Key}");
            }
        }

        return result;
    }

    private FmlStructureMap CreateMap(StructureDefinition sourceSd, StructureDefinition targetSd)
    {
        if (sourceSd.Derivation == StructureDefinition.TypeDerivationRule.Constraint)
        {
            // Console.WriteLine($"Not generating for constriant {sourceSd.Name} {sourceSd.Url}");
            return null;
        }
        var fml = new FmlStructureMap();
        var sourceAlias = UseScope(fml, sourceSd.Url + "|" + sourceSd.Version, StructureMode.Source);
        var targetAlies = UseScope(fml, targetSd.Url + "|" + targetSd.Version, StructureMode.Target);

        string sourceVersion = getFhirVersion(sourceSd.Version).Replace("R", "").Replace("STU", "");
        string targetVersion = getFhirVersion(targetSd.Version).Replace("R", "").Replace("STU", "");

        SetMetadata(fml, "url", $"http://hl7.org/fhir/uv/xver/StructureMap/{sourceSd.Name}{sourceVersion}to{targetVersion}");
        SetMetadata(fml, "name", $"{sourceSd.Name}{sourceVersion}to{targetVersion}");
        SetMetadata(fml, "title", $"{sourceSd.Name} Transforms: {getFhirVersion(sourceSd.Version)} to {getFhirVersion(targetSd.Version)}");

        fml.Imports ??= new List<ImportDeclaration>();
        fml.Imports.Add(new ImportDeclaration() { Url = $"http://hl7.org/fhir/uv/xver/StructureMap/*{sourceVersion}to{targetVersion}" });

        // walk all the properties in the sourceSd
        var group = new GroupDeclaration();
        fml.Groups.Add(group);
        group.Name = $"{ConceptMapConverter.PascalCase(sourceSd.Name)}";
        group.Parameters.Add(new GroupParameter
        {
            Mode = ParameterMode.Source,
            Type = sourceAlias,
            Name = "src"
        });
        group.Parameters.Add(new GroupParameter
        {
            Mode = ParameterMode.Target,
            Type = targetAlies,
            Name = "tgt"
        });
        group.Extends = sourceSd.BaseDefinition?.Replace("http://hl7.org/fhir/StructureDefinition/", "");
        if (group.Extends == "Base")
            group.Extends = null;
        if (group.Extends != null)
            group.TypeMode = GroupTypeMode.TypePlus;

        // for now just do a simple copy of all matching elements by name
        var missedSourceElements = new List<ElementDefinition>();
        var missedTargetElements = targetSd.Differential.Element.Skip(1).ToList();
        Stack<GroupDeclaration> groupStack = new Stack<GroupDeclaration>();
        Stack<string> groupPathStack = new Stack<string>(); // Track the full path for each group
        groupStack.Push(group);
        groupPathStack.Push(sourceSd.Name); // Root level is the resource name

        foreach (var se in sourceSd.Differential.Element.Skip(1))
        {
            // Calculate the parent path for this element
            var currentElementPath = se.Path;
            var parentPath = currentElementPath.Contains(".") ? currentElementPath.Substring(0, currentElementPath.LastIndexOf(".")) : sourceSd.Name;

            // Pop groups until we're at the right level
            while (groupPathStack.Count > 1 && parentPath != groupPathStack.Peek() && !parentPath.StartsWith(groupPathStack.Peek() + "."))
            {
                groupStack.Pop();
                groupPathStack.Pop();
            }

            var matchingTe = targetSd.Differential.Element.FirstOrDefault(te =>
                    te.Path.Replace("[x]", "") == se.Path.Replace("[x]", "")
                    || KnownMappings.Contains($"{se.Path.Replace("[x]", "")} -> {te.Path.Replace("[x]", "")}")
                    || KnownMappings.Contains($"{te.Path.Replace("[x]", "")} -> {se.Path.Replace("[x]", "")}") // reverse mapping too (from the previous direction)
                    );
            if (matchingTe != null)
            {
                missedTargetElements.Remove(matchingTe);
                var rule = new Rule();
                rule.Sources.Add(new RuleSource
                {
                    Context = "src",
                    Element = se.Path.Contains(".") ? se.Path.Substring(se.Path.IndexOf(".") + 1).Replace("[x]", "") : se.Path.Replace("[x]", "")
                });
                rule.Targets.Add(new RuleTarget
                {
                    Context = "tgt",
                    Element = matchingTe.Path.Contains(".") ? matchingTe.Path.Substring(matchingTe.Path.IndexOf(".") + 1).Replace("[x]", "") : matchingTe.Path.Replace("[x]", "")
                });

                bool displayCardinality = false;
                string? mappingWarningMessage = null;
                if (se.Min == 0 && matchingTe.Min == 1) //  an optional field was made mandatory
                {
                    displayCardinality = true;
                    mappingWarningMessage = " // Warning: source optional, target mandatory";
                }
                if (se.Max == "*" && matchingTe.Max == "1") // a repeating field was made single-valued
                {
                    displayCardinality = true;
                    mappingWarningMessage = " // Warning: source repeating, target single-valued";
                }

                // these types need to go into their own group and invocation
                if (se.Type.Any(t => t.Code == "BackboneElement" || t.Code == "Element"))
                {
                    rule.Sources[0].Variable = "s";
                    rule.Targets[0].Variable = "t";
                    rule.Dependent = new RuleDependent();
                    rule.Dependent.Invocations = new List<GroupInvocation>();

                    // Create a group name based on the source and target types
                    var groupName = ConceptMapConverter.PascalCase(se.Path.Replace("[x]", "")).Replace(".", "");

                    rule.Dependent.Invocations.Add(new GroupInvocation()
                    {
                        Name = groupName,
                        Parameters = [
                            new InvocationParameter() { Type = InvocationParameterType.Identifier, Value = "s" },
                        new InvocationParameter() { Type = InvocationParameterType.Identifier, Value = "t" }
                            ]
                    });

                    // Add a new group
                    var groupBackbone = new GroupDeclaration()
                    {
                        Name = groupName,
                        Extends = se.Type.First().Code
                    };
                    groupBackbone.Parameters.Add(new GroupParameter
                    {
                        Mode = ParameterMode.Source,
                        Name = "src"
                    });
                    groupBackbone.Parameters.Add(new GroupParameter
                    {
                        Mode = ParameterMode.Target,
                        Name = "tgt"
                    });

                    displayCardinality = true;

                    // Add cardinality comments before pushing the new group
                    if (displayCardinality)
                    {
                        rule.Sources[0].TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.Sources[0].TrailingHiddenTokens.Add(new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.COMMENT,
                            Text = $" /* [{se.Min}..{se.Max}] */"
                        });
                    }

                    if (displayCardinality)
                    {
                        rule.Targets[0].TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.Targets[0].TrailingHiddenTokens.Add(new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.COMMENT,
                            Text = $" /* [{matchingTe.Min}..{matchingTe.Max}] */"
                        });
                    }

                    // Add the rule to the PARENT group before pushing the child
                    groupStack.Peek().Rules.Add(rule);

                    if (se.Path.Replace("[x]", "") != matchingTe.Path.Replace("[x]", ""))
                    {
                        rule.TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.TrailingHiddenTokens.Add(new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.LINE_COMMENT,
                            Text = $" // renamed"
                        });
                    }

                    groupStack.Push(groupBackbone);
                    groupPathStack.Push(currentElementPath); // Push the full path of this BackboneElement
                    fml.Groups.Add(groupBackbone);
                }
                else
                {
                    // Check the types for missmatching types to include messages there too
                    string sourceTypes = String.Join(",", se.Type?.Select(t => t.Code));
                    string sourceTargetProfiles = String.Join(",", se.Type?.SelectMany(t => t.TargetProfile.Select(t => t.Replace("http://hl7.org/fhir/StructureDefinition/", ""))) ?? Enumerable.Empty<string>());
                    string targetTypes = String.Join(",", matchingTe.Type?.Select(t => t.Code));
                    string targetTargetProfiles = String.Join(",", matchingTe.Type?.SelectMany(t => t.TargetProfile.Select(t => t.Replace("http://hl7.org/fhir/StructureDefinition/", ""))) ?? Enumerable.Empty<string>());
                    if (!AreTypesCompatible(sourceTypes, targetTypes))
                    {
                        mappingWarningMessage = (mappingWarningMessage != null ? mappingWarningMessage + "    " : " // Warning: ") + $"Source Type unsupported: {InCompatibleTypes(sourceTypes, targetTypes)}  ({sourceTypes} -> {targetTypes})";
                    }
                    if (!AreTypesCompatible(sourceTargetProfiles, targetTargetProfiles))
                    {
                        mappingWarningMessage = (mappingWarningMessage != null ? mappingWarningMessage + "    " : " // Warning: ") + $"Source TargetProfile unsupported: {InCompatibleTypes(sourceTargetProfiles, targetTargetProfiles)}";
                    }

                    // Not a BackboneElement - just add cardinality comments and add rule normally
                    if (displayCardinality)
                    {
                        rule.Sources[0].TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.Sources[0].TrailingHiddenTokens.Add(new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.COMMENT,
                            Text = $" /* [{se.Min}..{se.Max}] */"
                        });
                    }

                    if (displayCardinality)
                    {
                        rule.Targets[0].TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.Targets[0].TrailingHiddenTokens.Add(new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.COMMENT,
                            Text = $" /* [{matchingTe.Min}..{matchingTe.Max}] */"
                        });
                    }

                    groupStack.Peek().Rules.Add(rule);

                    if (mappingWarningMessage != null)
                    {
                        rule.TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.TrailingHiddenTokens.Add(new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.LINE_COMMENT,
                            Text = mappingWarningMessage
                        });
                    }
                    if (se.Path.Replace("[x]", "") != matchingTe.Path.Replace("[x]", ""))
                    {
                        rule.TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.TrailingHiddenTokens.Add(new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.LINE_COMMENT,
                            Text = $" // renamed"
                        });
                    }
                }
            }
            else
            {
                missedSourceElements.Add(se);
            }
        }

        if (missedSourceElements.Any())
        {
            string comment = "\n\n  // The following source properties were not read:";
            foreach (var element in missedSourceElements)
            {
                var targetTypes = String.Join(",", element.Type?.Select(t => t.Code));
                comment += $"\n  //    {element.Path} {targetTypes}[{element.Min}..{element.Max}]";
                IEnumerable<string> targetProfiles = element.Type?.SelectMany(t => t.TargetProfile.Select(tp => tp.Replace("http://hl7.org/fhir/StructureDefinition/", ""))) ?? Enumerable.Empty<string>();
                if (targetProfiles.Any())
                    comment += $" ({String.Join(",", targetProfiles)})";
            }
            if (group.Rules.Any())
            {
                group.Rules.Last().TrailingHiddenTokens ??= new List<HiddenToken>();
                group.Rules.Last().TrailingHiddenTokens.Add(new HiddenToken()
                {
                    TokenType = FmlMappingLexer.LINE_COMMENT,
                    Text = comment
                });
            }
        }
        if (missedTargetElements.Any())
        {
            string comment = "\n\n  // The following target properties were not populated:";
            foreach (var element in missedTargetElements)
            {
                var targetTypes = String.Join(",", element.Type?.Select(t => t.Code));
                comment += $"\n  //    {element.Path} {targetTypes}[{element.Min}..{element.Max}]";
                IEnumerable<string> targetProfiles = element.Type?.SelectMany(t => t.TargetProfile.Select(tp => tp.Replace("http://hl7.org/fhir/StructureDefinition/", ""))) ?? Enumerable.Empty<string>();
                if (targetProfiles.Any())
                    comment += $" ({String.Join(",", targetProfiles)})";
            }
            if (group.Rules.Any())
            {
                group.Rules.Last().TrailingHiddenTokens ??= new List<HiddenToken>();
                group.Rules.Last().TrailingHiddenTokens.Add(new HiddenToken()
                {
                    TokenType = FmlMappingLexer.LINE_COMMENT,
                    Text = comment
                });
            }
        }

        return fml;
    }

    /// <summary>
    /// The left hand type is a compatible source to the right hand type.
    /// </summary>
    private readonly List<(string, string)> compatiblePairs = new List<(string, string)>
        {
            ("CodeableConcept", "CodeableReference"),
            ("Coding", "CodeableConcept"),
            ("date", "dateTime"),
            ("id", "string"),
            ("instant", "dateTime"),
            ("markdown", "string"),
            ("Reference", "CodeableReference"),
            ("Reference", "canonical"), // is this a legit mapping?
            ("string", "markdown"),
            ("string", "Annotation"),
            ("string", "CodeableConcept"),
            ("string", "CodeableReference"), // need to select where the string should go, display or text?
            ("string", "Reference"), // goes into the display property
            ("unsignedInt", "integer64"),
            ("uri", "canonical"),
            ("url", "uri"),
        };

    private bool AreTypesCompatible(string sourceTypes, string targetTypes)
    {
        var sourceTypeList = sourceTypes.Split(',').Select(t => t.Trim()).ToHashSet();
        var targetTypeList = targetTypes.Split(',').Select(t => t.Trim()).ToHashSet();

        // ensure that all source types are in target types
        // (the target having more types available is fine, just not the other way around)
        // the simple case
        if (sourceTypeList.IsSubsetOf(targetTypeList))
            return true;

        // remove any compatible pairs from the source/target lists
        foreach (var pair in compatiblePairs)
        {
            if (sourceTypeList.Contains(pair.Item1) && targetTypeList.Contains(pair.Item2))
            {
                sourceTypeList.Remove(pair.Item1);
            }
        }
        if (sourceTypeList.IsSubsetOf(targetTypeList))
            return true;

        return false;
    }

    private string InCompatibleTypes(string sourceTypes, string targetTypes)
    {
        var sourceTypeList = sourceTypes.Split(',').Select(t => t.Trim()).ToHashSet();
        var targetTypeList = targetTypes.Split(',').Select(t => t.Trim()).ToHashSet();

        // remove any compatible pairs from the source/target lists
        foreach (var pair in compatiblePairs)
        {
            if (sourceTypeList.Contains(pair.Item1) && targetTypeList.Contains(pair.Item2))
            {
                sourceTypeList.Remove(pair.Item1);
            }
        }

        // report any types in the source list that aren't in the target list
        var incompatibleTypes = sourceTypeList.Except(targetTypeList);
        return String.Join(",", incompatibleTypes);
    }

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
    private string? UseScope(FmlStructureMap map, string dt, StructureMode mode)
    {
        Canonical canonical = new Canonical(dt);

        string resourceType = canonical.Uri.Substring(canonical.Uri.LastIndexOf("/") + 1);
        string? fhirVersion = getFhirVersion(canonical.Version);
        map.Structures.Add(new StructureDeclaration()
        {
            Url = $"http://hl7.org/fhir/{canonical.Version?.Substring(0, 3)}/StructureDefinition/{resourceType}",
            Alias = resourceType + fhirVersion,
            Mode = mode
        });
        return resourceType + fhirVersion;
    }

    // Convert the ConceptMap (which has different groups for each resource/backbone type)
    //public FmlStructureMap ConvertToFml(ConceptMap conceptMap)
    //{
    //    // create a new FmlStructureMap
    //    // and create a new group in each group of the ConceptMap
    //    // metadata can be populated with ConceptMap metadata if needed
    //    // (This isn't going into the Fml ConceptMap structure, as these are conceptual mappings that
    //    // are being converted into actual executable structure mappings)

    //    var structureMap = new FmlStructureMap();
    //    structureMap.Structures = new();
    //    var sourceAlias = UseScope(structureMap, conceptMap.SourceScope, StructureMode.Source);
    //    var targetAlies = UseScope(structureMap, conceptMap.TargetScope, StructureMode.Target);

    //    // Populate metadata from ConceptMap if available
    //    if (!string.IsNullOrEmpty(conceptMap.Url))
    //    {
    //        SetMetadata(structureMap, "url", conceptMap.Url.Replace("ConceptMap", "StructureMap"));
    //    }

    //    if (!string.IsNullOrEmpty(conceptMap.Name))
    //    {
    //        SetMetadata(structureMap, "name", conceptMap.Name);
    //    }

    //    if (!string.IsNullOrEmpty(conceptMap.Title))
    //    {
    //        SetMetadata(structureMap, "title", conceptMap.Title);
    //    }

    //    if (!string.IsNullOrEmpty(conceptMap.Version))
    //    {
    //        SetMetadata(structureMap, "version", conceptMap.Version);
    //    }

    //    if (conceptMap.Status.HasValue)
    //    {
    //        SetMetadata(structureMap, "status", conceptMap.Status.Value.ToString().ToLowerInvariant());
    //    }
    //    else
    //    {
    //        // default the status to draft
    //        SetMetadata(structureMap, "status", "draft");
    //    }

    //    // Create map declaration if URL exists
    //    //if (!string.IsNullOrEmpty(conceptMap.Url))
    //    //{
    //    //    var identifier = conceptMap.Name ?? "ConceptMapToStructureMap";
    //    //    structureMap.MapDeclaration = new MapDeclaration
    //    //    {
    //    //        Url = conceptMap.Url.Replace("ConceptMap", "StructureMap"),
    //    //        Identifier = identifier
    //    //    };
    //    //}

    //    // Convert each ConceptMap group into a StructureMap group
    //    foreach (var group in conceptMap.Group)
    //    {
    //        var fmlGroup = ConvertGroupToFml(group);
    //        if (fmlGroup != null)
    //        {
    //            structureMap.Groups.Add(fmlGroup);
    //            // fmlGroup.TypeMode = GroupTypeMode.TypePlus;
    //            fmlGroup.Extends = "BackboneElement";
    //        }
    //    }

    //    // patch the first group's source/target to use the overall source/target scope aliases
    //    if (structureMap.Groups.Count > 0)
    //    {
    //        var firstGroup = structureMap.Groups[0];
    //        firstGroup.TypeMode = GroupTypeMode.TypePlus;
    //        firstGroup.Extends = "Resource";
    //        if (sourceAlias != null && firstGroup.Parameters.Count > 0)
    //        {
    //            firstGroup.Parameters[0].Type = sourceAlias;
    //        }
    //        if (targetAlies != null && firstGroup.Parameters.Count > 1)
    //        {
    //            firstGroup.Parameters[1].Type = targetAlies;
    //        }
    //        string sourceVersion = structureMap.Structures[0].Alias.Replace(firstGroup.Name, "").Replace("R", "");
    //        string targetVersion = structureMap.Structures[1].Alias.Replace(firstGroup.Name, "").Replace("R", "");
    //        SetMetadata(structureMap, "url", $"http://hl7.org/fhir/uv/xver/StructureMap/{firstGroup.Name}{sourceVersion}to{targetVersion}");
    //        SetMetadata(structureMap, "name", $"{firstGroup.Name}{sourceVersion}to{targetVersion}");
    //        SetMetadata(structureMap, "title", $"{firstGroup.Name} Transforms: {structureMap.Structures[0].Alias.Replace(firstGroup.Name, "")} to {structureMap.Structures[1].Alias.Replace(firstGroup.Name, "")}");

    //        structureMap.Imports ??= new List<ImportDeclaration>();
    //        structureMap.Imports.Add(new ImportDeclaration() { Url = $"http://hl7.org/fhir/uv/xver/StructureMap/*{sourceVersion}to{targetVersion}" });
    //    }

    //    return structureMap;
    //}

    public static string PascalCase(string input)
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
                        rule.TrailingHiddenTokens ??= new List<HiddenToken>();
                        rule.TrailingHiddenTokens.Add(ht);
                    }

                    if (target.Relationship != ConceptMap.ConceptMapRelationship.Equivalent)
                    {
                        var ht = new HiddenToken()
                        {
                            TokenType = FmlMappingLexer.LINE_COMMENT,
                            Text = $"  // {target.Relationship.GetLiteral()}"
                        };
                        rule.TrailingHiddenTokens ??= new List<HiddenToken>();
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
            rule.LeadingHiddenTokens ??= new List<HiddenToken>();
            rule.LeadingHiddenTokens.Add(ht);
        }

        return rule;
    }

    public static string ExtractTypeName(string structureDefinitionUrl)
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

    public static void SetMetadata(FmlStructureMap map, string name, string value)
    {
        map.Metadata ??= new List<MetadataDeclaration>();
        var metadata = map.Metadata.FirstOrDefault(m => m.Path == name);
        if (metadata == null)
        {
            metadata = new MetadataDeclaration();
            metadata.Path = name;
            map.Metadata.Add(metadata);
        }
        metadata.Value = value;
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
}
