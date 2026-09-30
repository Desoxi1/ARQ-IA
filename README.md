# Rvit-mcp-dynamo

PoC local para evaluar ChatGPT/GPT → MCP/bridge → Dynamo + PythonNet3 → Revit 2027.

## Estado

El POC tiene configuración MCP local para Codex y un generador que guarda scripts `.py`, grafos `.dyn` manuales PythonNet3 y manifiestos con hashes. Codex CLI muestra habilitados los tres MCP del proyecto (`rvt_mcp`, `bim_ai_routines`, `bim_ai_dynamo_runner`), y una sesión CLI efímera ya llamó `bim_ai_routines.list_saved_routines` y recibió las tres rutinas. La app Codex aún debe comprobarse por separado; esa llamada no ejecutó Dynamo ni accedió a Revit.

La consulta directa Revit 2027 ↔ rvt-mcp pasó lecturas de solo lectura de vista y selección; descubrió 16 herramientas. El perfil habilita una lista acotada de operaciones tipadas de consulta, creación y edición, con aprobación por llamada; no expone borrado ni ToolBaker. El bridge propio ejecuta rutinas PythonNet3 guardadas, separado de la rutina de lectura fijada por hash. Toda ejecución genérica exige aprobación de la llamada y `authorize_possible_model_writes=true`, valida hashes y grafo, se limita a RVT locales dentro de `revit/test-models/` y contrasta los IDs reportados con `DocumentChanged` de Revit. El DLL endurecido (SHA-256 `9205c035…`) está instalado por usuario. En el intento vivo del 28-09, el named pipe aceptó la llamada, pero el ping terminó con `revit_dispatch_timeout_before_execution`; la captura mostraba cinco referencias sin resolver. El 30-09, la captura volvió a mostrar ese aviso al abrir el RVT descartable; el diagnóstico aún no detecta ese modelo ni el pipe. No se ha ejecutado Dynamo/PythonNet3 en vivo ni se ha usado el modelo productivo para estas pruebas.

Las pruebas offline pasan: 27 pruebas unitarias, MCP stdio del generador y runner, loop Builder→Runner con bridge falso y compilación del add-in contra RevitAPI/DynamoRevitDS/.NET 10. La consulta rvt-mcp real de vista/selección también pasó. Ninguno de esos resultados demuestra que `DynamoRevit.ExecuteCommand` ejecute PythonNet3 dentro de Revit; esa prueba sigue pendiente de despejar el cuadro modal en el RVT descartable. El directorio `revit/test-models/` contiene copias locales e ignoradas por Git de un modelo de muestra de Revit 2027.

La preparación del entorno está documentada en [docs/ENV_AUDIT.md](docs/ENV_AUDIT.md), la decisión de arquitectura en [docs/ARCHITECTURE_DECISION.md](docs/ARCHITECTURE_DECISION.md) y la ruta de prueba en [docs/PROTOTYPE_STATUS.md](docs/PROTOTYPE_STATUS.md).

La capacidad objetivo para interpretar lenguaje natural/técnico, planificar cambios, usar herramientas Revit tipadas y evolucionar hacia rutinas verificadas está en [docs/CAPACIDADES_OBJETIVO.md](docs/CAPACIDADES_OBJETIVO.md). El glosario de términos español ↔ Revit API está en [docs/GLOSARIO_BIM_REVIT_ES_EN.md](docs/GLOSARIO_BIM_REVIT_ES_EN.md); los ejemplos para medir interpretación y aclaraciones están en [tests/LENGUAJE_Y_AMBIGUEDADES.md](tests/LENGUAJE_Y_AMBIGUEDADES.md).

## Estructura

- docs/: auditoría, decisión y estado del prototipo.
- revit/: notas del host Revit y del add-in.
- dynamo/: plantilla PythonNet3 manual para Dynamo 4.0.2.
- routines/: destino de rutinas revisadas, versionadas y reutilizables.
- mcp/: MCP local para crear/listar rutinas y ejecutar las guardadas desde el bridge.
- revit/bridge/: add-in fuente, DLL precompilado para la prueba y script de instalación local.
- vendor/: referencia y caché local ignorada del instalador oficial rvt-mcp v0.6.2.
- logs/: ubicación local para logs minimizados.
- tests/: plan de validación y resultados del protocolo MCP local.

## Límites del experimento

- Usar un modelo de prueba local y descartable cuando empiece la fase siguiente.
- Las rutinas genéricas solo se ejecutan en un RVT guardado, idle, local y no workshared dentro de `revit/test-models/`. Toda ejecución requiere aprobación de herramienta y `authorize_possible_model_writes=true`, incluso si el manifiesto dice solo lectura, porque PythonNet3 no es un sandbox.
- PythonNet3 corre dentro del proceso Revit con los permisos del usuario. La comprobación de carpeta limita qué documento puede escribir, pero no crea un sandbox de sistema operativo para el script. Revisa cada `.py` antes de ejecutar y no guardes secretos en los resultados.
- Para herramientas Revit tipadas, Codex debe describir objetivo, elementos y valores afectados y pedir aprobación para cada llamada concreta.
- Las herramientas de escritura tipadas en este perfil pueden modificar el documento al aprobarse; no hay garantía general de previsualización o rollback. Haz primero operaciones unitarias en un modelo descartable.
- El generador guarda la fuente, el `.dyn` con un wrapper JSON acotado y el manifiesto. Una rutina se promueve a `validated` solo después de una ejecución correcta del runner en el modelo de prueba.
- No guardar archivos RVT/RFA, credenciales, tokens, rutas de proyectos productivos ni datos personales en este repositorio.
- Una rutina guardada debe poder volver a ejecutarse sin llamar a GPT; GPT participa al crear o cambiar la rutina.

## Preparar el equipo

Ejecuta `setup.ps1` para instalar/configurar rvt-mcp. Si solo hace falta actualizar la configuración local del proyecto, usa `setup.ps1 -ConfigureOnly`. `revit/bridge/install.ps1` instala el puente por usuario; si Revit sigue abierto, `-StageWhileRevitOpen` prepara los archivos para una sesión nueva. Abre esta carpeta como proyecto confiable en Codex y reinicia Codex para cargar los MCP. La primera prueba viva es solo lectura en el RVT desechable de `revit/test-models/`; confirma que `ping_revit_dynamo_bridge` responde y que la lectura de Dynamo termina antes de intentar una escritura.

Para revisar la instalación sin modificarla, ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\doctor.ps1`. El diagnóstico valida Revit/Dynamo, los MCP, el add-in y sus hashes, las rutas locales y si el RVT de prueba y el pipe están disponibles. `WAIT` indica que falta abrir la sesión de prueba; `FAIL` señala una instalación o configuración que requiere corregirse.

Consulta [docs/INSTRUCCIONES_OTRO_PC.md](docs/INSTRUCCIONES_OTRO_PC.md) para replicar el POC en otro PC.

Para crear un ZIP portable del commit actual, primero guarda todos los cambios en Git y ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1`. El empaquetador excluye los modelos y la configuración local, y valida secretos/rutas antes de escribir el archivo en `artifacts/`.
