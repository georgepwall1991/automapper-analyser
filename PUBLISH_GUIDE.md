# 🚀 Auto-Publish Guide

## Secure NuGet Publishing with Auto-Increment Versioning

This guide explains how to securely use the auto-publish scripts without exposing your NuGet API key.

## 🔐 Security First

**⚠️ NEVER commit API keys to version control!**

The scripts now use environment variables to keep your API key secure.

## 📋 Setup Instructions

### 1. Set Your API Key as Environment Variable

#### Option A: PowerShell (Recommended for Windows/macOS)
```powershell
# Set for current session
$env:NUGET_API_KEY = "your-nuget-api-key-here"

# Set permanently (Windows)
[Environment]::SetEnvironmentVariable("NUGET_API_KEY", "your-nuget-api-key-here", "User")
```

#### Option B: Bash/Zsh (Linux/macOS Terminal)
```bash
# Set for current session
export NUGET_API_KEY="your-nuget-api-key-here"

# Set permanently (add to ~/.bashrc or ~/.zshrc)
echo 'export NUGET_API_KEY="your-nuget-api-key-here"' >> ~/.zshrc
source ~/.zshrc
```

### 2. Run Auto-Publish

#### PowerShell Version (Recommended)
```powershell
./Publish.ps1
```

#### Bash Version
```bash
./publish.sh
```

#### One-time Usage (Without Setting Environment Variable)
```powershell
./Publish.ps1 -ApiKey "your-nuget-api-key-here"
```

```bash
NUGET_API_KEY="your-nuget-api-key-here" ./publish.sh
```

## 🔄 Auto-Versioning System

**Version Format:** `1.5.MMDD.HHMM`

- `1.5` = Major.Minor (1.5 indicates auto-versioning)
- `MMDD` = Month and Day (e.g., 0606 for June 6th)
- `HHMM` = Hour and Minute (e.g., 0941 for 09:41)

**Examples:**
- `1.5.606.941` = June 6th, 09:41
- `1.5.1225.1530` = December 25th, 15:30

## 📦 What the Script Does

1. **Generates unique version** based on current date/time
2. **Cleans previous builds** to ensure fresh compilation
3. **Builds NuGet package** with auto-generated version
4. **Publishes to NuGet.org** using your API key
5. **Cleans up old packages** (keeps only 3 most recent)
6. **Reports success** with package URL

## 🛡️ Security Features

- ✅ **No hardcoded API keys** in source code
- ✅ **Environment variable usage** for secure key storage
- ✅ **Input validation** to prevent empty API keys
- ✅ **Clear error messages** if API key is missing
- ✅ **Safe for version control** - no secrets committed

## 🚨 Troubleshooting

### "API key not provided" Error
```
❌ Error: NuGet API key not provided!
```
**Solution:** Set the `NUGET_API_KEY` environment variable as shown above.

### GitHub Push Protection Error
```
remote: - Push cannot contain secrets
```
**Solution:** This guide prevents this error by using environment variables instead of hardcoded keys.

### Package Already Exists Error
```
error: Response status code does not indicate success: 409 (Conflict)
```
**Solution:** The auto-versioning system generates unique versions every minute, preventing conflicts.

## 🔗 Useful Links

- [NuGet API Keys](https://www.nuget.org/account/apikeys)
- [AutoMapper Analyzer Package](https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/)
- [Environment Variables Guide](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_environment_variables)

## 📈 Version History

Each successful publish creates a new version. You can view all versions at:
https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/

The auto-versioning ensures every build gets a unique version number without manual intervention! 