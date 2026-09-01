# Security audit

Audit date: 2026-09-01

Scope: the Windows tray wrapper, `@waishnav/devspace@1.0.8`, the resolved runtime dependency tree, and the runtime behavior relevant to the npm findings. This is not a security certification of Windows, Cloudflare, Node.js, or user workspace content.

## Release decision

Current status: **PASS WITH KNOWN UPSTREAM RISK**.

The audited DevSpace dependency tree reports five vulnerable package nodes in `npm audit` (`3 moderate`, `2 high`, `0 critical`). The underlying advisories are concentrated in three transitive libraries below `@earendil-works/pi-coding-agent@0.80.10`:

- `brace-expansion@5.0.6`
- `protobufjs@7.6.4`
- `undici@8.5.0`

The tray explicitly sets `DEVSPACE_SUBAGENTS=0` before starting DevSpace. DevSpace dynamically imports `@earendil-works/pi-coding-agent` only when the Pi local-agent provider is created, so the affected Pi path is not part of the tray's normal MCP execution path while subagents remain disabled.

This acceptance is conditional. Enabling DevSpace subagents, exposing the Pi provider, changing any audited dependency version, or introducing a new advisory invalidates this decision and requires a new audit.

## Findings

### brace-expansion 5.0.6 — high

Path: `@waishnav/devspace -> @earendil-works/pi-coding-agent -> minimatch -> brace-expansion`.

The known findings are denial-of-service/resource-exhaustion cases triggered by hostile brace patterns. The patched 5.x line is 5.0.9. The finding is not reachable in the tray's normal runtime while Pi subagents remain disabled.

### protobufjs 7.6.4 — moderate

Path: `@waishnav/devspace -> @earendil-works/pi-coding-agent -> @earendil-works/pi-ai -> @google/genai -> protobufjs`.

The advisory affects parsing of attacker-controlled `.proto` schema text and is fixed in 7.6.5. No tray path was identified that accepts untrusted `.proto` schemas, and the containing Pi path is disabled.

### undici 8.5.0 — high aggregate severity

Path: `@waishnav/devspace -> @earendil-works/pi-coding-agent -> undici`.

The audited 8.x advisories are fixed in 8.9.0. `pi-coding-agent@0.80.10` declares the exact version `8.5.0`, so this project does not silently replace it without upstream compatibility confirmation and Pi regression testing.

## Known advisory allowlist

`audit-runtime.ps1` accepts only these already-reviewed advisory URLs:

- https://github.com/advisories/GHSA-3jxr-9vmj-r5cp
- https://github.com/advisories/GHSA-4cwx-7wf7-3272
- https://github.com/advisories/GHSA-8xcm-r25x-g524
- https://github.com/advisories/GHSA-j3f2-48v5-ccww
- https://github.com/advisories/GHSA-jr45-8vmc-qm54
- https://github.com/advisories/GHSA-m8rv-5g2x-5cg5
- https://github.com/advisories/GHSA-mh99-v99m-4gvg
- https://github.com/advisories/GHSA-rgw5-rvv9-x895
- https://github.com/advisories/GHSA-v3r7-h72x-cjcm

Any new advisory or audited-version drift blocks setup/release and requires a new review.

## Release gate

The gate enforces:

- `@waishnav/devspace == 1.0.8`
- `@earendil-works/pi-coding-agent == 0.80.10`
- `undici == 8.5.0`
- `brace-expansion == 5.0.6`
- `protobufjs == 7.6.4`
- zero critical findings
- no vulnerability package outside the reviewed set
- no advisory outside the nine reviewed advisory URLs
- `DEVSPACE_SUBAGENTS=0` still present in the tray source

The result is deliberately reported as `PASS_WITH_KNOWN_UPSTREAM_RISK`, not a clean audit.

## Preferred remediation

Prefer a future DevSpace release that moves Pi onto patched dependency versions and removes the vulnerable pins. At that point regenerate the runtime dependency tree and rerun build, setup, npm audit, MCP read/write/exec smoke tests, and tunnel startup tests.
