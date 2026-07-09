# fml-generator
The primary purpose of this project is to generate FHIR Mapping Language (FML) mappings between FHIR versions.

It leverages the FHIR Mapping Language (FML) assembly to provide the FML Text parsing/serialization, object models and validation.

## What has been done
* Converting Gino's ConceptMaps to a complex type group mapping format
* Split type selections
* Primitive type maps
* Generate from raw StructureDefinitions (not just Gino's ConceptMap format)

## Remaining work
* Duplicate group name detection and mapping
* Content reference for types
    * ClinicalUseDefinitionContraindication `indication` property
* Required code binding switches (if that makes sense)
* Reference type switching (if there are reduced target types)
    * `src.id as patientId where (is(FHIR.id)) -> entry.fullUrl = append('http://hl7.org/fhir/us/sdoh-clinicalcare/Patient/', patientId);`
    * `src.id as patientId where (is(FHIR.id).not()) -> entry.extension as e, e.url = 'http://.../Patient/', e.value = patientId;`
* Backport extension injection
* Walking into datatypes where was converted to/from a backbone element

## Other possible future activities
* Generate javascript code
* Generate dotnet code
* Generate dotnet Expressions dynamically https://github.com/dadhi/FastEXpressionCompiler?tab=readme-ov-file

