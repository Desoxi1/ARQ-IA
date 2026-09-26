# MCP de rutinas Dynamo

`RoutineBuilderMcp.ps1` implementa transporte MCP stdio con Windows PowerShell 5.1 y componentes integrados de Windows. Ofrece:

- `list_saved_routines`: muestra nombre, alcance, efecto declarado y estado de validación.
- `create_dynamo_routine`: escribe fuente PythonNet3, un grafo `.dyn` Manual con wrapper JSON fijado por hash y un manifiesto bajo `routines/`.

El servidor rechaza rutas elegidas por el llamador, nombres fuera del slug permitido, scripts mayores a 32 KiB, rutinas que no asignan a `OUT`, y rutinas de escritura sin `confirm_model_write=true`. La aprobación de guardar una rutina no aprueba su ejecución. Para ejecutar se usa `DynamoRunnerMcp.ps1` y el add-in separado `revit/bridge/`.

El runner ofrece ping, la rutina fija `read_active_view_selection` y `execute_saved_routine`. El ejecutor genérico vuelve a validar hashes, PythonNet3, modo Manual y ausencia de conectores/dependencias. Solo acepta un RVT local guardado bajo `revit/test-models/`; exige aprobación por herramienta y una bandera adicional para escrituras. El bridge compara los IDs esperados con `DocumentChanged` y promueve el manifiesto después de una ejecución verificada.

Las pruebas actuales verifican MCP stdio, hashes y transporte con un bridge falso. Se inició una sesión nueva con el RVT descartable, pero Revit no cargó el DLL y el ping devolvió timeout; la ejecución viva sigue pendiente. PythonNet3 tiene los permisos del proceso Revit, por lo que la revisión de código y el RVT descartable son obligatorios para este POC.
