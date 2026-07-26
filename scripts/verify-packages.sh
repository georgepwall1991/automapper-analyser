#!/usr/bin/env bash
# Verify packed AutoMapperAnalyzer.Analyzers nupkg contents and discoverability metadata.
set -euo pipefail

package_dir="${1:-artifacts/packages}"
version="$(dotnet msbuild src/AutoMapperAnalyzer.Analyzers/AutoMapperAnalyzer.Analyzers.csproj -getProperty:PackageVersion -nologo | tr -d '[:space:]')"

analyzer_package="$package_dir/AutoMapperAnalyzer.Analyzers.$version.nupkg"

if [[ ! -f "$analyzer_package" ]]; then
  # Fallback: accept any single matching nupkg when version property resolution differs.
  shopt -s nullglob
  matches=("$package_dir"/AutoMapperAnalyzer.Analyzers.*.nupkg)
  if (( ${#matches[@]} != 1 )); then
    echo "Expected AutoMapperAnalyzer.Analyzers.$version.nupkg in $package_dir (found ${#matches[@]})." >&2
    exit 1
  fi
  analyzer_package="${matches[0]}"
fi

test -f "$analyzer_package"

contents="$(unzip -Z1 "$analyzer_package")"
case "$contents" in *"analyzers/dotnet/cs/AutoMapperAnalyzer.Analyzers.dll"*) ;; *)
  echo "Package missing analyzers/dotnet/cs/AutoMapperAnalyzer.Analyzers.dll" >&2
  exit 1
  ;;
esac
case "$contents" in *"README.md"*) ;; *)
  echo "Package missing README.md" >&2
  exit 1
  ;;
esac
case "$contents" in *"icon.png"*) ;; *)
  echo "Package missing icon.png" >&2
  exit 1
  ;;
esac

cmp README.md <(unzip -p "$analyzer_package" README.md)
cmp icon.png <(unzip -p "$analyzer_package" icon.png)

# Product-flow visuals referenced by PackageReadmeFile must ship inside the package.
for asset in \
  assets/flow-ide-diagnostics.svg \
  assets/flow-before-after-fix.svg \
  assets/flow-analyzer-ci-loop.svg
do
  cmp "$asset" <(unzip -p "$analyzer_package" "$asset")
done

# Discoverability metadata: high-intent AutoMapper / CreateMap terms (NuGet search).
nuspec="$(unzip -p "$analyzer_package" AutoMapperAnalyzer.Analyzers.nuspec)"

for term in CreateMap Profile ForMember MapFrom ProjectTo AutoMapperMappingException nullable unmapped roslyn-analyzer; do
  printf '%s' "$nuspec" | grep -Fq "$term" || {
    echo "Analyzer nuspec missing discoverability term: $term" >&2
    exit 1
  }
done

echo "Verified package payload, README, assets, and discoverability metadata for $analyzer_package."
