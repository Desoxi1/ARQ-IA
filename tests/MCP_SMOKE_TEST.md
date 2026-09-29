# Registro de pruebas MCP — actualizado el 29 de septiembre de 2026

| Prueba | Resultado | Alcance |
|---|---|---|
| Revalidación offline, 28-09-2026 | PASS | 27 pruebas unitarias; Builder MCP; Runner MCP; loop completo contra bridge falso. Confirma integridad, autorización de escritura, ejecución simulada y reutilización; no ejecuta Revit/Dynamo. |
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
| RVT de prueba | PASS | Copia local de `Snowdon Towers Sample HVAC.rvt` incluida con Revit 2027; coincide en tamaño/hash y está ignorada por Git. |
| Estado Revit, 29-09-2026 | PENDIENTE | La sesión de prueba se cerró; solo está abierto el proyecto productivo y no existe el pipe de prueba. El SHA-256 del RVT descartable sigue igual. |
| Smoke vivo bridge → Revit, 28-09-2026 | FAIL | `--live-generic` conectó al named pipe, pero `ping_revit_dynamo_bridge` terminó con `revit_dispatch_timeout_before_execution` tras 90 s. La captura de Revit muestra el cuadro de cinco referencias sin resolver. La rutina no llegó a ejecutarse y el hash del RVT se mantuvo `3368FDBB…`. La automatización de escritorio no expone ventanas nativas en esta sesión. |
| Ejecución Dynamo PythonNet3 en Revit | PENDIENTE | Requiere cargar el add-in en la sesión del RVT de prueba y pasar primero el smoke read-only. |
| Escritura Comments + lectura posterior | PENDIENTE | Se hará en el RVT de prueba con un elemento seleccionado y aprobación explícita. |
| Herramientas Codex desde esta carpeta | PENDIENTE | Abrir esta carpeta como proyecto confiable y comprobar `tools/list` en la app Codex. |

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
