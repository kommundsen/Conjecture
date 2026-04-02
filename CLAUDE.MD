- Be extremely concise in your responses. Sacrifice grammer for concision
- Never auto-commit code. Commits are to be done by user.
- Follow .editorconfig rules when writing code.
- Use workspace-relative paths in all tool calls (not absolute paths)

## Build & Test

```bash
dotnet build src/
dotnet test src/
dotnet test src/ --filter "FullyQualifiedName~SomeTest"
```