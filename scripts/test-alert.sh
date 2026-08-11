#!/bin/bash
# Test alert webhook notification
# Usage: bash scripts/test-alert.sh

ALERT_WEBHOOK_URL=""

echo "Sending test alert to: "

curl -X POST "" \
  -H "Content-Type: application/json" \
  -d '{
  "title": "[TEST] Fish Social Alert",
  "message": "This is a test alert message from test-alert.sh",
  "severity": "info",
  "ruleName": "test_alert",
  "timestamp": 9999999999000
}'

echo "Done."
