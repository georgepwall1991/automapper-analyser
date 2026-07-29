#!/usr/bin/env bash
# Enforces the project coverage target declared in codecov.yml, locally.
#
# codecov.yml declares a project target, but nothing in this repository enforces it: the upload step
# sets fail_ci_if_error: false, so a failed or missing upload is not a build failure, and the status
# check itself lives in a hosted service that can be unavailable, unconfigured on a fork, or missing a
# token. The declared target was therefore aspirational rather than enforced.
#
# The threshold is read from codecov.yml rather than repeated here. Two copies of a number that must
# agree is how the number silently stops agreeing.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
codecov_config="$repo_root/codecov.yml"

if [[ ! -f "$codecov_config" ]]; then
  echo "check-coverage: codecov.yml not found at $codecov_config" >&2
  exit 1
fi

# The project target and its allowed slack, as declared.
target="$(python3 - "$codecov_config" <<'PY'
import re, sys
text = open(sys.argv[1]).read()
project = re.search(r'project:\s*\n\s*default:\s*\n(?P<body>(?:\s+\w+:.*\n)+)', text)
if not project:
    sys.exit("check-coverage: could not read coverage.status.project.default from codecov.yml")
target = re.search(r'target:\s*([0-9.]+)%', project.group('body'))
threshold = re.search(r'threshold:\s*([0-9.]+)%', project.group('body'))
if not target:
    sys.exit("check-coverage: coverage.status.project.default declares no target")
# The declared threshold is the slack codecov allows below target before failing; honour it so this
# gate and the hosted check agree rather than this one being stricter.
print(float(target.group(1)) - (float(threshold.group(1)) if threshold else 0.0))
PY
)"

report="$(find "$repo_root/tests" -name 'coverage.cobertura.xml' -print0 2>/dev/null \
  | xargs -0 ls -t 2>/dev/null | head -n 1 || true)"

if [[ -z "$report" ]]; then
  echo "check-coverage: no coverage.cobertura.xml found; run tests with --collect:\"XPlat Code Coverage\" first" >&2
  exit 1
fi

actual="$(python3 - "$report" <<'PY'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
print(f"{float(root.get('line-rate')) * 100:.2f}")
PY
)"

printf 'check-coverage: line coverage %s%%, floor %s%% (codecov.yml project target minus threshold)\n' \
  "$actual" "$target"
printf 'check-coverage: report %s\n' "$report"

if (( $(python3 -c "print(1 if float('$actual') < float('$target') else 0)") )); then
  echo "check-coverage: FAILED - coverage is below the target this repository publishes in codecov.yml." >&2
  exit 1
fi

echo "check-coverage: OK"
