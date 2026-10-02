# Rvit-mcp-dynamo

PoC local para evaluar ChatGPT/GPT → MCP/bridge → Dynamo + PythonNet3 → Revit 2027.

## Estado

El paquete incluye un chat para Revit y una extensión lateral para Dynamo, conectados a Codex local mediante MCP. El asistente acepta texto, imágenes y documentos; puede consultar vista/selección, preparar scripts PythonNet3 y grafos `.dyn`, guardar rutinas reutilizables y solicitar aprobación antes de cada operación sensible.

La configuración para otro PC ahora tiene una sola entrada: `install.ps1`. En este PC se ejecutó correctamente y dejó bridge, chat de Revit y panel Dynamo instalados para la próxima sesión. Ambos assemblies compilaron en Release con 0 advertencias y 0 errores. La suite offline pasa 54 pruebas; `doctor.ps1` informa 14 PASS, 4 WAIT y 0 FAIL. Los WAIT son esperables antes de reiniciar Revit y Dynamo: el proceso actual conserva los assemblies anteriores, por eso la UI nueva todavía no está validada en pantalla.

La compilación Dynamo actual es SHA-256 `874713ea360d5f4acd0ef30dfec9725f98407269a07483726adf4866df89e0ab`. La compilación del panel nativo de Revit es `60178b1eae3ef918c88632dc189232e7c4ade76b256f48a86478cc0ccbfe3897`; el bridge conserva `adf38e853f958fe37d866b8b5c21da2c071f4afabf0f71e503a52ead903f053c`. La respuesta desde la UI nueva y la ejecución viva de rutinas siguen pendientes. El instalador no abrió ni editó modelos.

La preparación del entorno está documentada en [docs/ENV_AUDIT.md](docs/ENV_AUDIT.md), la decisión de arquitectura en [docs/ARCHITECTURE_DECISION.md](docs/ARCHITECTURE_DECISION.md) y la ruta de prueba en [docs/PROTOTYPE_STATUS.md](docs/PROTOTYPE_STATUS.md).

La capacidad objetivo para interpretar lenguaje natural/técnico, planificar cambios, usar herramientas Revit tipadas y evolucionar hacia rutinas verificadas está en [docs/CAPACIDADES_OBJETIVO.md](docs/CAPACIDADES_OBJETIVO.md). El glosario de términos español ↔ Revit API está en [docs/GLOSARIO_BIM_REVIT_ES_EN.md](docs/GLOSARIO_BIM_REVIT_ES_EN.md); los ejemplos para medir interpretación y aclaraciones están en [tests/LENGUAJE_Y_AMBIGUEDADES.md](tests/LENGUAJE_Y_AMBIGUEDADES.md).

## Estructura

- docs/: auditoría, decisión y estado del prototipo.
- revit/: notas del host Revit y del add-in.
- dynamo/: plantilla PythonNet3 manual para Dynamo 4.0.2.
- dynamo/extension/: panel lateral **Asistente BIM** integrado en Dynamo; usa Codex app-server y los MCP del proyecto.
- revit/assistant/: add-in nativo de Revit 2027 con pestaña BIM-AI y panel acoplable; comparte el chat y adjuntos con Dynamo.
- routines/: destino de rutinas revisadas, versionadas y reutilizables.
- mcp/: MCP local para crear/listar rutinas y ejecutar las guardadas desde el bridge.
- revit/bridge/: add-in fuente, DLL precompilado para la prueba y script de instalación local.
- vendor/: referencia y caché local ignorada del instalador oficial rvt-mcp v0.6.2.
- logs/: ubicación local para logs minimizados.
- tests/: plan de validación y resultados del protocolo MCP local.

## Límites del experimento

- Usar un modelo de prueba local y descartable cuando empiece la fase siguiente.
- Las rutinas genéricas solo se ejecutan en un RVT local guardado, idle y no workshared dentro de `revit/test-models/`. Dynamo puede dejar ese RVT marcado como modificado en memoria al iniciarse; cada ejecución verifica su propio delta `DocumentChanged`. Toda ejecución genérica requiere aprobación de herramienta y `authorize_possible_model_writes=true`, incluso si el manifiesto dice solo lectura, porque PythonNet3 no es un sandbox.
- PythonNet3 corre dentro del proceso Revit con los permisos del usuario. La comprobación de carpeta limita qué documento puede escribir, pero no crea un sandbox de sistema operativo para el script. Revisa cada `.py` antes de ejecutar y no guardes secretos en los resultados.
- Para herramientas Revit tipadas, Codex debe describir objetivo, elementos y valores afectados y pedir aprobación para cada llamada concreta.
- Las herramientas de escritura tipadas en este perfil pueden modificar el documento al aprobarse; no hay garantía general de previsualización o rollback. Haz primero operaciones unitarias en un modelo descartable.
- El generador guarda la fuente, el `.dyn` con un wrapper JSON acotado y el manifiesto. Una rutina se promueve a `validated` solo después de una ejecución correcta del runner en el modelo de prueba.
- No guardar archivos RVT/RFA, credenciales, tokens, rutas de proyectos productivos ni datos personales en este repositorio.
- Una rutina guardada debe poder volver a ejecutarse sin llamar a GPT; GPT participa al crear o cambiar la rutina.

## Preparar el equipo

### Instalación rápida en un PC nuevo

Clona esta carpeta y, desde PowerShell en su raíz, ejecuta una sola vez:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

El instalador detecta Revit/Dynamo 2027 y Codex CLI, descarga rvt-mcp si hace falta, configura los MCP para esta copia del repositorio, enlaza sus rutinas locales e instala el bridge y los paneles **BIM-AI** de Revit y **Asistente BIM** de Dynamo. Windows solicita elevación para escribir la extensión en la carpeta de Dynamo. Si Revit ya está abierto y rvt-mcp está instalado, prepara las nuevas versiones para el próximo reinicio; para una primera instalación con Revit abierto, guarda y cierra antes de ejecutar el comando.

Después, abre esta carpeta en Codex y confirma que confías en el proyecto para cargar sus MCP locales. Inicia sesión en Codex si aún no lo has hecho y reinicia Revit; abre Dynamo para ver el panel. No se requiere una API key ni el SDK de .NET en el PC arquitecto. `install.ps1 -CheckOnly` solo revisa prerequisitos y no instala archivos.

Los instaladores individuales siguen disponibles para reparación: `setup.ps1`, `revit/bridge/install.ps1`, `revit/assistant/install.ps1` y `dynamo/extension/install.ps1`. Para la prueba viva, usa un RVT descartable, abre Dynamo y confirma `dynamoReadyForActiveDocument=true` antes de ejecutar la rutina fija de solo lectura.

Para revisar la instalación sin modificarla, ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\doctor.ps1`. El diagnóstico valida Revit/Dynamo, los MCP, el add-in y sus hashes, las rutas locales, el RVT de prueba y el ping del bridge. `WAIT` indica que falta reiniciar Revit o abrir Dynamo UI sobre el modelo de prueba; `FAIL` señala una instalación o configuración que requiere corregirse.

Consulta [docs/INSTRUCCIONES_OTRO_PC.md](docs/INSTRUCCIONES_OTRO_PC.md) para la prueba segura después de la instalación rápida. El DLL precompilado evita instalar .NET SDK en el equipo arquitecto.

Para crear un ZIP portable del commit actual, primero guarda todos los cambios en Git y ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1`. El empaquetador excluye los modelos y la configuración local, y valida secretos/rutas antes de escribir el archivo en `artifacts/`.
