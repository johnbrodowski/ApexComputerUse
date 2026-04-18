# ProjectMapBuilder

Small C# utility to generate an AI-friendly call map of the repository.

## What it outputs

- `project-map.json` — machine-readable map with method/constructor nodes and internal call edges.
- `project-map.md` — quick summary with top callers and sample edges.

## Run

From repository root:

```bash
dotnet run --project Tools/ProjectMapBuilder/ProjectMapBuilder.csproj -- --root . --out docs/project-map
```

PowerShell wrapper:

```powershell
pwsh ./Tools/ProjectMapBuilder/Generate-ProjectMap.ps1
```

## Notes

- Traces internal calls via Roslyn semantic analysis (`InvocationExpression` and constructor calls).
- Excludes files under `bin/` and `obj/`.
- Keeps external framework/library calls out of the edge list.
