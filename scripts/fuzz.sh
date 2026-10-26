#!/usr/bin/env bash
set -euo pipefail

mode=${1:?Usage: scripts/fuzz.sh <parse|decode> [seconds]}
seconds=${2:-60}
case "$mode" in
    parse|decode) ;;
    *) echo 'Target must be parse or decode.' >&2; exit 2 ;;
esac
if [[ ! "$seconds" =~ ^[1-9][0-9]*$ ]] || (( seconds > 86400 )); then
    echo 'Duration must be between 1 and 86400 seconds.' >&2
    exit 2
fi

root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$root"
output="artifacts/fuzz/$mode"
mkdir -p "$output/corpus" "$output/findings" artifacts/fuzz/tools

driver=artifacts/fuzz/tools/libfuzzer-dotnet
if [[ ! -f "$driver" ]]; then
    curl --fail --location --retry 2 \
        https://github.com/Metalnem/libfuzzer-dotnet/releases/download/v2025.05.02.0904/libfuzzer-dotnet-ubuntu \
        --output "$driver"
fi
printf '%s  %s\n' c2c2a90d94c409a4af339a0d4f244e0442c5a5d249be0f1252ba07871f285958 "$driver" | sha256sum --check
chmod +x "$driver"

dotnet tool restore
rm -rf -- "$output/bin"
dotnet publish Heic.Net.Fuzzing -c Release -o "$output/bin" --nologo
dotnet tool run sharpfuzz -- "$output/bin/Heic.Net.dll"

find corpus -maxdepth 1 -type f -name '*.heic' -size -65537c -exec cp -t "$output/corpus" -- {} +
find corpus/regressions -maxdepth 1 -type f -name '*.heic' -size -65537c -exec cp -t "$output/corpus" -- {} +

export HEIC_FUZZ_TARGET="$mode"
export DOTNET_GCHeapHardLimit=0x10000000
export DOTNET_PROCESSOR_COUNT=2
export DOTNET_TieredPGO=0
exec timeout --kill-after=10s "$((seconds + 60))s" "$driver" \
    -max_total_time="$seconds" -timeout=10 -max_len=65536 -rss_limit_mb=512 \
    -print_final_stats=1 -artifact_prefix="$output/findings/" \
    --target_path=dotnet --target_arg="$output/bin/Heic.Net.Fuzzing.dll" \
    "$output/corpus"
