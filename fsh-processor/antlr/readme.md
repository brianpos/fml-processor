## Antlr files
https://github.com/FHIR/sushi/tree/v3.8.0/antlr/src/main/antlr

Use the FSH.g4 and FDHLexer.g4 files, not the Mini ones.

### Regenerating the parser
To regenerate the parser files from the Antlr grammar, you will need to have ANTLR 4 installed. You can download it from the [official ANTLR website](https://www.antlr.org/).

``` powershell
java -jar c:\git\antlr-4.13.1.jar -Dlanguage=CSharp -visitor -package fsh_processor.antlr -o fsh-processor\antlr fsh-processor\antlr\FSHLexer.g4 fsh-processor\antlr\FSH.g4
```
