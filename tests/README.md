# Validación

El registro y el estado actual están en [MCP_SMOKE_TEST.md](MCP_SMOKE_TEST.md). La suite offline comprueba rutinas, hashes, permisos de ejecución, wrapper JSON y transporte MCP/named pipe con un bridge simulado. No carga Revit ni Dynamo.

Ejecuta las pruebas offline desde la raíz:

```powershell
python -B -m unittest discover -s tests -v
python -B tests/run_routine_builder_mcp_smoke.py
python -B tests/run_dynamo_runner_pipe_smoke.py
python -B tests/run_dynamo_runner_mcp_smoke.py
```

Para comprobar rvt-mcp en una sesión Revit activa, ejecuta:

```powershell
python -B tests/run_revit_mcp_readonly_smoke.py
```

Consulta solo vista y selección y no imprime datos del modelo. Si agota el timeout, no repite escrituras: es un smoke solo de lectura.

La primera integración viva después de cargar el add-in debe usar el RVT desechable dentro de `revit/test-models/`:

```powershell
python -B tests/run_dynamo_runner_mcp_smoke.py --live-generic
```

`read_test_model_summary` lee título, vista, selección y cantidad de elementos; Revit debe quedar sin cambios. El modo `--live` ejecuta la rutina fija `read_active_view_selection` de solo lectura.

Después de aprobar el smoke read-only, selecciona un elemento de prueba y ejecuta `set_comments_on_selection` con aprobación MCP. Relee Comments con rvt-mcp y confirma que `affected_ids` coincide con los IDs reportados por Revit. No uses el bridge genérico sobre un proyecto productivo.

El smoke MCP del runner sin flags valida solo el contrato. El smoke `run_dynamo_runner_pipe_smoke.py` usa un pipe aleatorio y un fake bridge; no es evidencia de ejecución Revit/Dynamo.
