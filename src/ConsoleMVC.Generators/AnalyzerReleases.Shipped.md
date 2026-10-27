; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.2.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CVW001 | ConsoleMVC.Views | Warning | View file is not in a controller folder
CVW002 | ConsoleMVC.Views | Error | Invalid view or controller name
CVW003 | ConsoleMVC.Views | Warning | View name should end with 'View'
CVW100 | ConsoleMVC.Views | Error | Unknown attribute
CVW101 | ConsoleMVC.Views | Error | Invalid attribute value
CVW102 | ConsoleMVC.Views | Error | Element is not closed correctly
CVW103 | ConsoleMVC.Views | Error | Element not allowed here
CVW104 | ConsoleMVC.Views | Error | Unterminated construct
CVW105 | ConsoleMVC.Views | Error | Unknown element
CVW106 | ConsoleMVC.Views | Error | Code not allowed inside text
CVW107 | ConsoleMVC.Views | Error | Duplicate attribute
CVW108 | ConsoleMVC.Views | Error | Malformed tag
