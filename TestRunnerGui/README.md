# TestRunnerGui

A small WinForms GUI that discovers NUnit Categories in a test project and runs tests using the NUnit Engine API.

What it does

- Builds the target test project and locates the compiled assembly
- Uses the NUnit Engine to Explore the test assembly and extract Category properties
- Runs tests filtered by the selected Category using the NUnit Engine, receiving live test events
- Displays live XML event output and a results grid with test name, outcome, and failure message

Prerequisites

- .NET SDK supporting net10.0
- For Playwright tests: ensure browsers are installed on the machine that will run the tests (refer to Playwright docs or run project-specific install steps)

Build & run

From the repository root:

```bash
dotnet restore TestRunnerGui
dotnet build TestRunnerGui
dotnet run --project TestRunnerGui
```

Usage

1. Browse to the test project's .csproj file (for example: Salary_Finance_(Preapproval).csproj)
2. Click "Discover Categories"
3. Select a category from the dropdown
4. Click "Run Selected Category"

Notes & caveats

- The GUI runs tests using NUnit Engine in separate worker processes; this is recommended for Playwright tests.
- If discovery fails to find categories, ensure the test project builds successfully and that the assembly's output folder contains required dependencies.
- The UI prints raw NUnit Engine XML events to the output window; parsing extracts test-case results for the results grid.
