# Registro de pruebas MCP — 26 de septiembre de 2026

| Prueba | Resultado | Alcance |
|---|---|---|
| rvt-mcp v0.6.2: SHA-256 oficial | PASS | ZIP coincide con el hash fijado en el instalador. |
| rvt-mcp `initialize` + `tools/list` | PASS | 51 herramientas para los toolsets configurados; perfil Codex filtra una lista acotada y no habilita ToolBaker ni send-code. |
| rvt-mcp vista/selección en Revit | PASS, histórico | Consultas de solo lectura respondieron en la sesión iniciada por el usuario; no se modificó el documento. El detalle del proyecto se omitió. |
| rvt-mcp read-only repetido hoy | TIMEOUT | `initialize` y `tools/list` responden; ninguna consulta de vista/selección respondió dentro del timeout de 60 s. El proceso Revit sigue abierto. No se llamó ninguna escritura ni se guardó contenido del modelo. |
| Builder MCP stdio | PASS | `tests/run_routine_builder_mcp_smoke.py` creó una rutina temporal, comprobó PythonNet3/Manual/result wrapper/hashes y limpió los archivos. Escritura sin confirmación se rechazó. |
| Wrapper JSON PythonNet3 | PASS | Limita salida a 64 KiB, exige correlation ID válido y rechaza tipos no JSON. |
| Rutinas y modelos de prueba | PASS offline | 21 pruebas cubren fakes, alcance de Comments, estructura `.py/.dyn/manifest`, hashes, expiración de cola y protección contra documentos fuera de `revit/test-models/`. |
| Runner MCP stdio | PASS | `tests/run_dynamo_runner_mcp_smoke.py` descubre tres tools: ping, lectura fijada y ejecución de rutina guardada. |
| Named pipe del runner | PASS con fake | `tests/run_dynamo_runner_pipe_smoke.py` cubre ping, lectura, ejecución genérica, rechazo sin confirmación, promoción del manifiesto y timeout sin bridge. No inicia Revit/Dynamo. |
| Build y paquete del add-in | PASS | Roslyn compiló contra RevitAPI, RevitAPIUI, DynamoRevitDS y .NET Runtime 10; DLL distribuido e instalado coinciden por SHA-256. |
| Instalación del bridge | PASS, staged | Add-in y bridge.json están instalados por usuario. El DLL está sin firma; se inició otra sesión de Revit con el RVT desechable, pero el proceso no lo cargó. La sesión productiva permanece intacta. |
| RVT de prueba | PASS | Copia local de `Snowdon Towers Sample HVAC.rvt` incluida con Revit 2027; coincide en tamaño/hash y está ignorada por Git. |
| Smoke vivo bridge → Revit | PENDIENTE | `--live-generic` devolvió `bridge_connect_timeout`; el proceso de prueba sigue sin el DLL cargado. Ningún modelo cambió. |
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

Con el add-in cargado en Revit 2027 y el RVT desechable abierto, guardado y sin cambios pendientes:

```powershell
python -B tests/run_dynamo_runner_mcp_smoke.py --live-generic
```

El modo llama `read_test_model_summary` y no debe modificar el modelo. `--live` llama la rutina fijada `read_active_view_selection`, también solo lectura.

Después de validar esa lectura, seleccionar un elemento del RVT desechable y pedir a Codex que ejecute `set_comments_on_selection`. La ejecución requiere aprobación de la herramienta. Deben coincidir los IDs modificados de Revit y `affected_ids`; después se vuelve a consultar el parámetro Comments mediante rvt-mcp.
