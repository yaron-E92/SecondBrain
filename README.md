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

The root `global.json` pins the .NET 10 SDK, while `Directory.Build.props`
provides shared framework, nullable-reference-type, and implicit-using defaults.
