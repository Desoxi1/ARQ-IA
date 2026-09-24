# Third-party installer cache

`RvtMcp.Setup-v0.6.2-win-x64.zip` is a local cache, ignored by Git to avoid embedding a 90 MB third-party binary in this repository. `setup.ps1` downloads the pinned release from the official project and checks its SHA-256 before installation.

- Release: <https://github.com/bimwright/rvt-mcp/releases/tag/v0.6.2>
- Asset: `RvtMcp.Setup-v0.6.2-win-x64.zip`
- Expected SHA-256: `0d6239afcab47c729c30b8c5b763214c20c38c4b72de7e30c5a7191b4131b1ae`

The installer is executed only after the hash matches. See the upstream release notes and licensing before redistributing its binary.
