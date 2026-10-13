# MCP de rutinas Dynamo

`RoutineBuilderMcp.ps1` implementa transporte MCP stdio con Windows PowerShell 5.1 y componentes integrados de Windows. Ofrece:

- `list_saved_routines`: muestra nombre, alcance, efecto declarado y estado de validación.
- `create_dynamo_routine`: escribe fuente PythonNet3, un grafo `.dyn` Manual con wrapper JSON fijado por hash y un manifiesto bajo `routines/`.

El servidor rechaza rutas elegidas por el llamador, nombres fuera del slug permitido, scripts mayores a 32 KiB, rutinas que no asignan a `OUT`, y escrituras declaradas sin `confirm_model_write=true` al guardar la rutina. Guardar una rutina no autoriza por sí solo su ejecución. `DynamoRunnerMcp.ps1` exige `authorize_possible_model_writes=true` en toda ejecución genérica porque el manifiesto no puede demostrar que PythonNet3 sea solo lectura. Revisa el código y autoriza la posible modificación del RVT antes de ejecutar. El runner usa el add-in separado `revit/bridge/`.

El runner ofrece ping, la rutina fija `read_active_view_selection` y `execute_saved_routine`. El ejecutor genérico vuelve a validar hashes, PythonNet3, modo Manual y ausencia de conectores/dependencias. Solo acepta un RVT local guardado bajo `revit/test-models/` y exige `authorize_possible_model_writes=true`. En Acceso controlado Codex solicita aprobación por herramienta; en Acceso completo no hay confirmación por llamada, por lo que la petición explícita del usuario debe incluir la acción y el alcance. La bandera declara riesgo, pero no sustituye la autorización del usuario. El bridge compara los IDs esperados con `DocumentChanged` y promueve el manifiesto después de una ejecución verificada.

La rutina fija de solo lectura pasó en Revit real con Dynamo/PythonNet3 sobre un RVT descartable el 05-10-2026. La rutina genérica de solo lectura agotó el tiempo al cambiar de grafo; el ajuste posterior sigue sin validación viva. Las escrituras complejas tampoco están validadas. PythonNet3 tiene los permisos del proceso Revit, por lo que la revisión de código y el RVT descartable son obligatorios para este POC. Consulta [estado vigente](../docs/PROTOTYPE_STATUS.md).
