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
2. Complete the settings dialog. A fresh install has no default Tunnel mode.
3. Select an existing workspace root, `minimal` / `full` / `codex` tool mode, and a valid local port.
4. Explicitly select `Quick`, `Named`, or `Service` and satisfy every requirement for that mode.
5. Only after the full preflight passes will the tray start DevSpace and Cloudflare connectivity.
6. Copy the displayed MCP URL into ChatGPT.
7. Copy the Owner password when the MCP authorization page requests it.

### Strict startup gate

Startup is fail-closed. The tray does not partially start DevSpace and then hope the remaining Cloudflare configuration works. If any global or mode-specific prerequisite is missing, the status window reports `启动已阻止` and no DevSpace/tunnel startup is attempted.

Global prerequisites for every mode:

- workspace root exists;
- tool mode and local port are valid;
- the DevSpace runtime is complete: Node.js and DevSpace CLI must both be available;
- a Tunnel mode was explicitly selected.

Mode-specific prerequisites:

| Mode | Required before startup | Notes |
| --- | --- | --- |
| `Quick` | `cloudflared.exe` plus all global prerequisites | Temporary test mode only. It deliberately has no user-owned Tunnel UUID or fixed hostname; Cloudflare generates a `trycloudflare.com` URL that can change after restart. |
| `Named` | fixed hostname, Tunnel UUID/name, existing credentials JSON, existing cloudflared YAML config, `cloudflared.exe`, plus all global prerequisites | Use this when the tray owns the fixed named tunnel process. Missing any field/file blocks saving or startup. |
| `Service` | fixed hostname, an installed/readable `cloudflared` Windows Service, and that service must be `Running`, plus all global prerequisites | The Windows Service owns its Tunnel UUID, credentials, and ingress/config. If the service is missing or stopped, DevSpace is not started. Starting the service from the tray re-runs the full startup gate. |

If you need a stable Cloudflare domain, use `Named` or `Service`; do not use `Quick`.

For a typical Named Tunnel, Cloudflare must provide a Tunnel UUID/name and a DNS hostname routed to that Tunnel. Enter the hostname without `https://`, select the credentials JSON file, and select a cloudflared YAML config whose ingress target points to the local DevSpace port, for example `http://127.0.0.1:7676`.

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

The Git repository pins `@waishnav/devspace` to the exact version `1.0.8`, but does not commit Node.js, cloudflared, `node_modules`, an extracted runtime, or a large transitive lockfile. `setup-runtime.ps1` installs that exact DevSpace version into the local runtime, generates a local lockfile, and immediately runs `audit-runtime.ps1`.

This keeps the repository small while retaining a fail-closed security gate: audited core dependency version drift, new advisories, or any critical finding blocks setup. Updating DevSpace requires updating the pinned version and rerunning the runtime/security checks.

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
