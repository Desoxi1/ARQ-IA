# Registro de pruebas MCP

| Fecha | Prueba | Resultado | Evidencia/límite |
|---|---|---|---|
| 2026-09-23 | SHA-256 del ZIP oficial rvt-mcp v0.6.2 | Aprobado | Esperado y calculado: `0d6239afcab47c729c30b8c5b763214c20c38c4b72de7e30c5a7191b4131b1ae`. |
| 2026-09-23 | Instalador oficial en WhatIf (Revit 2027, `-Client none`) | Aprobado | Solo previsualizó Addins R27 y servidor bajo carpetas del usuario. |
| 2026-09-23 | Instalación oficial para perfil de usuario | Aprobado | Addin manifest, DLL del plugin y servidor presentes; Revit estaba cerrado. |
| 2026-09-23 | rvt-mcp initialize + tools/list | Aprobado | Proceso v0.6.2 respondió por MCP stdio con toolset query; 16 tools, incluidas vista activa/selección; sin send-code. |
| 2026-09-23 | MCP de rutinas initialize/list/create/list | Aprobado | Cuatro respuestas JSON-RPC; creó `.py`, `.dyn`, manifiesto; el grafo tiene un nodo PythonNet3 y `RunType=Manual`. |
| 2026-09-23 | Sintaxis del script de muestra | Aprobado | `python -m py_compile routines\read_active_view_selection.py` terminó sin error. |
| 2026-09-23 | Rechazo de rutina de escritura no confirmada | Aprobado | `writes_model=true` con `confirm_model_write=false` devuelve error y no crea archivos. |
| 2026-09-23 | Consulta Revit vista activa + selección | No aprobado | Ambas devolvieron `Revit MCP plugin not running`. Revit no llegó a registrar el add-in en la sesión lanzada desde este entorno; no se leyó ni cambió ningún documento. |
| 2026-09-24 | Consulta Revit en sesión iniciada por el usuario | Aprobado | El servidor rvt-mcp en modo `query --read-only` obtuvo la vista activa y, tras selección manual, un muro. La lectura se repitió en esta fase sin cambios en el documento. Los IDs y nombres del modelo se excluyeron de la versión pública. Falta probar las herramientas desde Codex con el proyecto confiable abierto. |
| 2026-09-24 | Smoke de lectura MCP repetible | Aprobado | `python tests/run_revit_mcp_readonly_smoke.py`: initialize, lista de 16 herramientas query, vista activa y selección. La prueba no escribe ni muestra valores del modelo. |
| 2026-09-24 | Smoke MCP del generador de rutinas | Aprobado | `python tests/run_routine_builder_mcp_smoke.py`: initialize, tools/list, guardado temporal `.py/.dyn/manifest`, validación de motor y hashes, y eliminación segura de los tres archivos de prueba. No abre Dynamo ni modifica Revit. |
| 2026-09-24 | rvt-mcp initialize + tools/list (grupos de perfil) | Aprobado | Lista 51 herramientas para `query,create,modify,view,annotation`. El proyecto expone una lista curada de consultas y escrituras tipadas; esto valida el inventario/esquemas, no su ejecución en el modelo. |
| 2026-09-24 | Rutina base read-only: pruebas offline | Aprobado | `python -m unittest discover -s tests -v`: 4 pruebas aprobadas sobre documento ausente, selección vacía, elementos/categorías y coherencia/hash del paquete. Usa dobles de Revit; la ejecución visual del `.dyn` en Dynamo sigue pendiente. |
| 2026-09-24 | Compilación del add-in puente | Aprobado | `revit/bridge/build.ps1` compiló contra RevitAPI, RevitAPIUI, DynamoRevitDS y .NET Runtime 10. La DLL no se ha instalado ni cargado en Revit. |
| 2026-09-24 | Contrato MCP del runner Dynamo | Aprobado | Initialize y `tools/list` anuncian solo ping y ejecución del ID fijo `read_active_view_selection`. |
| 2026-09-24 | Arranque desde configuración local Codex | Aprobado | Se iniciaron los tres comandos de `.codex/config.toml`; rvt-mcp devolvió 51 tools, generador 2 y runner 2. Se verificaron las listas permitidas; no se invocaron operaciones. La app Codex aún debe abrir/confiar el proyecto y cargar la configuración. |
| 2026-09-24 | Smoke vivo del runner Revit | Pendiente | `python tests/run_dynamo_runner_mcp_smoke.py --live` pasó el contrato MCP pero falló en `REVIT_BRIDGE_PING`; el add-in no está cargado. No se ejecutó Dynamo ni se cambió el documento. |
| 2026-09-24 | DynamoCLI 4.0.2: prueba de grafo sintético | Fallido | El ejecutable salió sin XML de evaluación y registró una aserción ProtoCore. La prueba no usó Revit ni modificó modelos; no se usará DynamoCLI como runner hasta resolverlo. |
| 2026-09-25 | Transporte del runner con fake named pipe | Aprobado | `python tests/run_dynamo_runner_pipe_smoke.py`: verifica ping, respuesta de la rutina allowlisted y `bridge_connect_timeout` cuando falta servidor, usando un fake named pipe aleatorio. No inicia Revit/Dynamo ni demuestra ejecución dentro del add-in. |
| 2026-09-25 | Smoke vivo del runner tras reparar timeouts | Pendiente por instalación | El contrato MCP pasó y el ping devolvió `bridge_connect_timeout`; el add-in no está instalado/cargado porque Revit sigue abierto. La prueba no llegó a la rutina ni cambió el modelo. |
| Pendiente | Codex `tools/list` desde la app | Pendiente | Abrir este proyecto como confiable y reiniciar Codex. |
| Pendiente | Abrir/correr grafo PythonNet3 | Pendiente | Revit/Dynamo debe iniciarse interactivamente. |
| Pendiente | Ejecución del runner en Revit | Pendiente | Instalar el add-in con Revit cerrado, abrir un RVT descartable y volver a ejecutar la rutina read-only fijada. |
| Pendiente | Rutina de escritura mediante Dynamo | No habilitada | Requiere primero verificar el runner de lectura, diseñar una allowlist, probar en copia descartable y verificar el cambio mediante consulta posterior. |

## Criterios para vista/selección

- La vista reportada coincide con la vista activa en Revit.
- IDs y categorías coinciden con la selección manual.
- No se expone borrado, ejecución arbitraria C#/Python, ToolBaker ni ejecución automática de Dynamo.
- Antes de aprobar una escritura tipada, confirmar archivo descartable, alcance, valores/unidades y resultado esperado; verificar el modelo después.
- Revit sigue estable y no se guardan logs de contenido sensible.
