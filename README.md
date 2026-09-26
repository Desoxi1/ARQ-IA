# Rvit-mcp-dynamo

PoC local para evaluar ChatGPT/GPT → MCP/bridge → Dynamo + PythonNet3 → Revit 2027.

## Estado

El POC tiene configuración MCP local para Codex y un generador que guarda scripts `.py`, grafos `.dyn` manuales PythonNet3 y manifiestos con hashes. rvt-mcp v0.6.2 está instalado para Revit 2027 en el perfil del usuario. La descarga oficial pasó la verificación SHA-256 publicada.

La conexión directa Revit 2027 ↔ rvt-mcp respondió consultas de solo lectura en una prueba histórica; la repetición actual agota 60 s. El perfil de Codex habilita una lista acotada de operaciones tipadas de consulta, creación y edición, con aprobación por llamada; no expone borrado ni ToolBaker. El bridge propio tiene un ejecutor de rutinas guardadas PythonNet3, separado de la rutina read-only fijada por hash. La ejecución genérica exige aprobación de la llamada y `authorize_possible_model_writes=true` para toda rutina, comprueba hashes y grafo, se limita a RVT locales dentro de `revit/test-models/` y verifica los IDs reportados frente a `DocumentChanged` de Revit. El DLL endurecido ya está instalado por usuario y su SHA-256 coincide con el paquete. Se abrió una sesión nueva con el RVT descartable; falta aceptar **Load Once** en el aviso de Revit para iniciar la prueba viva.

Las pruebas offline ya pasan: 22 pruebas unitarias, integración stdio del generador, transporte MCP/named pipe con bridge falso y compilación del add-in contra RevitAPI/DynamoRevitDS/.NET 10. Estas pruebas no demuestran que `DynamoRevit.ExecuteCommand` ejecute PythonNet3 dentro de Revit. El directorio `revit/test-models/BIM-AI-Dynamo-Test.rvt` contiene una copia local e ignorada por Git de un modelo de muestra incluido con Autodesk Revit 2027.

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

Ejecuta `setup.ps1` para instalar/configurar rvt-mcp. Si solo hace falta actualizar la configuración local del proyecto, usa `setup.ps1 -ConfigureOnly`. `revit/bridge/install.ps1` instala el puente por usuario; si Revit sigue abierto, `-StageWhileRevitOpen` prepara los archivos para una sesión nueva y no afecta las sesiones ya iniciadas. Abre esta carpeta como proyecto confiable en Codex y reinicia Codex para cargar los MCP. La primera prueba viva es solo lectura en el RVT desechable de `revit/test-models/`; confirma que Revit cargó el add-in y que `ping_revit_dynamo_bridge` responde antes de ejecutar una rutina.

Consulta [docs/INSTRUCCIONES_OTRO_PC.md](docs/INSTRUCCIONES_OTRO_PC.md) para replicar el POC en otro PC.
