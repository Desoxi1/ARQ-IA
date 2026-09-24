# MCP de rutinas Dynamo

`RoutineBuilderMcp.ps1` implementa transporte MCP stdio con Windows PowerShell 5.1 y componentes integrados de Windows. Ofrece solo:

- `list_saved_routines`: muestra nombre, alcance, efecto declarado y estado.
- `create_dynamo_routine`: escribe un script PythonNet3, un grafo `.dyn` Manual y un manifiesto bajo `routines/`.

El servidor rechaza rutas elegidas por el llamador, nombres fuera del slug permitido, scripts mayores a 32 KiB, rutinas que no asignan a `OUT`, y rutinas de escritura sin `confirm_model_write=true`. Aun así, la confirmación autoriza **guardar** código, no ejecutarlo. Este MCP no ejecuta el `.py`, no invoca Dynamo ni abre documentos RVT.

El MCP Revit se configura como servidor aparte con una lista explícita de 23 herramientas permitidas para lectura, creación y edición tipada. En conjunto, Codex puede consultar contexto de Revit, pedir aprobación para cambios tipados y generar artefactos Dynamo para ejecución manual. La llamada automática a Dynamo requiere un runner Revit separado, aún pendiente.
