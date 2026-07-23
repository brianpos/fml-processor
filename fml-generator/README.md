# fml-generator
The primary purpose of this project is to generate FHIR Mapping Language (FML) mappings between FHIR versions.

It leverages the FHIR Mapping Language (FML) assembly to provide the FML Text parsing/serialization, object models and validation.

## How it works
The basics:
* Walks the StructureDefintion's elements in source, then target
* Checks renames while walking the source and target elements to match the elements
* Checks type compatibility between source and target elements
* Where compatible, just maps the source to the target element
* Checks the custom rules for any exclusions
* Appends any custom rules that are not exclusions
* Any properties not mapped (from either side) are noted in the comments at the top of the FML output

Where things are not simple as shown above:
* source type not available in target: 
	* Reads backport extensions into the target
	* If the type is not available in the old version, the reading is more complex
	  (use a custom rule to convert - CodeableConcept to CodeableReference from R4)
	* cardinality differences (e.g. 0..1 vs 0..*)
* source reference type (for canonicals/references) not available in target:
	* ...
* target type not available in source:
	* ...
* target reference type (for canonicals/references) not available in source:
	* ...

> **Note:** The checker doesn't properly walk content references, so those need custom 
> rules to perform the mappings.
> From the initial reviews, this doesn't have any issues with incompatabilities (inside the rule possibly)

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

