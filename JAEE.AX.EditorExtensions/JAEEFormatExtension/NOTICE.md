# NOTICE

The X++ formatter in this project (`XppLexer`, `XppFormatter`) is a clean-room
reimplementation in C# of the **approach** used by the XppForge browser-based X++
formatter (https://xppforge.com/tools/xpp-formatter):

- tokenize the source (strings/comments/directives consumed whole),
- render layout from the token stream,
- **re-tokenize the output and abort if the significant token stream changed**
  (so a format can only move whitespace — never alter meaning).

No source code from XppForge (JavaScript or otherwise) was copied. The algorithmic
approach is not itself subject to copyright. This implementation additionally:

- handles X++ preprocessor directives (`#define` / `#localmacro` / …),
- preserves the author's line breaks inside `select` statements,
- re-aligns declaration and assignment columns,

which the referenced tool does not do.
