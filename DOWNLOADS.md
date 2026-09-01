# Dependency downloads

The project does not commit third-party runtimes or `node_modules`. The lightweight release downloads pinned dependencies on first setup; the offline release downloads the same dependencies at package time and bundles them.

| Dependency | Pinned version | Official source | Verification |
|---|---:|---|---|
| Node.js Windows x64 | 22.22.3 | https://nodejs.org/dist/v22.22.3/node-v22.22.3-win-x64.zip | SHA-256 `6c8d54f635feff4df76c2ca80f45332eb2ff57d25226edce36592e51a177ee33` |
| Node.js checksums | 22.22.3 | https://nodejs.org/dist/v22.22.3/SHASUMS256.txt | Upstream checksum manifest |
| cloudflared Windows amd64 | 2026.8.2 | https://github.com/cloudflare/cloudflared/releases/download/2026.8.2/cloudflared-windows-amd64.exe | SHA-256 `c29eee2b121f5436a642eed69fd9767da7e7b8c510fa50aaa130337f931357b5` |
| cloudflared release page | 2026.8.2 | https://github.com/cloudflare/cloudflared/releases/tag/2026.8.2 | Upstream release/checksum page |
| @waishnav/devspace | 1.0.8 | https://www.npmjs.com/package/@waishnav/devspace/v/1.0.8 | Installed by npm, then checked by `audit-runtime.ps1` |
| @waishnav/devspace tarball | 1.0.8 | https://registry.npmjs.org/@waishnav/devspace/-/devspace-1.0.8.tgz | npm registry package |
| Git for Windows | current supported | https://git-scm.com/download/win | Optional for DevSpace modes that use Git Bash |

Do not replace pinned executable URLs with `latest` URLs in a release script. Updating Node.js, cloudflared, DevSpace, or any audited transitive dependency requires updating the pinned version/checksum and rerunning the security/runtime checks.
