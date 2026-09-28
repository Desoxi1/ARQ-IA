# Estado del prototipo

**Actualizado:** 28 de septiembre de 2026
**Objetivo:** Codex → MCP → rutina guardada → bridge Revit → Dynamo/PythonNet3 → verificación y reutilización.

## Estado actual

- Revit 2027 build 27.0.20.10 y Dynamo 4.0.2.3852 están instalados. rvt-mcp v0.6.2 ya respondió consultas de solo lectura en este PC.
- El proyecto Codex contiene tres MCP: rvt-mcp con una lista curada de herramientas tipadas, generador/listado de rutinas y runner Dynamo. Las llamadas tienen aprobación por herramienta.
- El generador guarda fuente `.py`, un `.dyn` manual con un único nodo PythonNet3, wrapper de resultado JSON y un manifiesto con hashes.
- El runner comprueba los hashes y el formato del grafo tanto en PowerShell como en el add-in. Las rutinas genéricas solo corren con un RVT local guardado y no workshared dentro de `revit/test-models/`; toda ejecución requiere `authorize_possible_model_writes=true`, aunque el manifiesto diga solo lectura, porque PythonNet3 no permite demostrar estáticamente que no habrá escrituras. Para rutinas declaradas como escritoras, el wrapper debe devolver `affected_ids` que coincidan con los elementos reportados por Revit `DocumentChanged`.
- El add-in compila contra RevitAPI, RevitAPIUI, DynamoRevitDS y .NET Runtime 10. Está instalado en el perfil de usuario. El DLL no está firmado. La sesión productiva sigue abierta e intacta. La instancia de prueba acepta conexiones al named pipe del bridge, pero el `ExternalEvent` no se despachó durante el smoke vivo.
- El paquete `dist/` y la copia instalada por usuario coinciden en SHA-256 (`9205c035…`). La instancia previa del modelo de prueba se cerró sin guardar y el hash del RVT en disco permaneció igual.
- Hay copias locales, ignoradas por Git, de `Snowdon Towers Sample HVAC.rvt` bajo `revit/test-models/`; ambas provienen de los Samples instalados con Revit 2027.
- Rutinas incluidas: `read_active_view_selection` (solo lectura y hash fijado), `read_test_model_summary` (solo lectura genérica) y `set_comments_on_selection` (borrador de escritura con protección interna de ruta).
- Endurecimiento del 26-09-2026: la revisión estática encontró que el manifiesto podía declarar una rutina genérica como solo lectura y omitir su confirmación aunque el código intentara escribir. El runner y el add-in ahora exigen autorización de posible escritura en toda ejecución genérica, sin confiar en esa etiqueta.

## Evidencia de pruebas

| Prueba | Resultado | Alcance |
|---|---|---|
| Revalidación offline, 28-09-2026 | PASS | 27 pruebas unitarias, Builder MCP, Runner MCP, loop Builder → Runner con bridge falso y contrato de tres tools; confirma generación, autorización, guardado y reutilización, pero no ejecuta Revit/Dynamo. |
| rvt-mcp read-only, 28-09-2026 | PASS | `initialize`, descubrimiento de 16 herramientas y lecturas de vista activa/selección; sin escrituras. |
| Registro MCP de Codex CLI, 28-09-2026 | PASS | `codex mcp list` muestra los tres MCP del proyecto habilitados. El acceso desde la app Codex con el proyecto abierto sigue sin comprobar. |
| `python -B -m unittest discover -s tests -p 'test_*.py'` | PASS, 27 pruebas | Fakes de Revit, límites del wrapper JSON, integridad de paquetes `.py/.dyn/manifest`, hashes, timeout de cola, gate de autorización genérica y rechazo de modelos fuera de `test-models`. No ejecuta Revit. |
| `python -B tests/run_routine_builder_mcp_smoke.py` | PASS | MCP stdio, confirmación al guardar una escritura, rutina temporal, estructura PythonNet3/Manual, hashes y limpieza. |
| `python -B tests/run_dynamo_runner_pipe_smoke.py` | PASS | Loop Builder MCP → rutina guardada → Runner MCP → named pipe/bridge falso; deniega toda ejecución genérica sin autorización, comprueba la promoción a `validated` y que Builder la vuelva a listar. No ejecuta Revit ni Dynamo. |
| `python -B tests/run_dynamo_runner_mcp_smoke.py` | PASS | Contrato stdio y descubrimiento de los tres tools del runner. No conecta con Revit si no se usa un modo live. |
| `python -B tests/run_revit_mcp_readonly_smoke.py` | PASS, 26-09-2026 | `initialize`, descubrimiento de 16 herramientas y lecturas de vista/selección; no ejecutó escrituras ni imprimió datos del modelo. |
| `revit/bridge/build.ps1 -Package` | PASS | DLL de 36.352 bytes compilado contra las APIs locales; el paquete y el archivo SHA-256 coinciden (`9205c035…`). |
| `revit/bridge/install.ps1 -Force -StageWhileRevitOpen` | PASS, staged | El paquete endurecido se copió al perfil de usuario; el SHA-256 instalado coincide con `dist/`. |
| Copia del RVT de muestra | PASS | Tamaño y SHA-256 de la copia coinciden con el sample Autodesk instalado; el RVT está excluido de Git. |
| Listener del add-in en sesión de prueba | PARCIAL | El named pipe acepta la conexión; la llamada no alcanzó `IExternalEventHandler.Execute`. La captura muestra un cuadro modal abierto. La app Codex no expone ventanas nativas a CUA en esta sesión. |
| Smoke vivo bridge → Revit, 28-09-2026 | FAIL | El named pipe aceptó la llamada, pero `ping_revit_dynamo_bridge` terminó con `revit_dispatch_timeout_before_execution` tras 90 s. La captura de Revit muestra un cuadro con cinco referencias sin resolver. No hubo ejecución de rutina y el SHA-256 del RVT de prueba permaneció `3368FDBB…`. |
| Ejecución de Dynamo desde el bridge dentro de Revit | PENDIENTE | Requiere cerrar el cuadro de referencias en la sesión del RVT de prueba, repetir primero el ping/read-only y confirmar que Dynamo devuelve el resultado. No se probó sobre el proyecto productivo. |
| Escritura PythonNet3 y lectura posterior de Comments | PENDIENTE | Requiere completar primero la prueba genérica read-only en el test RVT. |
| Herramientas de escritura tipadas rvt-mcp desde Codex | PARCIAL | El inventario y esquemas están revisados; las operaciones no se probaron una por una. |
| Carga de `.codex/config.toml` desde Codex | PASS, cableado CLI | `codex mcp list` muestra `rvt_mcp`, `bim_ai_routines` y `bim_ai_dynamo_runner` habilitados; falta comprobar acceso a esos tools en la app Codex con este proyecto abierto. |

## Qué puede hacer hoy

El paquete ya puede preparar rutinas a partir de código PythonNet3, guardarlas con su grafo Dynamo y manifiesto, y exponerlas para ejecución mediante MCP con aprobación. Para otras acciones, rvt-mcp aporta herramientas Revit tipadas de lectura, creación y edición dentro de una lista curada. El próximo gate comprueba que Dynamo 4.0.2 realmente inicia y retorna resultados desde Revit 2027.

Todavía **no** hay evidencia de que el loop genérico funcione en vivo ni de que cubra cualquier solicitud BIM. El código PythonNet3 se ejecuta dentro del proceso Revit con permisos del usuario: la restricción a `test-models` protege el documento objetivo, pero no sandboxea el acceso del script a archivos o red. El usuario debe revisar código y aprobar cada ejecución de escritura.

## Siguiente prueba segura

1. Conservar intacta la sesión productiva que está abierta.
2. En la sesión con el RVT descartable, cerrar **«Referencias sin resolver»** eligiendo **«Ignorar y abrir el proyecto»**. La captura aportada muestra cinco referencias; esto aplica únicamente al RVT de prueba.
3. Comprobar `ping_revit_dynamo_bridge` y ejecutar `python -B tests/run_dynamo_runner_mcp_smoke.py --live-generic`. Esta rutina solo lee título/vista/selección/cantidad de elementos y comprueba que el documento queda sin cambios.
4. Si la lectura genérica pasa, seleccionar en ese RVT un elemento descartable con Comments editable y ejecutar `set_comments_on_selection` con aprobación. Confirmar que el resultado y `DocumentChanged` enumeran el mismo ID, luego releer Comments mediante rvt-mcp.
5. Si el add-in falla, conservar salida de Revit y status del runner, corregir el bridge y repetir solo en el RVT de prueba.

La UI de Codex en la app sigue siendo la ventana de conversación. No hay un panel acoplable dentro de Revit todavía; se evalúa después de probar el flujo técnico y observar el uso real.
