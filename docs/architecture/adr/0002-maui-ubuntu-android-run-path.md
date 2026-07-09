# ADR 0002: MAUI Ubuntu Android Run Path

- Status: Accepted
- Date: 2026-07-09

## Context

SecondBrain is expected to consume shared MAUI presentation work, but this
repository does not yet contain a SecondBrain MAUI host project. The tracked
development path still needs to be explicit so the host can be added without
guessing where it belongs or how it should be smoke-tested on Ubuntu.

.NET MAUI does not provide a native Linux desktop target. On Ubuntu, the
supported development path for this repository is therefore Android through an
emulator or attached device. Windows, iOS, and MacCatalyst targets remain tied
to their native host operating systems and runners.

## Decision

The SecondBrain MAUI host or integration layer will live in the presentation
area as `SecondBrain.Presentation.Maui/SecondBrain.Presentation.Maui.csproj`
when it is added. The project is the SecondBrain composition root for MAUI and
may reference shared MAUI presentation code plus `SecondBrain.Abstractions`.
Domain, application, persistence, and optional module boundaries remain owned
by their existing projects and must not be moved into the MAUI host.

The MAUI host is responsible for app shell composition, navigation integration,
dependency injection, and translating shared MAUI presentation surfaces into
SecondBrain runtime behavior. Shared MAUI presentation work should expose UI
surfaces, view models, services, or registration hooks that the host can opt
into. It must not require SecondBrain core projects to depend on concrete
presentation projects, and it must not bypass module opt-in, permissions, or
data-access boundaries.

Once the host project exists, the Ubuntu smoke-test command is:

```bash
dotnet build SecondBrain.Presentation.Maui/SecondBrain.Presentation.Maui.csproj \
  -t:Run \
  -f net10.0-android \
  -p:EnableMauiTargets=true
```

This command intentionally targets Android. It is not a Linux desktop run
command.

## Ubuntu Prerequisites

Before running the MAUI host on Ubuntu, verify:

```bash
dotnet workload list
dotnet workload install maui-android
dotnet --info
```

The Android SDK must also be installed and discoverable by the .NET Android
tooling. Use either `ANDROID_HOME` or `ANDROID_SDK_ROOT`, and confirm that an
emulator or attached device is available before using `-t:Run`:

```bash
adb devices
```

If the MAUI workload is missing, install `maui-android` for the SDK version in
use and rerun the build command. If Android SDK discovery fails, set
`ANDROID_HOME` or `ANDROID_SDK_ROOT` to the SDK root and ensure platform tools
are on `PATH`. If no emulator or device is listed by `adb devices`, start an
Android emulator or attach a device with USB debugging enabled before retrying.

## Current Blocker

A full Ubuntu Android run is not feasible in this repository until
`SecondBrain.Presentation.Maui/SecondBrain.Presentation.Maui.csproj` exists.
Until then, this ADR is the repo-specific decision record for the MAUI host
location, the expected relationship to shared MAUI presentation work, and the
Android run command that should be enabled by the host issue.

## Non-Android Targets

- Windows MAUI targets require a Windows host with the appropriate MAUI and
  Windows tooling.
- iOS targets require Apple tooling and supported macOS runners.
- MacCatalyst targets require supported macOS runners.
- Ubuntu does not replace those native host requirements; it only covers the
  Android smoke-test path for this repository.
