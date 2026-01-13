#!/usr/bin/env bash
# Instructions to use BFG to remove sensitive values from git history
# Requires installing BFG (https://rtyley.github.io/bfg-repo-cleaner/)

set -e

if [ -z "$1" ]; then
  echo "Usage: $0 <string-to-remove>"
  exit 2
fi

SECRET="$1"

echo "This script will remove all occurrences of '$SECRET' from your git history using BFG."
read -p "Continue? [y/N] " confirm
if [[ "$confirm" != "y" && "$confirm" != "Y" ]]; then
  echo "Aborted"
  exit 1
fi

# Make a fresh clone
git clone --mirror $(git remote get-url origin) repo-mirror.git
cd repo-mirror.git

# Run BFG
bfg --delete-files "$SECRET" || true
bfg --delete-text-lines "$SECRET" || true

# Cleanup and push
git reflog expire --expire=now --all
git gc --prune=now --aggressive

echo "Pushing cleaned repo back to origin (force push)."
read -p "Are you ready to force-push to origin? This will rewrite history. [y/N] " ok
if [[ "$ok" == "y" || "$ok" == "Y" ]]; then
  git push --force
else
  echo "Canceling push. Inspect repo-mirror.git locally." 
fi
