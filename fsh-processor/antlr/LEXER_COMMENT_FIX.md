# FSH Lexer Comment Handling Fix

## Problem

The original FSHLexer.g4 had an issue with standalone comment-only lines. Comments that appeared at the beginning of a line (column 0) without being followed by a rule caused parse errors.

### Example of Failing Code
```fsh
Logical: SDCExample
Parent: Element
* gender 0..1 CodeableConcept "The gender..."
* gender from http://hl7.org/fhir/ValueSet/administrative-gender (required)
//  * ^base.path = "dataelement-sdc-profile-example.gender"
//  * ^base.min = 0
//  * ^base.max = "1"
```

Lines with `//` at column 0 were causing:
```
extraneous input '//' expecting {<EOF>, KW_ALIAS, KW_PROFILE, ...}
```

## Root Cause Analysis

The issue went through several iterations before finding the correct solution:

### Original Lexer (Problematic)
```antlr
STAR: ([\r\n] | LINE_COMMENT) WS* '*' [ \u00A0];
LINE_COMMENT: '//' ~[\r\n]* [\r\n] -> skip;
```

**Problems:**
1. LINE_COMMENT used `-> skip` which completely discarded comments (no round-trip preservation)
2. LINE_COMMENT consumed the trailing newline `[\r\n]`
3. When LINE_COMMENT consumed the newline, the next line didn't start with `\r\n`, breaking STAR matching

### First Fix Attempt (Incomplete)
```antlr
STAR: ([\r\n] WS* | LINE_COMMENT WS*) '*' [ \u00A0];
LINE_COMMENT: '//' ~[\r\n]* [\r\n] -> channel(HIDDEN);
```

**Problems:**
1. Changed `-> skip` to `-> channel(HIDDEN)` ? (good for preservation)
2. Made STAR pattern more complex with double `WS*` (unnecessarily greedy)
3. **Still consumed the newline in LINE_COMMENT**, causing standalone comments to fail

### Investigation & Revert
Through token stream debugging, we discovered that the double `WS*` in STAR and the newline consumption in LINE_COMMENT were causing issues. We reverted STAR to the original simpler pattern but kept the HIDDEN channel change.

```antlr
STAR: ([\r\n] | LINE_COMMENT) WS* '*' [ \u00A0];
LINE_COMMENT: '//' ~[\r\n]* [\r\n] -> channel(HIDDEN);
```

**Result:** Better, but standalone comments still failed because LINE_COMMENT consumed the newline.

## Final Solution

### Changes Made to FSHLexer.g4

#### 1. STAR Token Pattern (Line 68) - Kept Simple
```antlr
STAR: ([\r\n] | LINE_COMMENT) WS* '*' [ \u00A0];
```

**Why:** The original simple pattern works correctly. The STAR token expects either:
- A newline followed by optional whitespace and `*`, OR
- A LINE_COMMENT followed by optional whitespace and `*`

#### 2. BLOCK_COMMENT to channel(HIDDEN) (Line 119)
**Before:**
```antlr
BLOCK_COMMENT: '/*' .*? '*/' -> skip;
```

**After:**
```antlr
BLOCK_COMMENT: '/*' .*? '*/' -> channel(HIDDEN);
```

**Why:** Preserve block comments for round-trip serialization.

#### 3. LINE_COMMENT: Remove Trailing Newline and Use HIDDEN Channel (Line 131)
**Before:**
```antlr
LINE_COMMENT: '//' ~[\r\n]* [\r\n] -> skip;
```

**After:**
```antlr
LINE_COMMENT: '//' ~[\r\n]* -> channel(HIDDEN);
```

**Why (CRITICAL):** 
- **Changed `-> skip` to `-> channel(HIDDEN)`** for round-trip preservation
- **Removed `[\r\n]` from the pattern** - This is the key fix!
  - LINE_COMMENT now only consumes: `//` and characters up to (but not including) the newline
  - The newline becomes a separate WHITESPACE token (also on HIDDEN channel)
  - When a standalone comment line appears, the next line properly starts with `\r\n`
  - This allows STAR tokens on the next line to match correctly

## Impact

### Before Fix
- **Parse Success Rate:** 208/209 files (99.5%)
- **Failed Files:** 1 (SDCExample.fsh)
- **Parse Error:** `extraneous input '//' expecting {<EOF>, KW_ALIAS, KW_PROFILE, ...}`
- **Comments:** Discarded via `-> skip`, not available for serialization

### After Fix
- **Parse Success Rate:** 209/209 files (**100%** ?)
- **Failed Files:** 0
- **Comments:** Preserved on HIDDEN channel
- **Round-trip:** Comments available via `GetLeadingHiddenTokens`/`GetTrailingHiddenTokens`
- **Test Suite:** All 30 tests passing

## Testing

### Test File: SDCExample.fsh (Previously Failing)
```fsh
Logical: SDCExample
Parent: Element
Id: ProfileExample
Title: "Patient Gender"
Description: "Data element SDC Profile Example"
* ^identifier.system = "http://example.org/nlm/some_other_text/data_element_identifier"
* ^identifier.value = "DE42AHRQ"
* ^status = #active
* ^publisher = "Health Level Seven, International"
* ^contact.telecom.system = #other
* ^contact.telecom.value = "http://hl7.org"
//* ^type = "DataelementSdcProfileExample"
* gender 0..1 CodeableConcept "The gender..."
* gender from http://hl7.org/fhir/ValueSet/administrative-gender (required)
//  * ^base.path = "dataelement-sdc-profile-example.gender"
//  * ^base.min = 0
//  * ^base.max = "1"
```

**Result:** Now parses successfully! ?

### Test Coverage
- **`ParseSingleFile_SDCExample_WithStandaloneComments`** - Verifies standalone comments parse correctly
- **`TestRoundTripAllSDCFiles`** - All 209 real-world SDC IG files parse and round-trip successfully
- **`TestCommentPreservation`** - Comments preserved in entity headers
- **`MultilineStringTest`** - Triple-quoted strings work correctly

### Full Test Results
```
Test summary: total: 30, failed: 0, succeeded: 30, skipped: 0
```

## Benefits

1. **100% Parse Success** - All 209 real-world FSH files from SDC IG now parse successfully
2. **Comment Preservation** - Both line and block comments preserved on HIDDEN channel for round-trip serialization
3. **Cleaner Token Boundaries** - Newlines are separate from comments, making token stream more predictable
4. **Simpler STAR Pattern** - Kept the original simple pattern instead of over-engineering
5. **Better Debugging** - TOKEN stream inspection tools can see comment tokens clearly

## Lessons Learned

1. **Lexer token boundaries matter** - Including `[\r\n]` in LINE_COMMENT broke the invariant that line starts have newlines
2. **Simple patterns are better** - The original STAR pattern was fine; the issue was elsewhere
3. **HIDDEN vs skip** - Use `channel(HIDDEN)` for tokens you want to preserve but not parse
4. **Token stream debugging is essential** - Raw ANTLR token inspection (not parse tree) reveals lexer issues
5. **Iterative refinement** - The solution went through multiple iterations before finding the root cause

## Related Code Changes

### FshModelVisitor.cs
No changes needed! The visitor's `GetLeadingHiddenTokens` and `GetTrailingHiddenTokens` methods work correctly with the new token structure.

**Key observation:** Since LINE_COMMENT no longer includes `\r\n`, the logic in `GetTrailingHiddenTokens` that stops when encountering a token with newline now works perfectly:
- Line comments are captured as trailing tokens
- The newline after the comment causes the loop to stop
- Next rule's leading tokens don't accidentally capture the previous rule's trailing comment

## Regenerating Lexer

After modifying FSHLexer.g4:

```powershell
java -jar c:\git\antlr-4.13.1.jar -Dlanguage=CSharp -visitor -package fsh_processor.antlr -o fsh-processor\antlr fsh-processor\antlr\FSHLexer.g4 fsh-processor\antlr\FSH.g4
```

Then rebuild the project.

## Related Files

- `fsh-processor/antlr/FSHLexer.g4` - Modified lexer grammar
- `fsh-processor/antlr/FSHLexer.cs` - Generated lexer (regenerated)
- `fsh-processor/Visitors/FshModelVisitor.cs` - Uses GetLeadingHiddenTokens/GetTrailingHiddenTokens
- `fsh-processor/Models/HiddenToken.cs` - Stores preserved comments
- `fsh-tester/ParserTests.cs` - Test validation

## Notes

- **STAR pattern limitation:** Comments that appear on the same line as a rule's `*` (inline comments) are still absorbed into the STAR token itself due to the pattern `([\r\n] | LINE_COMMENT) WS* '*'`. This is by design and would require parser-level changes to handle differently.
- **Newline handling:** Newlines are now consistently handled as separate WHITESPACE tokens on the HIDDEN channel
- **All ANTLR modes work correctly:** RULESET_OR_INSERT, PARAM_RULESET_OR_INSERT, LIST_OF_CONTEXTS, LIST_OF_CODES all work with the new pattern
- **Backward compatible:** Files without comments parse identically to before
- **No visitor changes needed:** The existing hidden token preservation logic in FshModelVisitor works perfectly with the cleaner token boundaries

## Summary

The fix involved two key changes:
1. **`LINE_COMMENT: '//' ~[\r\n]* -> channel(HIDDEN);`** - Removed `[\r\n]` and changed from `skip` to `channel(HIDDEN)`
2. **`STAR` pattern kept simple** - Original pattern `([\r\n] | LINE_COMMENT) WS* '*'` was correct

The critical insight was that LINE_COMMENT should NOT consume the newline. This allows standalone comment lines to work while preserving comments for round-trip serialization.
