# Build and test

Windows x64 and the .NET 9 SDK are required. Run commands from the repository root.

```powershell
.\scripts\build.ps1 -Language en
.\scripts\build.ps1 -Language zh-CN
.\scripts\test.ps1 -ApplicationDirectory .\artifacts\app-en
.\scripts\test.ps1 -ApplicationDirectory .\artifacts\app-zh-CN
```

To also run isolated native tests against your own supported game installation:

```powershell
.\scripts\test.ps1 -ApplicationDirectory .\artifacts\app-en -GamePath 'FULL_PATH_TO_GAME\th06nc.exe'
```

These tests use private memory images and a test host, not live gameplay. The game is never uploaded to CI.

`src/TH06NCTrainer.csproj` compiles application code from `src/` and links regression code from `tests/`. Tests intentionally remain in the same assembly so the existing self-test commands and internal-access behavior are unchanged. This is a directory reorganization, not a gameplay or architecture change. The background keeps its original embedded-resource name.

## Packaging

```powershell
.\scripts\build-release.ps1 -OutputRoot .\artifacts\packages-new
```

This creates separate language packages and checksums locally. Use a new output directory; existing packages are not overwritten. It does not publish, delete or change GitHub releases.

The active GitHub workflow only builds and tests; it has read-only repository permissions. Earlier version-specific release workflows are retained under [workflows](workflows/) as historical records, not active automation. Version-specific notes in [history](history/) may mention the old layout and are preserved as historical documentation.

## Directory guide

- `src/`: application, localization, process-memory code and project file.
- `tests/`: UI, localization and isolated gameplay regressions.
- `assets/`: red-moon background, P icon and provenance notes.
- `docs/en/`, `docs/zh-CN/`: separate user guides and release notes.
- `docs/images/`: interface previews, not desktop-compositor verification.
- `scripts/`: build, test and local packaging entry points.

The user's edited README content has been retained in the language-specific guides; the root README is now a short entry page. Existing releases and tags are unchanged.
