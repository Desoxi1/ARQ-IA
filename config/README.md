# Configuración de ARQ-IA

## Fuente versionada

[`rvt-mcp-tools.json`](rvt-mcp-tools.json) define el catálogo que el instalador registra en Codex. Conserva identificadores de herramientas en inglés porque son nombres de la API; sus descripciones, clasificación y restricciones se explican en español.

- `toolsets`: dominios del servidor `rvt-mcp` v1.1.0 seleccionados para Revit.
- `enabled_tools`: allowlist de 105 herramientas que Codex puede ver desde ARQ-IA (58 de lectura y 47 de escritura tipada).
- `clasificacion_herramientas_es`: separa consultas de solo lectura y operaciones que modifican el modelo; cada escritura habilitada debe quedar en una sola clase.
- `toolsets_bloqueados` / `always_disabled`: dominios y funciones que el perfil no expone.
- `protecciones`: condiciones que `setup.ps1` exige antes de escribir la configuración.
- `approval_mode: prompt`: Codex solicita aprobación para las herramientas habilitadas que modifican el modelo en acceso controlado.

El catálogo se contrastó con la superficie y el código publicado de [rvt-mcp v1.1.0](https://github.com/bimwright/rvt-mcp/tree/v1.1.0). La selección actual cubre creación/edición de elementos arquitectónicos, estructurales y MEP, parámetros, materiales, gráficos, vistas, láminas y tablas. No son las 233 herramientas del servidor ni toda la API de Revit. Cada herramienta conserva su esquema y puede fallar por precondiciones del modelo; no se declara validada en vivo solo por estar habilitada.

## Capas y ubicación

`setup.ps1` comprueba versión, nombres, clasificaciones, duplicados y bloqueos; después genera `.codex/config.toml` con rutas absolutas de este clon. La allowlist limita las herramientas visibles en el cliente y los argumentos del servidor desactivan `send_code` y ToolBaker. Borrado de elementos, C# arbitrario, exportación de archivos y flujos compuestos permanecen excluidos.

Cuando una operación de geometría o edición no tenga herramienta tipada (por ejemplo dividir un muro), ARQ-IA debe consultar el modelo, construir una rutina Dynamo acotada y usar el runner del RVT activo si está disponible. El código se revisa antes de ejecutarse; Revit mantiene su confirmación Sí/No para la ejecución PythonNet3 sobre el modelo abierto.

`.codex/config.toml` se crea en cada PC y se ignora en Git. No lo copies a otro equipo: vuelve a generar la configuración desde el instalador. Habilitar herramientas amplía lo que el modelo puede solicitar; no prueba su funcionamiento en cada versión, documento o estado de Revit. Consulta [estado y evidencia](../docs/PROTOTYPE_STATUS.md) y valida cada flujo de escritura en una copia controlada antes de declararlo confiable.
