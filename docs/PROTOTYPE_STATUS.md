# Estado del prototipo

**Actualizado:** 30 de septiembre de 2026
**Objetivo:** Chat en Dynamo → Codex → MCP → rutina guardada → bridge Revit → Dynamo/PythonNet3 → verificación y reutilización.

## Estado actual

- Revit 2027 build 27.0.20.10 y Dynamo 4.0.2.3852 están instalados. rvt-mcp v0.6.2 ya respondió consultas de solo lectura en este PC.
- El proyecto Codex contiene tres MCP: rvt-mcp con una lista curada de herramientas tipadas, generador/listado de rutinas y runner Dynamo. Las llamadas tienen aprobación por herramienta.
- El generador guarda fuente `.py`, un `.dyn` manual con un único nodo PythonNet3, wrapper de resultado JSON y un manifiesto con hashes.
- La extensión lateral **Asistente BIM** para Dynamo compila contra las DLL locales de Dynamo 4.0.2 con cero errores/advertencias. Inicia `codex app-server` en el proyecto, mantiene un hilo temporal y muestra aprobaciones antes de las llamadas MCP. El DLL portable y su hash están en `dynamo/extension/dist/`; ya se validó desde el cliente C# real una consulta `list_saved_routines` por app-server → MCP. Esa prueba no carga la interfaz en Dynamo ni accede a Revit.
- El runner comprueba los hashes y el formato del grafo tanto en PowerShell como en el add-in. Las rutinas genéricas solo corren con un RVT local guardado y no workshared dentro de `revit/test-models/`; toda ejecución requiere `authorize_possible_model_writes=true`, aunque el manifiesto diga solo lectura, porque PythonNet3 no permite demostrar estáticamente que no habrá escrituras. Para rutinas declaradas como escritoras, el wrapper debe devolver `affected_ids` que coincidan con los elementos reportados por Revit `DocumentChanged`.
- El add-in compila contra RevitAPI, RevitAPIUI, DynamoRevitDS y .NET Runtime 10. Está instalado en el perfil de usuario. El DLL no está firmado. La captura del 30-09 muestra el cuadro de referencias sin resolver del RVT descartable; `doctor.ps1` no detecta ese RVT en el título y el pipe no está disponible. No se usó el modelo productivo para las pruebas del bridge.
- El paquete `dist/` y la copia instalada por usuario coinciden en SHA-256 (`9205c035…`). La instancia previa del modelo de prueba se cerró sin guardar y el hash del RVT en disco permaneció igual.
- Hay una copia local ignorada por Git de `Snowdon Towers Sample HVAC.rvt` en `revit/test-models/`, con sus cinco RVT vinculados vecinos. El preparador validó SHA-256 de cada copia; el HVAC conserva `3368FDBB…` y solo su copia local quedó escribible. El sample fuente de Autodesk sigue en solo lectura.
- Rutinas incluidas: `read_active_view_selection` (solo lectura y hash fijado), `read_test_model_summary` (solo lectura genérica) y `set_comments_on_selection` (borrador de escritura con protección interna de ruta).
- Endurecimiento del 26-09-2026: la revisión estática encontró que el manifiesto podía declarar una rutina genérica como solo lectura y omitir su confirmación aunque el código intentara escribir. El runner y el add-in ahora exigen autorización de posible escritura en toda ejecución genérica, sin confiar en esa etiqueta.

## Evidencia de pruebas

| Prueba | Resultado | Alcance |
|---|---|---|
| Revalidación offline, 30-09-2026 | PASS | 27 pruebas unitarias, Builder MCP, Runner MCP y loop Builder → Runner con bridge falso; confirma generación, autorización, guardado, ejecución simulada y reutilización, pero no ejecuta Revit/Dynamo. |
| rvt-mcp read-only, 28-09-2026 | PASS | `initialize`, descubrimiento de 16 herramientas y lecturas de vista activa/selección; sin escrituras. |
| Codex CLI → MCP de rutinas, 30-09-2026 | PASS | Desde esta carpeta, una sesión CLI efímera llamó `bim_ai_routines.list_saved_routines` y recibió las tres rutinas. Se usó revisión automática solo para esta consulta de lectura; ninguna rutina se ejecutó. La app Codex aún debe comprobarse aparte. |
| Extensión Dynamo build, 30-09-2026 | PASS | Compilación Release contra Dynamo 4.0.2; 0 advertencias y 0 errores. Genera DLL portable, manifiesto de la vista lateral y SHA-256. |
| Cliente C# de la extensión → app-server → MCP, 30-09-2026 | PASS | Una solicitud en lenguaje natural creó una rutina temporal PythonNet3 de solo lectura; el smoke verificó `.py`, grafo `.dyn` Manual, manifiesto/hash, aprobó la llamada MCP, volvió a listar la rutina y limpió los archivos temporales. No abre Dynamo ni Revit. |
| `doctor.ps1`, 30-09-2026 | PASS + WAIT | 12 comprobaciones pasaron y no hubo fallos. Incluye el permiso de escritura local del host y los cinco vínculos. Revit aún no muestra el modelo descartable en su título y el pipe no existe; diagnóstico de solo lectura. |
| Paquete portable anterior, 30-09-2026 | PASS | `package.ps1` empaquetó el commit `e9ebdef` en 54 archivos y excluyó RVT/RFA/RTE, `.codex/config.toml`, tokens y rutas privadas; SHA-256 `46bbb4fe203956efe7394bad531f706f68a9fb744ab09426024a3b01ff2fc426`. |
| Paquete con extensión Dynamo, 30-09-2026 | PASS | Windows PowerShell 5.1 empaquetó el commit `3454537` en 70 archivos, incluyó DLL/manifiesto/instalador de Dynamo y pasó la inspección de secretos, modelos y rutas privadas; SHA-256 `1bf81b60971a29aa61ddeef49b16c371808e4949dc00e0b82a5625bd575c2b0f`. |
| `python -B -m unittest discover -s tests -p 'test_*.py'` | PASS, 27 pruebas | Fakes de Revit, límites del wrapper JSON, integridad de paquetes `.py/.dyn/manifest`, hashes, timeout de cola, gate de autorización genérica y rechazo de modelos fuera de `test-models`. No ejecuta Revit. |
| `python -B tests/run_routine_builder_mcp_smoke.py` | PASS | MCP stdio, confirmación al guardar una escritura, rutina temporal, estructura PythonNet3/Manual, hashes y limpieza. |
| `python -B tests/run_dynamo_runner_pipe_smoke.py` | PASS | Loop Builder MCP → rutina guardada → Runner MCP → named pipe/bridge falso; deniega toda ejecución genérica sin autorización, comprueba la promoción a `validated` y que Builder la vuelva a listar. No ejecuta Revit ni Dynamo. |
| `python -B tests/run_dynamo_runner_mcp_smoke.py` | PASS | Contrato stdio y descubrimiento de los tres tools del runner. No conecta con Revit si no se usa un modo live. |
| `python -B tests/run_revit_mcp_readonly_smoke.py` | PASS, 26-09-2026 | `initialize`, descubrimiento de 16 herramientas y lecturas de vista/selección; no ejecutó escrituras ni imprimió datos del modelo. |
| `revit/bridge/build.ps1 -Package` | PASS | DLL de 36.352 bytes compilado contra las APIs locales; el paquete y el archivo SHA-256 coinciden (`9205c035…`). |
| `revit/bridge/install.ps1 -Force -StageWhileRevitOpen` | PASS, staged | El paquete endurecido se copió al perfil de usuario; el SHA-256 instalado coincide con `dist/`. |
| Copia del RVT y vínculos de muestra | PASS | El HVAC mantiene SHA-256 `3368FDBB…`; solo se quitó el atributo de solo lectura a la copia de prueba. Se copiaron y verificaron los cinco RVT relativos requeridos; una lectura de `TransmissionData` encontró las cinco rutas resueltas junto al host. Todos los RVT siguen excluidos de Git. |
| Sesión de prueba actual, 30-09-2026 | PENDIENTE | La sesión abierta es productiva y se inició antes de instalar el bridge. `doctor.ps1` detecta esa condición; el pipe no puede aparecer hasta una sesión nueva. La sesión productiva no se usará para la prueba. La extensión Dynamo está compilada, pero no instalada en esa sesión. |
| Ventana Asistente BIM dentro de Dynamo | PENDIENTE | Falta instalarla con Revit cerrado y abrir una sesión nueva para confirmar que aparece la pestaña y responde visualmente. La prueba C# verifica el protocolo/app-server/MCP, no la UI de Dynamo. |
| Smoke vivo bridge → Revit, 28-09-2026 | FAIL | El named pipe aceptó la llamada, pero `ping_revit_dynamo_bridge` terminó con `revit_dispatch_timeout_before_execution` tras 90 s. La captura de Revit muestra un cuadro con cinco referencias sin resolver. No hubo ejecución de rutina y el SHA-256 del RVT de prueba permaneció `3368FDBB…`. |
| Ejecución de Dynamo desde el bridge dentro de Revit | PENDIENTE | Requiere cerrar el cuadro de referencias en la sesión del RVT de prueba, repetir primero el ping/read-only y confirmar que Dynamo devuelve el resultado. No se probó sobre el proyecto productivo. |
| Escritura PythonNet3 y lectura posterior de Comments | PENDIENTE | Requiere completar primero la prueba genérica read-only en el test RVT. |
| Herramientas de escritura tipadas rvt-mcp desde Codex | PARCIAL | El inventario y esquemas están revisados; las operaciones no se probaron una por una. |
| Carga MCP desde Codex CLI | PASS, cableado y llamada de lectura | `codex mcp list` muestra `rvt_mcp`, `bim_ai_routines` y `bim_ai_dynamo_runner` habilitados. La llamada real a `list_saved_routines` respondió; el acceso desde la app Codex todavía no está verificado. |

## Qué puede hacer hoy

El paquete ya puede preparar rutinas a partir de código PythonNet3, guardarlas con su grafo Dynamo y manifiesto, y exponerlas para ejecución mediante MCP con aprobación. Para otras acciones, rvt-mcp aporta herramientas Revit tipadas de lectura, creación y edición dentro de una lista curada. El próximo gate comprueba que Dynamo 4.0.2 realmente inicia y retorna resultados desde Revit 2027.

Todavía **no** hay evidencia de que el loop genérico funcione en vivo ni de que cubra cualquier solicitud BIM. El código PythonNet3 se ejecuta dentro del proceso Revit con permisos del usuario: la restricción a `test-models` protege el documento objetivo, pero no sandboxea el acceso del script a archivos o red. El usuario debe revisar código y aprobar cada ejecución de escritura.

## Siguiente prueba segura

1. Conservar intacta la sesión productiva que está abierta.
2. Abrir en Revit 2027 `revit/test-models/BIM-AI-Dynamo-Test-9205.rvt`. Los cinco RVT vinculados ya están junto al host y solo la copia de prueba es escribible. Si el aviso ya estaba abierto antes de copiarlos, termina ese intento para el RVT descartable y vuelve a abrirlo; si el aviso persiste, elige **«Ignorar y abrir el proyecto»** solo para este RVT.
3. Comprobar `ping_revit_dynamo_bridge` y ejecutar `python -B tests/run_dynamo_runner_mcp_smoke.py --live-generic`. Esta rutina solo lee título/vista/selección/cantidad de elementos y comprueba que el documento queda sin cambios.
4. Si la lectura genérica pasa, seleccionar en ese RVT un elemento descartable con Comments editable y ejecutar `set_comments_on_selection` con aprobación. Confirmar que el resultado y `DocumentChanged` enumeran el mismo ID, luego releer Comments mediante rvt-mcp.
5. Si el add-in falla, conservar salida de Revit y status del runner, corregir el bridge y repetir solo en el RVT de prueba.

La extensión de Dynamo ya está implementada y empaquetada como panel lateral **Asistente BIM**. Aún no se ha confirmado visualmente en una sesión de Dynamo. Codex genera/propone las acciones y las rutinas se guardan para reutilizarlas; la ejecución que puede cambiar el modelo pide aprobación por llamada. La integración app-server de Codex es experimental ([documentación oficial](https://developers.openai.com/siwc/token-sharing-open-source/codex-app-server)) y el loop genérico Dynamo → Revit todavía no está demostrado en vivo.
