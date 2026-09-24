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
| 2026-09-24 | rvt-mcp initialize + tools/list (grupos de perfil) | Aprobado | Lista 51 herramientas para `query,create,modify,view,annotation`. El proyecto expone una lista curada de consultas y escrituras tipadas; esto valida el inventario/esquemas, no su ejecución en el modelo. |
| 2026-09-24 | Rutina base read-only: pruebas offline | Aprobado | `python -m unittest discover -s tests -v`: 4 pruebas aprobadas sobre documento ausente, selección vacía, elementos/categorías y coherencia/hash del paquete. Usa dobles de Revit; la ejecución visual del `.dyn` en Dynamo sigue pendiente. |
| Pendiente | Codex `tools/list` desde la app | Pendiente | Abrir este proyecto como confiable y reiniciar Codex. |
| Pendiente | Abrir/correr grafo PythonNet3 | Pendiente | Revit/Dynamo debe iniciarse interactivamente. |
| Pendiente | MCP → ejecución automática de Dynamo | Fuera del prototipo actual | Requiere implementar runner acotado antes de declarar el flujo completo. |

## Criterios para vista/selección

- La vista reportada coincide con la vista activa en Revit.
- IDs y categorías coinciden con la selección manual.
- No se expone borrado, ejecución arbitraria C#/Python, ToolBaker ni ejecución automática de Dynamo.
- Antes de aprobar una escritura tipada, confirmar archivo descartable, alcance, valores/unidades y resultado esperado; verificar el modelo después.
- Revit sigue estable y no se guardan logs de contenido sensible.
