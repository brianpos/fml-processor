# fml-processor
The primary purpose of this project is to create and validate FHIR Mapping Language (FML) mappings between FHIR versions.

> *Note: The FSH processor project has been split into its own repository https://github.com/brianpos/fsh-processor*

## What has been done
* ANTLR-based FML parser with full source position tracking for IDE features
* FML serializer for round-tripping back to FML text
* FML validator for verifying data types and structure against StructureDefinitions
* Converting Gino's ConceptMaps to a complex type group mapping format
* Processing this new format
* Update the R6 StructureDefinition source location (so I can update faster from CI build)
* Split type selections
* Primitive type maps
* Generate from raw StructureDefinitions (not just Gino's ConceptMap format)

## Remaining work

## Update antlr grammar
```
java -cp c:/git/antlr-4.13.1-complete.jar  org.antlr.v4.Tool -Dlanguage=CSharp FmlMapping.g4 -visitor -listener
```