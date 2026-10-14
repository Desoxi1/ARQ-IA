# Configuración de ARQ-IA

- `rvt-mcp-tools.json` es la fuente versionada de los toolsets y herramientas permitidos para instalaciones nuevas.
- `setup.ps1` valida esa lista y genera `.codex/config.toml` con rutas absolutas del PC. Ese archivo generado se ignora en Git.
- La lista es deliberadamente acotada. No habilita `send_code`, ToolBaker, borrado ni ejecución arbitraria.
- La configuración actual se deriva de los toolsets compatibles de `rvt-mcp` 1.1.0; el PC de desarrollo puede conservar la versión previa. Comprueba compatibilidad con `docs/PROTOTYPE_STATUS.md` antes de ampliar el catálogo.
