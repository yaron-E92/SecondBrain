#!/usr/bin/env bash

set -Eeuo pipefail

readonly script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

export SECOND_BRAIN_FORCE_EMULATOR=true
export SECOND_BRAIN_HEADLESS=false
export SECOND_BRAIN_KEEP_EMULATOR=true

exec "$script_dir/test-linux-android.sh"
