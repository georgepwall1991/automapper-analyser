#!/usr/bin/env bash
# Enforces the project coverage target declared in codecov.yml, locally.
#
# codecov.yml declares a project target, but nothing in this repository enforced it: the upload step
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

floor="$(python3 - "$codecov_config" <<'PY'
import re, sys

text = open(sys.argv[1]).read()

# Scope extraction to the project.default mapping only. A body pattern that simply consumes indented
# lines runs past the end of project.default into patch.default when project declares no threshold,
# and then reads the patch threshold - quietly lowering the project floor. Cut the block at the first
# line indented no further than "default:" itself.
start = re.search(r'^(?P<indent>\s*)project:\s*\n\s*default:\s*\n', text, re.M)
if not start:
    sys.exit("check-coverage: could not read coverage.status.project.default from codecov.yml")

default_indent = len(re.match(r'\s*', text[start.end():]).group(0))
body_lines = []
for line in text[start.end():].splitlines():
    if not line.strip():
        continue
    if len(line) - len(line.lstrip()) < default_indent:
        break
    body_lines.append(line)
body = "\n".join(body_lines)

target = re.search(r'^\s*target:\s*([0-9.]+)%', body, re.M)
if not target:
    sys.exit("check-coverage: coverage.status.project.default declares no target")

# The declared threshold is the slack codecov allows below target before failing. Honour it so this
# gate and the hosted check agree, rather than this one being quietly stricter.
threshold = re.search(r'^\s*threshold:\s*([0-9.]+)%', body, re.M)
print(repr(float(target.group(1)) - (float(threshold.group(1)) if threshold else 0.0)))
PY
)"

report="$(find "$repo_root/tests" -name 'coverage.cobertura.xml' -print0 2>/dev/null \
  | xargs -0 ls -t 2>/dev/null | head -n 1 || true)"

if [[ -z "$report" ]]; then
  echo "check-coverage: no coverage.cobertura.xml found; run tests with --collect:\"XPlat Code Coverage\" first" >&2
  exit 1
fi

# Counted line by line rather than read from the report's own attributes. line-rate is rounded before
# it is written, so comparing it is not the full-precision check it appears to be, and lines-covered
# credits partially covered lines as covered where Codecov does not. Both shortcuts make the gate
# disagree with the target it claims to enforce.
actual="$(python3 - "$report" <<'PY'
import re, sys, xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()

# A partially covered line counts as neither hit nor missed, matching how Codecov separates partials
# from hits. This is not hypothetical for this repository: the current report contains 800 partial
# lines, and crediting them as covered reports 90.44% where Codecov reports 83.67%.
#
# Lines appear twice in a Cobertura document, under <class> and again under <method>. Only the
# class-level list is counted; iterating every <line> double-counts the file.
hits = partial = missed = 0
for class_element in root.iter("class"):
    for lines_element in (child for child in class_element if child.tag == "lines"):
        for line in lines_element:
            # Coverlet writes branch="True"/"False" with a capital letter; a case-sensitive comparison
            # matches nothing and silently counts every partial line as a full hit.
            is_branch = line.get("branch", "false").strip().lower() == "true"
            condition = re.search(r"\((\d+)/(\d+)\)", line.get("condition-coverage", ""))

            if is_branch and condition:
                # For branch lines Codecov derives status from the covered/total fraction and ignores
                # hits, which Coverlet may record as 0 even where some conditions were covered - 33 such
                # lines exist in this report. Requiring hits > 0 first counts them as missed and makes
                # the gate stricter than the target it claims to enforce, which near the floor would
                # reject a commit Codecov accepts.
                covered, total = int(condition.group(1)), int(condition.group(2))
                if covered == total:
                    hits += 1
                elif covered > 0:
                    partial += 1
                else:
                    missed += 1
            elif int(line.get("hits", "0")) > 0:
                hits += 1
            else:
                missed += 1

total = hits + partial + missed
if total > 0:
    print(repr(hits / total * 100))
else:
    # An empty or unfamiliar report shape falls back to the reported rate rather than dividing by zero.
    print(repr(float(root.get("line-rate")) * 100))
PY
)"

printf 'check-coverage: line coverage %.2f%%, floor %.2f%% (codecov.yml project target minus threshold)\n' \
  "$actual" "$floor"
printf 'check-coverage: report %s\n' "$report"

if python3 -c "import sys; sys.exit(0 if $actual < $floor else 1)"; then
  # Full precision in the failure, because a rounded display can read as equal to the floor it just
  # failed against, leaving the reader to wonder whether the gate is broken.
  printf 'check-coverage: FAILED - %s%% is below the %s%% floor this repository publishes in codecov.yml.\n' \
    "$actual" "$floor" >&2
  exit 1
fi

echo "check-coverage: OK"
