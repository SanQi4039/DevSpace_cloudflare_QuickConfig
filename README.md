# DevSpace Quick Tunnel Tray

Windows x64 tray wrapper for running DevSpace behind Cloudflare Tunnel and exposing a local coding workspace to ChatGPT through MCP.

This project is built on top of [`@waishnav/devspace`](https://www.npmjs.com/package/@waishnav/devspace). DevSpace provides the MCP/web-control layer that lets the ChatGPT web client access and operate a local coding workspace. This project does not replace DevSpace; it adds a lightweight Windows tray launcher, workspace selection, Cloudflare Tunnel configuration, runtime setup, and packaging so the DevSpace endpoint can be exposed to ChatGPT with much less manual configuration.

In short: **DevSpace provides the ChatGPT-web-to-local-workspace control layer; this repository provides the Windows + Cloudflare quick configuration around it.**

This repository is the clean/public source tree. It intentionally excludes machine-specific configuration, logs, prebuilt runtimes, `node_modules`, Cloudflare credentials, and local tunnel identifiers.

## What it does

- starts and supervises DevSpace;
- supports Cloudflare Quick Tunnel, Named Tunnel, and an existing cloudflared Windows Service;
- restricts DevSpace to a user-selected workspace root;
- supports `minimal`, `full`, and `codex` DevSpace tool modes;
- stores the DevSpace owner token outside this repository in `%USERPROFILE%\.devspace` with restricted Windows ACLs;
- keeps DevSpace subagents disabled in this release because of the audited upstream dependency risk;
- optionally registers Windows login autostart only when the user enables it;
- rotates the tray log at approximately 5 MiB.

## Supported platform

- Windows x64
- .NET Framework 4.x runtime
- PowerShell 5.1+ for setup/release scripts
- internet access for the lightweight dependency setup mode

Git for Windows is recommended for DevSpace modes that use Bash-based tools. The tray discovers Git Bash from the Git for Windows registry entry, standard install locations, or `PATH`.

## Two release modes

### 1. Lightweight release — recommended

The normal GitHub Release stays small and does not include Node.js, `node_modules`, or cloudflared.

After extracting the ZIP, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup-runtime.ps1
```

The script downloads pinned Node.js and cloudflared binaries, installs the pinned DevSpace npm package, then runs the security gate. After `Runtime ready.` appears, start:

```text
DevSpaceQuickTunnelTray.exe
```

### 2. Offline release

Maintainers can generate a self-contained package that already contains Node.js, DevSpace dependencies, cloudflared, and third-party license texts:

```powershell
.\package-release.ps1 -Version 0.1.0 -Offline
```

This is substantially larger and should be used only when the target machine cannot download dependencies during setup.

## First run

1. Start `DevSpaceQuickTunnelTray.exe`.
2. Select the workspace root that DevSpace is allowed to access.
3. Select `minimal`, `full`, or `codex` tool mode.
4. Leave Tunnel mode as `Quick` for the simplest setup, or configure Named/Service mode if you already operate a fixed Cloudflare Tunnel.
5. Copy the displayed MCP URL into ChatGPT.
6. Copy the Owner password when the MCP authorization page requests it.

Quick Tunnel URLs change after restart. Named/Service mode can keep a fixed hostname.

## Build from source

```powershell
.\build.ps1
```

The build uses the .NET Framework 4.x C# compiler already present on supported Windows systems and produces `DevSpaceQuickTunnelTray.exe`.

## Create release packages

Lightweight:

```powershell
.\package-release.ps1 -Version 0.1.0
```

Offline:

```powershell
.\package-release.ps1 -Version 0.1.0 -Offline
```

Both modes include the C# source, setup/release scripts, dependency download references, example settings, third-party notices, and the security audit document.

## Dependency policy

The Git repository commits the small audited `package-lock.json`, but does not commit Node.js, cloudflared, `node_modules`, or any extracted runtime. `setup-runtime.ps1` copies `package.json` plus the lockfile into the local runtime directory and uses `npm ci`, so online and offline installs resolve the same dependency tree that was audited.

This keeps the repository small without sacrificing reproducibility. Updating DevSpace or any locked transitive dependency requires regenerating the lockfile and rerunning `audit-runtime.ps1`.

See [DOWNLOADS.md](DOWNLOADS.md) for direct official dependency URLs and checksums.

## Security status

Current status is **PASS WITH KNOWN UPSTREAM RISK**, not `npm audit = 0`.

The reviewed findings are in the Pi subagent dependency path. This release forces `DEVSPACE_SUBAGENTS=0`, and `audit-runtime.ps1` blocks newly introduced advisories, critical findings, or audited-version drift.

See [SECURITY_AUDIT.md](SECURITY_AUDIT.md) for the full decision and re-audit conditions.

## Files intentionally excluded from GitHub

- `settings.json`
- `DevSpaceQuickTunnelTray.log*`
- `*.exe` build output
- `cloudflared.exe`
- `runtime/`
- `node_modules/`
- `dist/`
- `.release-cache/`
- Cloudflare credentials, tokens, tunnel IDs, or private hostnames

## Dependency sources

Pinned dependency downloads are listed in [DOWNLOADS.md](DOWNLOADS.md). Third-party distribution notes are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Project license

This wrapper is released under the MIT License. See [LICENSE](LICENSE). Third-party software keeps its own upstream licenses.
