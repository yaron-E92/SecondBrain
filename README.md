# SecondBrain

SecondBrain is organized as a .NET 10 clean-architecture solution. Use
`SecondBrain.slnx` as the single solution entry point from the repository root.

## Repository layout

```text
src/
  Domain/          Enterprise and domain rules
  Application/     Use cases and application contracts
  Infrastructure/  Persistence and external service implementations
  Presentation/    API and user-interface entry points
tests/              Test projects mirroring projects under src
```

Projects belong under the matching area and use the `SecondBrain.<Area>` naming
pattern. Test projects use `SecondBrain.<Area>.Tests` and remain under `tests/`.
Concrete Brain projects, UI applications, and optional modules are added only by
their dedicated roadmap issues.

`Directory.Build.props` provides shared framework, nullable-reference-type, and implicit-using defaults.

## Continuous integration

GitHub Actions validates pull requests and pushes to `main` on
`ubuntu-latest`. The workflow installs the SDK selected by `global.json`, then
runs the following solution-level commands as separate steps:

```bash
dotnet restore SecondBrain.slnx
dotnet build SecondBrain.slnx --configuration Release --no-restore
dotnet test SecondBrain.slnx --configuration Release --no-build --no-restore
```

These commands include configured analyzers and architecture or test projects
that belong to the solution. The baseline CI job does not install MAUI workloads
or build device-specific targets; those require explicitly supported platform
runners and remain outside this solution-level check.
