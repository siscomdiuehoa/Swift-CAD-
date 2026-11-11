#!/bin/bash
# run.sh <lenh...> : chay lenh; neu loi, dua 25 dong cuoi thanh annotation ::error de doc duoc tren GitHub.
set -o pipefail
"$@" 2>&1 | tee /tmp/run-out.log
rc=$?
if [ $rc -ne 0 ]; then
  { grep -E ": (error|warning) |error|Error|fatal|Exception" /tmp/run-out.log | sort -u | head -20; tail -25 /tmp/run-out.log; } | sed 's/%/%25/g; s/^/::error::/'
fi
exit $rc
