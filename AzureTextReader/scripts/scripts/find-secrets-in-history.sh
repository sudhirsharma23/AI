#!/usr/bin/env bash
# Scans git history for potential secrets using simple patterns
set -e

PATTERNS=("SubscriptionKey" "Endpoint" "ConnectionString" "PASSWORD" "password" "PRIVATE_KEY" "AsposeLicensePath" "-----BEGIN PRIVATE KEY-----")

echo "Scanning git history for patterns: ${PATTERNS[*]}"

for p in "${PATTERNS[@]}"; do
  echo "\nSearching for pattern: $p"
  git rev-list --all | while read commit; do
    if git grep -n --cached -F --break --heading --line-number "$p" "$commit" >/dev/null 2>&1; then
      echo "Found in commit $commit:" 
      git grep -n --cached -F --heading --line-number "$p" "$commit" || true
    fi
  done
done

echo "Scan complete. If matches were found rotate keys immediately and consider removing them from history with git-filter-repo or BFG." 
