# Peckworks RevPro API CLI

A .NET 10.0 command-line tool for interacting with the Zuora RevPro API. Downloads financial reports by date, splits large CSV files into smaller chunks, and repairs CSV files with UTF-8 encoding errors.

## Architecture

```
RevProAPICmd/
  Program.cs              - All application code (single file)
  Settings/
    revprosettings.json   - API configuration (credentials, URIs, directories)
  RevProAPICmd.csproj     - Project file (.NET 10.0, Newtonsoft.Json)
RevProAPICmd.Tests/
  RevProAPICmd.Tests.csproj
  SplitCsvTests.cs
  RepairCsvTests.cs
```

### Classes in Program.cs
- `Program` — Entry point, argument parsing, RunWithDate orchestration
- `SettingsFileRootObject` — JSON deserialization wrapper
- `RevproAPI` — Core class: API methods + static file utility methods
  - Nested: `RevproAPIOptions`, `RevproReportMetadata`, response classes

## CLI Commands

```
RevProAPICmd <date>                  # Download reports for date (MMddyyyy or MM-dd-yyyy)
RevProAPICmd -split <file_path>      # Split CSV into 10 parts, preserving headers
RevProAPICmd -repair <file_path>     # Detect and remove invalid UTF-8 bytes
RevProAPICmd -h                      # Help
```

## Tech Stack
- .NET 10.0 (C#)
- Newtonsoft.Json for serialization
- xUnit + FluentAssertions for testing
- HttpClient for API communication
- System.IO.Compression for ZIP extraction

## Known Issues

### RepairCsvFile — BROKEN
The UTF-8 error detection logic is fundamentally flawed:
- It reduces `byteCount` on each DecoderFallbackException but re-decodes from byte 0 each time
- Error position calculation `fs.Position - (bytesRead - byteCount + ex.Index)` drifts as byteCount decreases
- The removal pass uses a flat `List.Contains(position)` check which is O(n) per byte — unusable on large files
- Does not handle multi-byte UTF-8 sequences correctly (may remove valid continuation bytes)

**Fix approach:** Replace with a proper byte-by-byte or sequence-aware scan. Detect invalid sequences, skip them, write everything else. One pass, streaming.

### SplitCsv — WORKS but needs hardening
- No handling for files with only a header row (creates 10 empty split files)
- `Path.GetDirectoryName` can return null (crash on root-level paths)
- Creates all 10 split files even when the source has fewer rows than splitcount
- No validation that the file is actually CSV

## Operational Context
- The compiled exe runs on a scheduled cadence from a script (automated report downloads)
- SplitCsv and RepairCsv are run manually via command line when needed (rare, large dataset situations)
- No RevPro API credentials are available — API methods cannot be tested against a live endpoint
- The API methods (GetAuthToken, GetReportList, GetSignedURL, DownloadFileFromSignedURL, UnzipFile) have been used in production and work correctly, but may have minor issues (null checks, error handling gaps, HttpClient disposal) that can be fixed without integration testing
- WriteLog writes to a log file at the configured LogDir path

## Constraints
- NEVER change the settings JSON format — external systems depend on it
- NEVER store credentials or API tokens in code or commit them to git
- Keep the single-file architecture (Program.cs) — do not refactor into multiple source files
- API method fixes are limited to obvious defensive improvements (null checks, using statements, error handling) — no behavioral changes since they can't be integration tested

## Agent Workflow — Orchestrator Pattern

This project uses a multi-agent workflow for code improvements. Each agent has a focused role:

### Orchestrator (Claude Code main session)
Breaks work into tasks, routes to specialist agents, reviews output between steps.

### Agent Roles

**Coder Agent**
- Fix RepairCsvFile using a streaming single-pass approach
- Add error handling to SplitCsv (header-only files, null directory, empty files)
- Apply defensive fixes to API methods where issues are obvious (null checks, HttpClient disposal via using statements, missing error paths) — no behavioral changes
- Follow existing code style: same logging pattern, same Console.WriteLine conventions

**Tester Agent**
- Create xUnit test project (RevProAPICmd.Tests)
- Write tests for SplitCsv: valid split, header-only file, missing file, empty file, single-row file, file with fewer rows than splitcount
- Write tests for RepairCsvFile: clean file passes through unchanged, file with invalid UTF-8 bytes gets repaired, empty file, file containing only invalid bytes
- Tests create real CSV files in a temp directory, run the actual functions, and verify output files
- Cleanup temp files in teardown (use IDisposable or fixture pattern)
- Do NOT test API methods (no credentials available)
- Tests should be runnable via `dotnet test` with no external dependencies

**Reviewer Agent**
- Review all changes from Coder and Tester agents
- Check: edge cases covered, error handling consistent, no regressions to working code
- Verify RepairCsvFile fix handles multi-byte UTF-8 correctly
- Verify tests are independent (no shared state)
- Classify issues: BLOCKER / WARNING / NIT

**Docs Agent**
- Update README.md with current usage, build instructions, and test instructions
- Document the orchestrator workflow used to improve this codebase

## Development

```powershell
# Build
dotnet build RevProAPICmd/RevProAPICmd.csproj

# Run tests
dotnet test RevProAPICmd.Tests/RevProAPICmd.Tests.csproj

# Run CLI
dotnet run --project RevProAPICmd/RevProAPICmd.csproj -- -split "path/to/file.csv"
dotnet run --project RevProAPICmd/RevProAPICmd.csproj -- -repair "path/to/file.csv"
```
