#!/usr/bin/env bash
HOOK_SOURCE="githooks/pre-push"
HOOK_DEST=".git/hooks/pre-push"
if [ -f "$HOOK_SOURCE" ]; then
  mkdir -p ".git/hooks"
  cp "$HOOK_SOURCE" "$HOOK_DEST"
  chmod +x "$HOOK_DEST"
  echo "Installed git hook to $HOOK_DEST"
else
  echo "Hook source $HOOK_SOURCE not found"
  exit 1
fi
