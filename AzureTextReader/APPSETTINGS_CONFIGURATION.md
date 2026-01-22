# appsettings.json Git Configuration Summary

## Current Status

### ? Files That SHOULD Be Ignored (Not Tracked)
These contain environment-specific secrets and should NOT be in git:

1. **appsettings.Development.json** - Development environment config
2. **appsettings.Staging.json** - Staging environment config  
3. **appsettings.Production.json** - Production environment config
4. **appsettings.Local.json** - Local development config
5. **launchSettings.json** - All launch settings with connection strings

### ? Files That SHOULD Be Tracked (In Git)
These are safe to commit and help with application setup:

1. **appsettings.json** - Base configuration (generic values only)
2. **appsettings.json.example** - Example file for reference

## Why This Matters

```
Environment-Specific Files (IGNORE):
??? appsettings.Development.json    ? Contains dev API keys, connection strings
??? appsettings.Staging.json        ? Contains staging secrets
??? appsettings.Production.json     ? Contains production credentials
??? appsettings.Local.json          ? Contains personal local config
??? launchSettings.json             ? Contains environment variables

Base Files (TRACK):
??? appsettings.json                ? Safe defaults, no secrets
??? appsettings.json.example        ? Reference for developers
```

## How to Use

### For Development
1. Keep `appsettings.json` in git with generic/example values
2. Create `appsettings.Development.json` locally (git-ignored) with your real values
3. Create `launchSettings.json` locally (git-ignored) with your environment variables

### For CI/CD & Production
1. Environment variables are injected by deployment pipeline
2. No sensitive files in git repository
3. Each environment has its own secrets management

## Recent Changes
Fixed `.gitignore` to explicitly list environment-specific appsettings files instead of using conflicting wildcard rules.
