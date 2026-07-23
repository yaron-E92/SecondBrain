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

The root `global.json` selects the .NET 10.0.200 SDK feature band and uses
`latestPatch` roll-forward. This allows the latest installed 10.0.2xx servicing
release, rejects later feature bands, and excludes prerelease SDKs. CI reads the
same file, so local and CI SDK selection follow one policy.
`Directory.Build.props` provides shared framework, nullable-reference-type, and
implicit-using defaults.

Install a stable .NET 10.0.2xx SDK, then confirm the selected SDK and policy from
the repository root:

```bash
dotnet --info
```

The output should identify a 10.0.2xx SDK and list this repository's
`global.json`. Run the standard solution checks with:

```bash
dotnet restore SecondBrain.slnx
dotnet build SecondBrain.slnx --configuration Debug --no-restore
dotnet test SecondBrain.slnx --configuration Debug --no-build --no-restore
```

## MAUI shell

`SecondBrain.App` is the MAUI presentation and composition-root project. The
local development target is Android on .NET 10, so install or restore the MAUI
Android workload before building the app. When using the repo automation VM,
the Android SDK and JDK are resolved from `$DOTNET_ROOT/android-sdk` and
`$DOTNET_ROOT/android-jdk`.

```bash
dotnet workload restore SecondBrain.App/SecondBrain.App.csproj
dotnet restore SecondBrain.App/SecondBrain.App.csproj
dotnet build SecondBrain.App/SecondBrain.App.csproj --no-restore
```
