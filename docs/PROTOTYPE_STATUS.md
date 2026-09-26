# Estado del prototipo

**Actualizado:** 26 de septiembre de 2026
**Objetivo:** Codex → MCP → rutina guardada → bridge Revit → Dynamo/PythonNet3 → verificación y reutilización.

## Estado actual

- Revit 2027 build 27.0.20.10 y Dynamo 4.0.2.3852 están instalados. rvt-mcp v0.6.2 ya respondió consultas de solo lectura en este PC.
- El proyecto Codex contiene tres MCP: rvt-mcp con una lista curada de herramientas tipadas, generador/listado de rutinas y runner Dynamo. Las llamadas tienen aprobación por herramienta.
- El generador guarda fuente `.py`, un `.dyn` manual con un único nodo PythonNet3, wrapper de resultado JSON y un manifiesto con hashes.
- El runner comprueba los hashes y el formato del grafo tanto en PowerShell como en el add-in. Las rutinas genéricas solo corren con un RVT local guardado y no workshared dentro de `revit/test-models/`; toda ejecución requiere `authorize_possible_model_writes=true`, aunque el manifiesto diga solo lectura, porque PythonNet3 no permite demostrar estáticamente que no habrá escrituras. Para rutinas declaradas como escritoras, el wrapper debe devolver `affected_ids` que coincidan con los elementos reportados por Revit `DocumentChanged`.
- El add-in compila contra RevitAPI, RevitAPIUI, DynamoRevitDS y .NET Runtime 10. Está instalado en el perfil de usuario. El DLL no está firmado. La sesión de Revit con el proyecto productivo sigue abierta e intacta. Se abrió una instancia nueva con el RVT descartable; el diario registra `TaskDialog_Security_Unsigned_File_Loading` y Revit espera que el usuario decida si carga el add-in.
- El paquete `dist/` y la copia instalada por usuario ya coinciden en SHA-256. El test Revit anterior fue cerrado y se lanzó una sesión limpia para probar este paquete.
- Hay una copia local, ignorada por Git, de `Snowdon Towers Sample HVAC.rvt` en `revit/test-models/BIM-AI-Dynamo-Test.rvt`. Se puede descartar; proviene de los Samples instalados con Revit 2027.
- Rutinas incluidas: `read_active_view_selection` (solo lectura y hash fijado), `read_test_model_summary` (solo lectura genérica) y `set_comments_on_selection` (borrador de escritura con protección interna de ruta).
- Endurecimiento del 26-09-2026: la revisión estática encontró que el manifiesto podía declarar una rutina genérica como solo lectura y omitir su confirmación aunque el código intentara escribir. El runner y el add-in ahora exigen autorización de posible escritura en toda ejecución genérica, sin confiar en esa etiqueta.

## Evidencia de pruebas

| Prueba | Resultado | Alcance |
|---|---|---|
| `python -B -m unittest discover -s tests -v` | PASS, 22 pruebas | Fakes de Revit, límites del wrapper JSON, integridad de paquetes `.py/.dyn/manifest`, hashes, timeout de cola, gate de autorización genérica y rechazo de modelos fuera de `test-models`. No ejecuta Revit. |
| `python -B tests/run_routine_builder_mcp_smoke.py` | PASS | MCP stdio, confirmación al guardar una escritura, rutina temporal, estructura PythonNet3/Manual, hashes y limpieza. |
| `python -B tests/run_dynamo_runner_pipe_smoke.py` | PASS | Loop Builder MCP → rutina guardada → Runner MCP → named pipe/bridge falso; deniega toda ejecución genérica sin autorización, comprueba la promoción a `validated` y que Builder la vuelva a listar. No ejecuta Revit ni Dynamo. |
| `python -B tests/run_dynamo_runner_mcp_smoke.py` | PASS | Contrato stdio y descubrimiento de los tres tools del runner. No conecta con Revit si no se usa un modo live. |
| `python -B tests/run_revit_mcp_readonly_smoke.py` | FAIL, timeout actual | `initialize` y `tools/list` pasan; las consultas de vista/selección no respondieron dentro del timeout de 60 s. El proceso Revit sigue abierto y no se llamó ninguna escritura. El smoke histórico del 24-09-2026 sí había respondido. |
| `revit/bridge/build.ps1 -Package` | PASS | DLL nuevo de 30.720 bytes compilado contra las APIs locales; el binario generado, el paquete y el archivo SHA-256 coinciden. |
| `revit/bridge/install.ps1 -Force -StageWhileRevitOpen` | PASS | El paquete endurecido se copió al perfil de usuario mientras la sesión productiva permanecía abierta; el SHA-256 instalado coincide con `dist/`. |
| Copia del RVT de muestra | PASS | Tamaño y SHA-256 de la copia coinciden con el sample Autodesk instalado; el RVT está excluido de Git. |
| Carga del add-in en sesión de prueba | PENDIENTE | La nueva instancia con el RVT descartable presenta el aviso del DLL sin firma. Falta elegir **Load Once**; no se modificó la configuración global de seguridad. |
| Ejecución de Dynamo desde el bridge dentro de Revit | PENDIENTE | Requiere resolver la carga del add-in y completar el smoke read-only en el RVT de prueba. No se probó sobre el proyecto productivo. |
| Escritura PythonNet3 y lectura posterior de Comments | PENDIENTE | Requiere completar primero la prueba genérica read-only en el test RVT. |
| Herramientas de escritura tipadas rvt-mcp desde Codex | PARCIAL | El inventario y esquemas están revisados; las operaciones no se probaron una por una. |
| Carga de `.codex/config.toml` desde Codex | PARCIAL | Se añadió confianza local para esta ruta y `codex mcp list` muestra los tres servidores del proyecto habilitados. La sesión `codex exec` consultada no expuso esos tools; falta comprobarlos en el proyecto abierto desde la app Codex. |

## Qué puede hacer hoy

El paquete ya puede preparar rutinas a partir de código PythonNet3, guardarlas con su grafo Dynamo y manifiesto, y exponerlas para ejecución mediante MCP con aprobación. Para otras acciones, rvt-mcp aporta herramientas Revit tipadas de lectura, creación y edición dentro de una lista curada. El próximo gate comprueba que Dynamo 4.0.2 realmente inicia y retorna resultados desde Revit 2027.

Todavía **no** hay evidencia de que el loop genérico funcione en vivo ni de que cubra cualquier solicitud BIM. El código PythonNet3 se ejecuta dentro del proceso Revit con permisos del usuario: la restricción a `test-models` protege el documento objetivo, pero no sandboxea el acceso del script a archivos o red. El usuario debe revisar código y aprobar cada ejecución de escritura.

## Siguiente prueba segura

1. Conservar intacta la sesión productiva que está abierta.
2. En la sesión nueva con el RVT de prueba, elegir **Load Once** para `BIM-AI Dynamo Bridge`. No cambiar configuraciones globales de seguridad; conservar abierta la sesión productiva sin tocarla.
3. Comprobar `ping_revit_dynamo_bridge` y ejecutar `python -B tests/run_dynamo_runner_mcp_smoke.py --live-generic`. Esta rutina solo lee título/vista/selección/cantidad de elementos y comprueba que el documento queda sin cambios.
4. Si la lectura genérica pasa, seleccionar en ese RVT un elemento descartable con Comments editable y ejecutar `set_comments_on_selection` con aprobación. Confirmar que el resultado y `DocumentChanged` enumeran el mismo ID, luego releer Comments mediante rvt-mcp.
5. Si el add-in falla, conservar salida de Revit y status del runner, corregir el bridge y repetir solo en el RVT de prueba.

La UI de Codex en la app sigue siendo la ventana de conversación. No hay un panel acoplable dentro de Revit todavía; se evalúa después de probar el flujo técnico y observar el uso real.
