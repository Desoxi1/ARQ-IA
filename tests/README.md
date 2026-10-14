# Validación

Este directorio contiene validaciones de desarrollo. El [estado vigente del producto](../docs/PROTOTYPE_STATUS.md) es la fuente de verdad; [MCP_SMOKE_TEST.md](MCP_SMOKE_TEST.md) conserva el último resumen de evidencias históricas y sus límites. Las pruebas offline comprueban generación MCP, guardado/reutilización de rutinas, hashes, autorización de ejecución, wrapper JSON y named pipe con un bridge simulado. No cargan Revit ni Dynamo.

`test_mcp_tool_catalog.py` valida que cada herramienta publicada en el catálogo versionado esté clasificada, use nombres conocidos y respete los bloqueos de seguridad. No comprueba que cada operación funcione en una sesión real de Revit.

El historial local se verifica de forma aislada, sin iniciar Revit, Dynamo ni Codex:

```powershell
dotnet run --project tests/conversation_history_smoke/ConversationHistorySmoke.csproj --configuration Release
```

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

La integración viva usa el RVT desechable dentro de `revit/test-models/`. Abre primero el documento y la interfaz Dynamo en Revit; el bridge no inicializa Dynamo en modo invisible sobre el modelo. `ping_revit_dynamo_bridge` informa si Dynamo está abierto y enlazado al documento activo:

```powershell
python -B tests/run_dynamo_runner_mcp_smoke.py --live-generic
```

`read_test_model_summary` lee título, vista, selección y cantidad de elementos; Revit debe quedar sin cambios. El modo `--live` ejecuta la rutina fija `read_active_view_selection` de solo lectura.

Después de aprobar el smoke read-only, selecciona un elemento de prueba y ejecuta `set_comments_on_selection`. En acceso controlado, aprueba la llamada MCP; en acceso completo, la petición explícita del usuario cubre la ejecución sin confirmación por llamada. Relee Comments con rvt-mcp y confirma que `affected_ids` coincide con los IDs reportados por Revit. No uses el bridge genérico sobre un proyecto productivo.

El smoke MCP del runner sin flags valida solo el contrato. `run_dynamo_runner_pipe_smoke.py` recorre Builder MCP → rutina generada/guardada → Runner MCP → pipe aleatorio → fake bridge → manifiesto `validated` y listado de rutina reutilizable. No es evidencia de ejecución Revit/Dynamo.

Para comprobar Codex app-server desde esta carpeta, ejecuta `python -B tests/run_dynamo_extension_codex_smoke.py`; añade `--mcp` para permitir únicamente el listado de rutinas de solo lectura. `--revit-readonly` consulta secuencialmente la vista activa y la selección por `rvt-mcp`; necesita Revit abierto con el RVT de prueba y no modifica el modelo. La opción `--eval-ambiguity` prueba que «Haz una planta para el segundo piso» genera una pregunta que distingue una vista de planta de otras intenciones de modelado, como distribuir recintos o crear una losa. El arnés deniega cualquier llamada de herramienta en esa evaluación; no abre Dynamo ni modifica Revit.

Para probar únicamente el enrutamiento local de las guías del panel, sin iniciar Codex, MCP ni Revit, ejecuta `tests/run_dynamo_extension_client_smoke.ps1 -RoutingOnly`. Comprueba que una propuesta de vivienda use la ficha de diseño contextual y que peticiones explícitas de muros/cotas y ciclos sigan llegando a sus guías específicas.
