#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "$script_dir/.." && pwd)"
runtime_identifier="${1:-osx-arm64}"
case "$runtime_identifier" in
  osx-arm64|osx-x64) ;;
  *)
    echo "Unsupported macOS runtime identifier: $runtime_identifier" >&2
    exit 2
    ;;
esac

dotnet_command="${DOTNET_COMMAND:-dotnet}"
publish_dir="$project_root/artifacts/publish/$runtime_identifier"
bundle_dir="$project_root/artifacts/$runtime_identifier/CodexUsage.app"

case "$publish_dir" in
  "$project_root/artifacts/publish/"*) ;;
  *) exit 2 ;;
esac
case "$bundle_dir" in
  "$project_root/artifacts/"*/CodexUsage.app) ;;
  *) exit 2 ;;
esac

rm -rf "$publish_dir" "$bundle_dir"
mkdir -p "$publish_dir" "$bundle_dir/Contents/MacOS" "$bundle_dir/Contents/Resources"

"$dotnet_command" publish "$project_root/src/CodexUsage.macOS/CodexUsage.macOS.csproj" \
  --configuration Release \
  --runtime "$runtime_identifier" \
  --self-contained true \
  --no-restore \
  -p:NuGetAudit=false \
  -p:UsedAvaloniaProducts= \
  --disable-build-servers \
  --tl:off \
  -m:1 \
  -p:UseSharedCompilation=false \
  -nodeReuse:false \
  --output "$publish_dir"

cp -R "$publish_dir/." "$bundle_dir/Contents/MacOS/"
cp "$project_root/src/CodexUsage.macOS/Info.plist" "$bundle_dir/Contents/Info.plist"
cp "$project_root/src/CodexUsage.macOS/Assets/codex-terminal-mark.png" "$bundle_dir/Contents/Resources/codex-terminal-mark.png"
chmod +x "$bundle_dir/Contents/MacOS/CodexUsage"
/usr/bin/codesign --force --deep --sign - "$bundle_dir"

echo "$bundle_dir"
