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
`Directory.Build.props` provides shared framework and C# defaults. Repository
projects inherit nullable reference types, implicit usings, C# 14, the .NET 10
analyzer baseline, build-time code-style enforcement, and warnings as errors.
The root `.editorconfig` defines the formatting and naming conventions.

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

Format the solution explicitly with:

```bash
dotnet format SecondBrain.slnx --no-restore
```

Verify whitespace formatting without changing files with:

```bash
dotnet format SecondBrain.slnx whitespace --verify-no-changes --no-restore
```

Run the verification command after restore. The standard build enforces
analyzers and code-style rules, and build warnings fail so new violations are
actionable. If an imported module cannot adopt a diagnostic immediately, scope
the exception to that module in its project file with `WarningsNotAsErrors`, or
to its path and diagnostic ID in `.editorconfig`. Do not weaken the
solution-wide policy for a module-specific exception.

## MAUI shell

`SecondBrain.Presentation` is the MAUI presentation and composition-root project. The
local development target is Android on .NET 10, so install or restore the MAUI
Android workload before building the app. When using the repo automation VM,
the Android SDK and JDK are resolved from `$DOTNET_ROOT/android-sdk` and
`$DOTNET_ROOT/android-jdk`.

```bash
dotnet workload restore SecondBrain.Presentation/SecondBrain.Presentation.csproj
dotnet restore SecondBrain.Presentation/SecondBrain.Presentation.csproj
dotnet build SecondBrain.Presentation/SecondBrain.Presentation.csproj --configuration Debug --no-restore
```

On Ubuntu, install Microsoft OpenJDK 21 and make the Android SDK available
through `ANDROID_HOME` or `ANDROID_SDK_ROOT`. Install the SDK components required
by the project with:

```bash
dotnet build SecondBrain.Presentation/SecondBrain.Presentation.csproj \
  -t:InstallAndroidDependencies \
  -f net10.0-android \
  -p:AcceptAndroidSdkLicenses=true
```

The Linux CI job runs that setup, builds and tests the solution, boots an
Android emulator, runs the app, and verifies the `com.secondbrain.app` process.
With an emulator running or a device attached, use the same smoke-test command
locally:

```bash
dotnet build SecondBrain.Presentation/SecondBrain.Presentation.csproj \
  --configuration Debug \
  --no-restore \
  -f net10.0-android \
  -t:Run
```

To set everything up, launch SecondBrain in a visible emulator, and leave that
emulator running for interactive use:

```bash
./scripts/run-linux-android.sh
```

The launcher creates or reuses the `secondbrain-test` x86_64 API 35 emulator.
After it reports that SecondBrain is running, use the app normally in the
emulator window. Stop the emulator when finished with:

```bash
"${ANDROID_HOME:-$ANDROID_SDK_ROOT}/platform-tools/adb" -s emulator-5554 emu kill
```

For a development-only, headless end-to-end check, the repository also provides
a smoke script that restores the workload and Android dependencies, builds the
app, uses an attached Android device when one is available or creates and starts
an x86_64 API 35 emulator, launches SecondBrain, verifies its process, and then
stops an emulator that it started:

```bash
./scripts/test-linux-android.sh
```

The scripts require `dotnet`, a JDK, `ANDROID_HOME` or `ANDROID_SDK_ROOT`, and
Android command-line tools. Hardware-accelerated emulator use also requires
access to `/dev/kvm`. They are development utilities and are not part of a
release artifact.
