# Registro de pruebas MCP — actualizado el 2 de octubre de 2026

| Prueba | Resultado | Alcance |
|---|---|---|
| UI Asistente BIM 0.2.0, 1-10-2026 | PASS build/render | Build sin advertencias y render WPF 200 × 820 px; incluye botón Ver contexto, chat y composición de adjuntos. La build está instalada y espera la próxima sesión Revit/Dynamo para carga visual. |
| Codex app-server: imagen + referencia de texto, 1-10-2026 | PASS | El cliente C# de la extensión envió un PNG como imagen y un `.txt` como referencia. Codex reconoció el cuadrado rojo y devolvió el token de prueba; el smoke limpió sus archivos temporales. No llamó Revit ni ejecutó código adjunto. |
| Suite Python, 1-10-2026 | PASS, 43 pruebas | Contratos de UI, contexto y exclusión de `logs/attachments/` del repositorio y del empaquetador. |
| Contexto actual por Codex → rvt-mcp, 1-10-2026 | PASS | `--revit-readonly` consultó vista activa y selección en serie. Valida el acceso MCP usado por **Ver**, no la DLL candidata dentro del Dynamo abierto. |
| Instalación build 0.2.0, 1-10-2026 | PASS, carga pendiente | `install.ps1 -Force` actualizó paquete por usuario y manifiesto oficial con SHA-256 `7bc1eb1d…`. `doctor.ps1` confirma ambos; Revit está cerrado y el panel aún debe cargarse en la siguiente sesión. |
| Carga de Dynamo 4.0.2, 2-10-2026 | FAIL confirmado por log | La sesión abierta no mostró Asistente BIM. El log de Dynamo solo enumera las extensiones integradas. La ruta por usuario anterior se reemplazó por la carpeta oficial del host y un manifiesto relativo; falta reinstalar con Revit cerrado y verificar el siguiente log. |
| Suite Python y carga observable, 2-10-2026 | PASS, 44 pruebas | Comprueba el manifiesto relativo y que `doctor.ps1` espere o avise según la fecha/log de Dynamo, sin confundir archivos instalados con la carga del panel. |
| Smoke C# Codex app-server + MCP + adjuntos, 2-10-2026 | PASS en reintento | Creó/listó una rutina temporal y leyó imagen + texto. Un primer intento recibió dos solicitudes de creación y el arnés limpió los archivos; el reintento completo pasó y también limpió. |
| `doctor.ps1` tras instalación, 1-10-2026 | 13 PASS + 2 WAIT | Cero WARN/FAIL; valida la build 0.2.0 y deja WAIT hasta abrir Revit con el RVT de prueba y habilitar el pipe del bridge. |
| Revalidación offline, 30-09-2026 | PASS | 27 pruebas unitarias; Builder MCP; Runner MCP; loop completo contra bridge falso. Confirma integridad, autorización de escritura, ejecución simulada y reutilización; no ejecuta Revit/Dynamo. |
| Diagnóstico local, 30-09-2026 | PASS + WAIT | `doctor.ps1`: Revit/Dynamo, Codex CLI, los tres MCP, add-in, configuración, hash del DLL, permiso de escritura del RVT y cinco vínculos locales verificados. Revit aún no muestra el RVT descartable en el título y el pipe no está disponible. No modifica Revit. |
| Codex CLI → Builder MCP, 30-09-2026 | PASS | Una sesión CLI efímera invocó `bim_ai_routines.list_saved_routines` y recibió tres rutinas. Se permitió solo esta consulta de lectura mediante revisión automática; no se ejecutó ninguna rutina. |
| ZIP portable previo, 30-09-2026 | PASS | `package.ps1` produjo 54 archivos desde `e9ebdef`; validó configuración/modelos privados, patrones de token y rutas. SHA-256 `46bbb4fe203956efe7394bad531f706f68a9fb744ab09426024a3b01ff2fc426`. El paquete actualizado se genera después de este commit. |
| rvt-mcp v0.6.2: SHA-256 oficial | PASS | ZIP coincide con el hash fijado en el instalador. |
| rvt-mcp `initialize` + `tools/list` | PASS | 51 herramientas para los toolsets configurados; perfil Codex filtra una lista acotada y no habilita ToolBaker ni send-code. |
| rvt-mcp vista/selección en Revit | PASS, histórico | Consultas de solo lectura respondieron en la sesión iniciada por el usuario; no se modificó el documento. El detalle del proyecto se omitió. |
| rvt-mcp read-only, 28-09-2026 | PASS | `initialize`, descubrimiento de 16 herramientas y consultas de vista/selección respondieron. No se llamó ninguna escritura ni se imprimió información del modelo. |
| Builder MCP stdio | PASS | `tests/run_routine_builder_mcp_smoke.py` creó una rutina temporal, comprobó PythonNet3/Manual/result wrapper/hashes y limpió los archivos. Escritura sin confirmación se rechazó. |
| Wrapper JSON PythonNet3 | PASS | Limita salida a 64 KiB, exige correlation ID válido y rechaza tipos no JSON. |
| Rutinas y modelos de prueba | PASS offline | 27 pruebas cubren fakes, alcance de Comments, estructura `.py/.dyn/manifest`, hashes, expiración de cola, autorización de toda ejecución genérica y protección contra documentos fuera de `revit/test-models/`. |
| Runner MCP stdio | PASS | `tests/run_dynamo_runner_mcp_smoke.py` descubre tres tools: ping, lectura fijada y ejecución de rutina guardada. |
| Loop Builder → Runner | PASS con fake | `tests/run_dynamo_runner_pipe_smoke.py` crea una rutina con Builder MCP, rechaza llamadas genéricas sin autorización (incluida una rutina marcada read-only), la ejecuta contra un pipe y bridge falsos, promueve el manifiesto a `validated` y confirma que Builder vuelve a listarla. No inicia Revit/Dynamo. |
| Build y paquete del add-in | PASS | Roslyn compiló un DLL de 36.352 bytes contra RevitAPI, RevitAPIUI, DynamoRevitDS y .NET Runtime 10; DLL distribuido e instalado coinciden por SHA-256 `9205c035…`. |
| Instalación del bridge | PASS, staged | Add-in y bridge.json están instalados por usuario. El diario registró el aviso `TaskDialog_Security_Unsigned_File_Loading`; después, el pipe aceptó una llamada. La sesión productiva permanece intacta. El `ExternalEvent` sigue sin verificarse. |
| RVT de prueba y vínculos | PASS | `prepare-test-model.ps1` preservó el hash del HVAC, quitó solo lectura en la copia local y copió/verificó los cinco RVT relativos desde Samples; `TransmissionData` confirma que existen junto al host. Todos se excluyen de Git. |
| Estado Revit, 30-09-2026 | PENDIENTE | La única sesión abierta es productiva y es anterior a la instalación del bridge; el test RVT está preparado pero Revit no muestra ese modelo ni hay pipe. `doctor.ps1` identifica que se necesita una sesión nueva. |
| Smoke vivo bridge → Revit, 28-09-2026 | FAIL | `--live-generic` conectó al named pipe, pero `ping_revit_dynamo_bridge` terminó con `revit_dispatch_timeout_before_execution` tras 90 s. La captura de Revit muestra el cuadro de cinco referencias sin resolver. La rutina no llegó a ejecutarse y el hash del RVT se mantuvo `3368FDBB…`. La automatización de escritorio no expone ventanas nativas en esta sesión. |
| Ejecución Dynamo PythonNet3 en Revit | PENDIENTE | Requiere cargar el add-in en la sesión del RVT de prueba y pasar primero el smoke read-only. |
| Escritura Comments + lectura posterior | PENDIENTE | Se hará en el RVT de prueba con un elemento seleccionado y aprobación explícita. |
| Herramientas Codex CLI desde esta carpeta | PASS | Se invocó el tool real `list_saved_routines` y devolvió tres rutinas. |
| Herramientas MCP en la app Codex | PENDIENTE | Falta comprobar que la app haya cargado las herramientas de este proyecto. |
| Suite offline actualizada, 1-10-2026 | PASS | 36 pruebas unitarias; suma controles para el ping/readiness, respuestas MCP con error y paquete Dynamo por usuario. Los smokes Builder/Runner/bridge falso continúan pasando. |
| Codex app-server, 1-10-2026 | PASS | Listó rutinas vía `bim_ai_routines.list_saved_routines` y pidió aclaración ante «Haz una planta para el segundo piso» sin llamar herramientas. La prueba de ambigüedad no consulta Revit. |
| Consulta rvt-mcp en estado actual, 1-10-2026 | ESPERA POR MODELO | Handshake y descubrimiento de 16 tools pasan. Las consultas de vista/selección reportan `no_active_document`; Revit está en Inicio sin RVT abierto. El smoke ya no clasifica ese texto de error como una lectura exitosa. |
| Estado bridge, 1-10-2026 | ESPERA POR REINICIO | El pipe responde `ready` en Revit 2027, pero el bridge cargado no expone `dynamoReadyForActiveDocument`, señal de que el proceso empezó antes del staging. No se ejecutó ninguna rutina en esta sesión. |

## Repetir pruebas offline

```powershell
python -B -m unittest discover -s tests -v
python -B tests/run_routine_builder_mcp_smoke.py
python -B tests/run_dynamo_runner_pipe_smoke.py
python -B tests/run_dynamo_runner_mcp_smoke.py
```

## Prueba viva read-only

Con el add-in cargado en Revit 2027 y el RVT desechable abierto, guardado y sin cuadros modales pendientes:

```powershell
python -B tests/run_dynamo_runner_mcp_smoke.py --live-generic
```

El modo llama `read_test_model_summary` y no debe modificar el modelo. `--live` llama la rutina fijada `read_active_view_selection`, también solo lectura.

Después de validar esa lectura, seleccionar un elemento del RVT desechable y pedir a Codex que ejecute `set_comments_on_selection`. La ejecución requiere aprobación de la herramienta. Deben coincidir los IDs modificados de Revit y `affected_ids`; después se vuelve a consultar el parámetro Comments mediante rvt-mcp.
