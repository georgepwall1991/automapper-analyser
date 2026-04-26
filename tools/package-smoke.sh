#!/usr/bin/env bash
set -euo pipefail

TARGET_FRAMEWORK="${1:-net10.0}"
CONFIGURATION="${CONFIGURATION:-Release}"
SMOKE_PACKAGE_VERSION="${SMOKE_PACKAGE_VERSION:-999.0.0-smoke}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ANALYZER_PROJECT="$REPO_ROOT/src/AutoMapperAnalyzer.Analyzers/AutoMapperAnalyzer.Analyzers.csproj"
WORK_ROOT="$REPO_ROOT/artifacts/package-smoke/$TARGET_FRAMEWORK"
PACKAGES_DIR="$WORK_ROOT/packages"
CONSUMER_DIR="$WORK_ROOT/consumer"
DOTNET="${DOTNET:-}"

if [ -z "$DOTNET" ]; then
  if command -v dotnet >/dev/null 2>&1; then
    DOTNET="$(command -v dotnet)"
  elif [ -x /opt/homebrew/bin/dotnet ]; then
    DOTNET=/opt/homebrew/bin/dotnet
  elif [ -x /usr/local/bin/dotnet ]; then
    DOTNET=/usr/local/bin/dotnet
  elif [ -x /usr/bin/dotnet ]; then
    DOTNET=/usr/bin/dotnet
  else
    echo "Could not locate dotnet. Set DOTNET=/path/to/dotnet and retry." >&2
    exit 127
  fi
fi

rm -rf "$WORK_ROOT"
mkdir -p "$PACKAGES_DIR"

"$DOTNET" restore "$ANALYZER_PROJECT"
"$DOTNET" build "$ANALYZER_PROJECT" --no-restore --configuration "$CONFIGURATION" -warnaserror
"$DOTNET" pack "$ANALYZER_PROJECT" \
  --no-build \
  --configuration "$CONFIGURATION" \
  --output "$PACKAGES_DIR" \
  /p:PackageVersion="$SMOKE_PACKAGE_VERSION"

NUPKG="$PACKAGES_DIR/AutoMapperAnalyzer.Analyzers.$SMOKE_PACKAGE_VERSION.nupkg"
test -f "$NUPKG"
unzip -l "$NUPKG" | grep -E "analyzers/dotnet/cs/AutoMapperAnalyzer.Analyzers.dll"
unzip -l "$NUPKG" | grep -E "README.md"
unzip -l "$NUPKG" | grep -E "icon.png"

"$DOTNET" new console --output "$CONSUMER_DIR" >/dev/null
sed -i.bak "s#<TargetFramework>[^<]*</TargetFramework>#<TargetFramework>$TARGET_FRAMEWORK</TargetFramework>#" "$CONSUMER_DIR/consumer.csproj"
rm -f "$CONSUMER_DIR/consumer.csproj.bak"
"$DOTNET" add "$CONSUMER_DIR/consumer.csproj" package AutoMapper --version 14.0.0 --no-restore
"$DOTNET" add "$CONSUMER_DIR/consumer.csproj" package AutoMapperAnalyzer.Analyzers \
  --version "$SMOKE_PACKAGE_VERSION" \
  --source "$PACKAGES_DIR" \
  --no-restore

cat > "$CONSUMER_DIR/Program.cs" <<'CS'
using AutoMapper;

namespace SmokeConsumer;

internal sealed class Source
{
    public string Age { get; set; } = string.Empty;
}

internal sealed class Destination
{
    public int Age { get; set; }
}

internal sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
    }
}

internal static class Program
{
    public static void Main()
    {
        Console.WriteLine(typeof(MappingProfile).FullName);
    }
}
CS

"$DOTNET" restore "$CONSUMER_DIR/consumer.csproj" \
  --source "$PACKAGES_DIR" \
  --source "https://api.nuget.org/v3/index.json"

set +e
BUILD_OUTPUT="$("$DOTNET" build "$CONSUMER_DIR/consumer.csproj" --no-restore --configuration Release --verbosity minimal 2>&1)"
BUILD_STATUS=$?
set -e

printf '%s\n' "$BUILD_OUTPUT"

if ! grep -q "AM001" <<<"$BUILD_OUTPUT"; then
  echo "Package smoke failed: expected AM001 from the installed analyzer package." >&2
  exit 1
fi

if [ "$BUILD_STATUS" -eq 0 ]; then
  echo "Package smoke failed: expected analyzer error AM001 to fail the consumer build." >&2
  exit 1
fi

echo "Package smoke passed for $TARGET_FRAMEWORK."
