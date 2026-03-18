# Repository Guidelines

## Project Structure & Module Organization
`src/PasteTool.App` contains the WPF desktop application: windows in `Windows/`, reusable UI pieces in `Controls/`, and startup/orchestration in `AppController.cs` and `Infrastructure/`.
`src/PasteTool.Core` contains shared models, services, native interop, and clipboard/image utilities.
`tests/PasteTool.Core.Tests` contains xUnit tests for core behavior.
`publish/` is build output for release binaries and should be treated as generated artifacts.

## Build, Test, and Development Commands
- `dotnet build src\PasteTool.App\PasteTool.App.csproj -p:NuGetAudit=false -v minimal`
  Builds the WPF app in Debug.
- `dotnet test tests\PasteTool.Core.Tests\PasteTool.Core.Tests.csproj -p:NuGetAudit=false -v minimal`
  Runs the xUnit suite for core services and utilities.
- `dotnet publish src\PasteTool.App\PasteTool.App.csproj -c Release -r win-x64 -p:NuGetAudit=false -o publish`
  Produces the single-file Windows release build in `publish/`.

After every code change, generate a fresh Release package with the publish command above.

## Coding Style & Naming Conventions
Use 4-space indentation and keep existing C# conventions:
- file-scoped namespaces
- `PascalCase` for types, methods, properties, and XAML element names
- `_camelCase` for private readonly fields
- one public type per file when practical

Match the existing style before introducing new patterns. Keep comments sparse and only where logic is not obvious.

## Testing Guidelines
Tests use xUnit and live under `tests/PasteTool.Core.Tests`.
Name test files after the subject under test, for example `ClipboardPayloadReaderTests.cs`.
Name test methods as `Method_Scenario_ExpectedResult`.
Add or update tests for core logic changes, especially clipboard parsing, persistence, search, and image handling.

## Commit & Pull Request Guidelines
Git history is not available in this workspace, so no repository-specific commit convention can be inferred. Use short, imperative commit messages such as `Fix blank image preview`.
For pull requests, include:
- a short problem/solution summary
- test evidence (`dotnet test ...`, `dotnet build ...`)
- screenshots for WPF UI changes

## Debugging & Configuration Tips
Runtime data is stored under `%LOCALAPPDATA%\PasteTool`.
Useful logs:
- `%TEMP%\pastetool_debug.log`
- `%LOCALAPPDATA%\PasteTool\logs\`
