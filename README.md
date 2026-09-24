# Rvit-mcp-dynamo

PoC local para evaluar ChatGPT/GPT → MCP/bridge → Dynamo + PythonNet3 → Revit 2027.

## Estado

El POC ya tiene la configuración MCP local para Codex y un generador que guarda scripts `.py` y grafos `.dyn` de Dynamo con engine PythonNet3. En este PC se instaló rvt-mcp v0.6.2 para Revit 2027 en el perfil del usuario. La descarga oficial pasó la verificación SHA-256 publicada.

La conexión directa Revit 2027 ↔ rvt-mcp ya respondió consultas de solo lectura en este PC (vista activa y un muro seleccionado). El perfil preparado para Codex habilita un conjunto acotado de herramientas tipadas para consultar, crear elementos y editar parámetros/tipos; Codex pide aprobación para cada llamada. No se exponen borrado ni ejecución arbitraria C#/Python. El puente propio compila y empaqueta un add-in que ofrece solo una rutina de lectura fijada por hash; el smoke vivo falló porque el add-in aún no está instalado/cargado. No se declara probado el loop de ejecución Dynamo. Este perfil aún debe cargarse y probarse desde Codex con esta carpeta abierta como proyecto confiable. El generador guarda nuevas rutinas `.py` y `.dyn` dentro de `routines/`; **el usuario todavía debe revisarlas y ejecutarlas manualmente en Dynamo**.

La preparación del entorno está documentada en [docs/ENV_AUDIT.md](docs/ENV_AUDIT.md), la decisión de arquitectura en [docs/ARCHITECTURE_DECISION.md](docs/ARCHITECTURE_DECISION.md) y la ruta de prueba en [docs/PROTOTYPE_STATUS.md](docs/PROTOTYPE_STATUS.md).

La capacidad objetivo para interpretar lenguaje natural/técnico, planificar cambios, usar herramientas Revit tipadas y evolucionar hacia rutinas verificadas está en [docs/CAPACIDADES_OBJETIVO.md](docs/CAPACIDADES_OBJETIVO.md). El glosario de términos español ↔ Revit API está en [docs/GLOSARIO_BIM_REVIT_ES_EN.md](docs/GLOSARIO_BIM_REVIT_ES_EN.md); los ejemplos para medir interpretación y aclaraciones están en [tests/LENGUAJE_Y_AMBIGUEDADES.md](tests/LENGUAJE_Y_AMBIGUEDADES.md).

## Estructura

- docs/: auditoría, decisión y estado del prototipo.
- revit/: notas del host Revit y del add-in.
- dynamo/: plantilla PythonNet3 manual para Dynamo 4.0.2.
- routines/: destino de rutinas revisadas, versionadas y reutilizables.
- mcp/: MCP local para crear y enumerar rutinas Dynamo.
- revit/bridge/: add-in fuente, DLL precompilado para la prueba y script de instalación local.
- vendor/: referencia y caché local ignorada del instalador oficial rvt-mcp v0.6.2.
- logs/: ubicación local para logs minimizados.
- tests/: plan de validación y resultados del protocolo MCP local.

## Límites del experimento

- Usar un modelo de prueba local y descartable cuando empiece la fase siguiente.
- Empieza con consultas de lectura en un modelo desechable; antes de cualquier cambio, Codex debe explicar el objetivo, los elementos y valores afectados y pedir aprobación para la llamada.
- Las herramientas de escritura tipadas en este perfil pueden modificar el documento al aprobarse; no hay garantía general de previsualización o rollback. Haz primero operaciones unitarias en un modelo descartable.
- Guardar una rutina solo después de revisar su código y resultado en un modelo de prueba.
- No guardar archivos RVT/RFA, credenciales, tokens, rutas de proyectos productivos ni datos personales en este repositorio.
- Una rutina guardada debe poder volver a ejecutarse sin llamar a GPT; GPT participa al crear o cambiar la rutina.

## Preparar el equipo

Ejecuta `setup.ps1` desde esta carpeta con Revit cerrado. Si falta el instalador, el script descarga rvt-mcp v0.6.2 desde su release oficial, verifica el SHA-256 y recién entonces lo instala para Revit 2027. Crea una configuración MCP de proyecto en `.codex/config.toml`; no modifica la configuración global de Codex. Si rvt-mcp ya está instalado, `setup.ps1 -ConfigureOnly` actualiza solo esa configuración y puede ejecutarse con Revit abierto. Luego ejecuta `revit/bridge/install.ps1` con Revit cerrado para instalar el add-in puente. Abre esta carpeta como proyecto confiable en Codex y reinicia Codex para cargar los MCP. La prueba viva del runner requiere Revit con un RVT descartable.

Consulta [docs/INSTRUCCIONES_OTRO_PC.md](docs/INSTRUCCIONES_OTRO_PC.md) para replicar el POC en otro PC.
