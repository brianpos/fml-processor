// <copyright file="CrossVersionMapCollection.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Navigation;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using Hl7.FhirPath;
using Microsoft.Health.Fhir.MappingLanguage;

namespace fml_processor;

public class NotReadElementAnnotation : ElementDefAnnotation
{
    public NotReadElementAnnotation(ElementDefinition definition) : base(definition)
    {
    }
}

public class NotPopulatedElementAnnotation : ElementDefAnnotation
{
    public NotPopulatedElementAnnotation(ElementDefinition definition) : base(definition)
    {
    }
}

public class ElementDefAnnotation
{
    public ElementDefAnnotation(ElementDefinition definition)
    {
        this.definition = definition;
    }

    public ElementDefinition definition { get; private set; }

    public string? RestrictedToType { get; set; }
}


public class NotReadElementTypeAnnotation : PropertyOrTypeDetailsAnnotation
{
    public NotReadElementTypeAnnotation(PropertyOrTypeDetails definition) : base(definition)
    {
    }
}

public class NotPopulatedElementTypeAnnotation : PropertyOrTypeDetailsAnnotation
{
    public NotPopulatedElementTypeAnnotation(PropertyOrTypeDetails definition) : base(definition)
    {
    }
}

public class PropertyOrTypeDetailsAnnotation
{
    public PropertyOrTypeDetailsAnnotation(PropertyOrTypeDetails pt)
    {
        this.definition = pt;
    }

    public PropertyOrTypeDetails definition { get; private set; }
}

public class NeedsReviewAnnotation
{
}

public class ElementRenamedAnnotation
{

}

public class WorkgroupAnnotation
{
    public WorkgroupAnnotation(string name)
    {
        this.name = name;
    }

    public string name { get; private set; }
}

public class MapCommentAnnotation
{
    public MapCommentAnnotation(string comment)
    {
        this.comment = comment;
    }

    public string comment { get; private set; }
}
