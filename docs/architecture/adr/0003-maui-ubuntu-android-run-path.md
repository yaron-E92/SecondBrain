# ADR 0003: MAUI Ubuntu Android Run Path

- Status: Accepted
- Date: 2026-07-09

## Context

`SecondBrain.Presentation/SecondBrain.Presentation.csproj` is the existing MAUI presentation and
composition-root project. It targets `net10.0-android`. .NET MAUI does not
provide a native Linux desktop target, so Ubuntu supports this repository's
MAUI development path through Android on an emulator or attached device.
Windows, iOS, and MacCatalyst targets remain tied to their native host operating
systems and runners.

## Decision

`SecondBrain.Presentation` composes the app shell, navigation integration, dependency
injection, and the translation of shared MAUI presentation surfaces into
SecondBrain runtime behavior. It may reference shared MAUI presentation work
and the existing application contracts. Domain, application, persistence, and
optional-module boundaries remain owned by their existing projects and must not
be moved into the MAUI host.

Shared MAUI presentation work should expose UI surfaces, view models, services,
or registration hooks that `SecondBrain.Presentation` can opt into. It must not require
SecondBrain core projects to depend on concrete presentation projects, and it
must not bypass module opt-in, permissions, or data-access boundaries.

## Ubuntu Android Workflow

From the repository root, restore the workload and project dependencies, then
build the Android app:

```bash
dotnet workload restore SecondBrain.Presentation/SecondBrain.Presentation.csproj
dotnet restore SecondBrain.Presentation/SecondBrain.Presentation.csproj
dotnet build SecondBrain.Presentation/SecondBrain.Presentation.csproj --configuration Debug --no-restore
```

With an Android emulator running or a device attached, the Android smoke test
uses the MAUI Android `Run` target:

```bash
dotnet build SecondBrain.Presentation/SecondBrain.Presentation.csproj --configuration Debug --no-restore -f net10.0-android -t:Run
```

This is an Android command, not a native Linux desktop run command.

The repository's Linux CI job exercises this workflow on Ubuntu: it restores
the workload and Android dependencies, builds and tests the solution, starts a
bounded Android emulator, invokes the `Run` target, and verifies that the
`com.secondbrain.app` process is running. This makes the Linux Android path a
tested repository contract rather than a documentation-only procedure.

## Environment Prerequisites and Blockers

The remaining blockers for an Ubuntu Android run are environment prerequisites,
not a missing host project: the MAUI Android workload, Android SDK and JDK, and
an available emulator or USB-debuggable attached device. Verify the installed
workloads and device visibility with:

```bash
dotnet workload list
dotnet --info
adb devices
```

The Android SDK must be discoverable by the .NET Android tooling. Set
`ANDROID_HOME` or `ANDROID_SDK_ROOT` when needed, and ensure Android platform
tools are on `PATH`. In the repository automation VM, the app project also
resolves the Android SDK and JDK from `$DOTNET_ROOT/android-sdk` and
`$DOTNET_ROOT/android-jdk` when those directories exist. If `adb devices` shows
no available device, start an emulator or attach a device before running the
smoke test.

## Non-Android Targets

- Windows MAUI targets require a Windows host with the appropriate MAUI and
  Windows tooling.
- iOS targets require Apple tooling and supported macOS runners.
- MacCatalyst targets require supported macOS runners.
- Ubuntu does not replace those native host requirements; it covers the Android
  smoke-test path for this repository only.
