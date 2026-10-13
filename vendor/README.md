# Third-party installer cache

`RvtMcp.Setup-v1.1.0-win-x64.zip` is an optional local cache ignored by Git. A fresh installation downloads the pinned official release and verifies SHA-256 before running its installer. An existing compatible v0.6.2 installation is reused without an automatic upgrade.

- Release: <https://github.com/bimwright/rvt-mcp/releases/tag/v1.1.0>
- Asset: `RvtMcp.Setup-v1.1.0-win-x64.zip`
- Expected SHA-256: `0b3f93df2f993b0cb537426ecbb33330497aadd04360ac68e575663160e4c0bb`

The installer is executed only after the hash matches. See the upstream release notes and licensing before redistributing its binary.
