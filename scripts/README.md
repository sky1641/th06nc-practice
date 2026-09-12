# Build tools

Run these scripts from the repository root with the .NET 9 SDK installed on Windows.

| Script | Purpose |
| --- | --- |
| `build.ps1` | Build one language edition; choose `-Language en` or `-Language zh-CN`. |
| `test.ps1` | Run UI tests and, with an optional game path, isolated native regressions. |
| `build-release.ps1` | Create separate language packages and download checksums locally. |

These scripts do not publish GitHub releases. See [build instructions](../docs/development/README.md) for commands. To use the helper without building it, download a [release package](https://github.com/sky1641/th06nc-practice/releases/latest).
