# Configuración de ARQ-IA

## Fuente versionada

[`rvt-mcp-tools.json`](rvt-mcp-tools.json) define el catálogo que el instalador registra en Codex. Conserva identificadores de herramientas en inglés porque son nombres de la API; sus descripciones, clasificación y restricciones se explican en español.

- `toolsets`: dominios del servidor `rvt-mcp` v1.1.0 seleccionados para Revit.
- `enabled_tools`: allowlist de herramientas que Codex puede ver desde ARQ-IA.
- `clasificacion_herramientas_es`: separa consultas de solo lectura y operaciones que modifican el modelo.
- `toolsets_bloqueados` / `always_disabled`: dominios y funciones que el perfil no expone.
- `protecciones`: condiciones que `setup.ps1` exige antes de escribir la configuración.
- `approval_mode: prompt`: Codex solicita aprobación para las herramientas habilitadas que modifican el modelo en acceso controlado.

El catálogo se contrastó con la superficie publicada de [rvt-mcp v1.1.0](https://github.com/bimwright/rvt-mcp/tree/v1.1.0). La versión está fijada para que una actualización del servidor no cambie silenciosamente los nombres disponibles. Antes de ampliar el catálogo, revisa la [documentación de esa versión](https://github.com/bimwright/rvt-mcp/blob/v1.1.0/README.md) y añade una prueba para cada nueva capacidad.

## Capas y ubicación

`setup.ps1` comprueba versión, nombres, clasificaciones, duplicados y bloqueos; después genera `.codex/config.toml` con rutas absolutas de este clon. La allowlist limita las herramientas visibles en el cliente y los argumentos del servidor desactivan `send_code` y ToolBaker. No habilites `meta`, `delete`, `export`, `workflows` ni `toolbaker` sin revisar antes la superficie y actualizar pruebas.

`.codex/config.toml` se crea en cada PC y se ignora en Git. No lo copies a otro equipo: vuelve a generar la configuración desde el instalador. Cambiar los toolsets no prueba que las operaciones funcionen; consulta [estado y evidencia](../docs/PROTOTYPE_STATUS.md) y ejecuta validaciones controladas antes de habilitar nuevas escrituras.
