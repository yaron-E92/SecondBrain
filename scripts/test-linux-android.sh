#!/usr/bin/env bash

set -Eeuo pipefail

readonly script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly repository_root="$(cd -- "$script_dir/.." && pwd)"
readonly project_path="$repository_root/SecondBrain.Presentation/SecondBrain.Presentation.csproj"
readonly application_id="com.secondbrain.app"
readonly api_level="${SECOND_BRAIN_ANDROID_API_LEVEL:-35}"
readonly system_image="system-images;android-${api_level};google_apis;x86_64"
readonly avd_name="${SECOND_BRAIN_AVD_NAME:-secondbrain-test}"
readonly emulator_port="${SECOND_BRAIN_EMULATOR_PORT:-5554}"
readonly emulator_serial="emulator-${emulator_port}"
readonly emulator_log="${SECOND_BRAIN_EMULATOR_LOG:-${RUNNER_TEMP:-${TMPDIR:-/tmp}}/secondbrain-android-emulator.log}"
readonly headless="${SECOND_BRAIN_HEADLESS:-true}"
readonly keep_emulator="${SECOND_BRAIN_KEEP_EMULATOR:-false}"
readonly force_emulator="${SECOND_BRAIN_FORCE_EMULATOR:-false}"

emulator_gpu="${SECOND_BRAIN_EMULATOR_GPU:-}"
if [[ -z "$emulator_gpu" ]]; then
  if [[ "$headless" == true ]]; then
    emulator_gpu=swiftshader_indirect
  else
    emulator_gpu=auto
  fi
fi
readonly emulator_gpu

started_emulator=false
selected_device=""

fail() {
  echo "error: $*" >&2
  exit 1
}

cleanup() {
  if [[ "$started_emulator" == true && "$keep_emulator" != true ]]; then
    "$adb" -s "$emulator_serial" emu kill >/dev/null 2>&1 || true
  fi
}

show_diagnostics() {
  if [[ -n "$selected_device" ]]; then
    "$adb" -s "$selected_device" logcat -d 2>/dev/null || true
  fi
  if [[ -f "$emulator_log" ]]; then
    echo "Android emulator log: $emulator_log" >&2
    cat "$emulator_log" >&2
  fi
}

trap cleanup EXIT
trap show_diagnostics ERR

[[ "$(uname -s)" == Linux ]] || fail "this Android script must run on Linux"
[[ -f "$project_path" ]] || fail "MAUI project not found at $project_path"
command -v dotnet >/dev/null || fail ".NET SDK is required"
command -v java >/dev/null || fail "a JDK is required; Microsoft OpenJDK 21 is used in CI"

android_sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
[[ -n "$android_sdk" ]] || fail "set ANDROID_HOME or ANDROID_SDK_ROOT to an Android SDK"

readonly sdkmanager="$android_sdk/cmdline-tools/latest/bin/sdkmanager"
readonly avdmanager="$android_sdk/cmdline-tools/latest/bin/avdmanager"
readonly adb="$android_sdk/platform-tools/adb"
readonly emulator="$android_sdk/emulator/emulator"

[[ -x "$sdkmanager" ]] || fail "sdkmanager not found at $sdkmanager"
[[ -x "$avdmanager" ]] || fail "avdmanager not found at $avdmanager"

cd "$repository_root"

dotnet workload restore "$project_path" --skip-manifest-update
dotnet build "$project_path" \
  -t:InstallAndroidDependencies \
  -f net10.0-android \
  -p:AcceptAndroidSdkLicenses=true
dotnet restore "$project_path"
dotnet build "$project_path" --configuration Debug --no-restore

"$sdkmanager" \
  "platform-tools" \
  "emulator" \
  "$system_image"

[[ -x "$adb" ]] || fail "adb not found at $adb after installing Android components"
[[ -x "$emulator" ]] || fail "emulator not found at $emulator after installing Android components"

"$adb" start-server >/dev/null
if [[ "$force_emulator" == true ]]; then
  selected_device="$("$adb" devices | awk -v serial="$emulator_serial" '$1 == serial && $2 == "device" { print $1; exit }')"
else
  selected_device="$("$adb" devices | awk 'NR > 1 && $2 == "device" { print $1; exit }')"
fi

if [[ -z "$selected_device" ]]; then
  if [[ -e /dev/kvm && ! -w /dev/kvm ]]; then
    if command -v sudo >/dev/null && sudo -n true 2>/dev/null; then
      sudo chmod 666 /dev/kvm
    else
      fail "the current user needs access to /dev/kvm (usually membership in the kvm group)"
    fi
  fi

  android_avd_home="${ANDROID_AVD_HOME:-$HOME/.android/avd}"
  export ANDROID_AVD_HOME="$android_avd_home"
  mkdir -p "$ANDROID_AVD_HOME" "$(dirname -- "$emulator_log")"

  if ! "$emulator" -list-avds | grep -Fxq "$avd_name"; then
    echo no | "$avdmanager" create avd \
      --force \
      --name "$avd_name" \
      --package "$system_image" \
      --device pixel
  fi

  emulator_arguments=(
    -avd "$avd_name"
    -port "$emulator_port"
    -no-audio
    -no-boot-anim
    -gpu "$emulator_gpu"
  )
  if [[ "$headless" == true ]]; then
    emulator_arguments+=(-no-window)
  fi

  nohup "$emulator" "${emulator_arguments[@]}" > "$emulator_log" 2>&1 &
  started_emulator=true
  selected_device="$emulator_serial"

  timeout 180 "$adb" -s "$selected_device" wait-for-device
  for _ in {1..120}; do
    if [[ "$("$adb" -s "$selected_device" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" == 1 ]]; then
      break
    fi
    sleep 5
  done
  [[ "$("$adb" -s "$selected_device" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" == 1 ]] || \
    fail "Android emulator did not finish booting within 10 minutes"
else
  echo "Using connected Android device: $selected_device"
fi

export ANDROID_SERIAL="$selected_device"
dotnet build "$project_path" \
  --configuration Debug \
  --no-restore \
  -f net10.0-android \
  -t:Run

for _ in {1..30}; do
  if [[ -n "$("$adb" -s "$selected_device" shell pidof "$application_id" 2>/dev/null | tr -d '\r')" ]]; then
    echo "SecondBrain is running on $selected_device."
    if [[ "$started_emulator" == true && "$keep_emulator" == true ]]; then
      echo "The emulator will remain running for interactive use."
      echo "Stop it with: $adb -s $selected_device emu kill"
    fi
    exit 0
  fi
  sleep 2
done

fail "SecondBrain did not start on $selected_device"
