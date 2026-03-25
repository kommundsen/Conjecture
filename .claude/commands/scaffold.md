Scaffold a new .NET project or module with correct structure.

## Input

$ARGUMENTS — one of:
- `solution` — create the solution file and core projects
- `project <name>` — add a new class library project
- `test <name>` — add a test project for an existing project
- `module <project> <name>` — add a new module (namespace folder + initial files) to an existing project

## Steps

### `solution`
1. Create `src/Hypothesis.slnx`
2. Create `src/Conjecture.Core/Conjecture.Core.csproj` (class library, net10.0)
3. Create `src/Conjecture.Tests/Conjecture.Tests.csproj` (xunit, net10.0)
4. Add project references and ensure `Directory.Packages.props` is used (no version attrs in csproj)
5. Verify `dotnet build src/` succeeds

### `project <name>`
1. Create `src/<name>/<name>.csproj` targeting net10.0
2. Add to `Hypothesis.slnx`
3. Verify build

### `test <name>`
1. Create `src/<name>.Tests/<name>.Tests.csproj` with xunit + project reference to `src/<name>`
2. Add to solution
3. Add a placeholder test that passes
4. Verify `dotnet test` succeeds

### `module <project> <name>`
1. Create folder `src/<project>/<Name>/`
2. Add an initial public class or interface file with file-scoped namespace
3. Add corresponding test file in the test project

## Guidelines

- Always use central package management (`Directory.Packages.props`)
- Follow `.editorconfig` conventions
- Target version compatible with global.json
- Use file-scoped namespaces
