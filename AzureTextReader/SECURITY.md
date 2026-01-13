# Security and Secrets

This repository uses a pre-push hook and .gitignore to prevent accidental commits of secrets.

If you find any secret committed in the history, perform these steps immediately:

1. Rotate the exposed secret (regenerate API keys, passwords, connection strings).
2. Remove the secret from repository history using one of:
   - BFG Repo-Cleaner (simple)
   - git-filter-repo (recommended)
3. Force-push the cleaned history to the remote and inform collaborators to reclone.

Scripts provided:
- `scripts/find-secrets-in-history.sh` - scans the git history for common patterns.
- `scripts/remove-secret-using-bfg.sh` - helper script showing how to use BFG to remove secrets.

Hook installation:
- Run `./scripts/install-git-hooks.sh` on Unix or `.\scripts\install-git-hooks.ps1` on Windows to install the pre-push hook.

Note: Rewriting git history is disruptive. Coordinate with your team before pushing rewritten history.
