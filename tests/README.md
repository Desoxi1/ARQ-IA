# Validación

El registro de instalación y de pruebas está en [MCP_SMOKE_TEST.md](MCP_SMOKE_TEST.md). Primero se validan consultas MCP/Revit en un proyecto local descartable. El perfil curado permite también herramientas tipadas de creación y edición bajo aprobación por llamada; la primera escritura debe ser una operación acotada, aprobada con sus argumentos visibles y verificada después. El runner add-in solo intenta ejecutar la rutina fija de lectura; su ejecución en Revit todavía está pendiente.

Ejecuta las pruebas offline desde la raíz del repositorio:

```powershell
python -m unittest discover -s tests -v
```

Estas pruebas simulan `RevitServices` y comprueban la rutina de solo lectura, la correspondencia entre `.py`, `.dyn` y manifiesto, y los hashes. No cargan Dynamo ni acceden a un modelo RVT.

Para comprobar la conexión viva MCP → Revit sin modificar el documento activo, abre Revit y ejecuta:

```powershell
python tests/run_revit_mcp_readonly_smoke.py
```

El smoke test solo consulta la vista activa y la selección. No imprime nombres, parámetros ni identificadores del modelo. Requiere el servidor `rvt-mcp` v0.6.2 instalado y una sesión abierta de Revit.

Para probar la generación MCP de una rutina temporal, sin abrir Revit ni ejecutar código Dynamo:

```powershell
python tests/run_routine_builder_mcp_smoke.py
```

Este smoke crea un grafo de solo lectura con nombre aleatorio, verifica el manifiesto y los hashes, y elimina los tres archivos que generó.

Con el puente instalado, Revit abierto y un documento guardado/sin cambios pendientes, la prueba viva ejecuta únicamente la rutina de lectura con hashes fijados:

```powershell
python tests/run_dynamo_runner_mcp_smoke.py --live
```

No prueba escrituras ni habilita otras rutinas.
Si el bridge no está cargado, el smoke informa `bridge_connect_timeout` y termina sin llamar a la rutina.

Para probar solo el transporte MCP ↔ named pipe sin abrir Revit:

```powershell
python tests/run_dynamo_runner_pipe_smoke.py
```

Este test crea un pipe de nombre aleatorio y un fake bridge temporal. Comprueba mensajes, el timeout cuando no hay servidor y el rechazo/éxito del cliente, no la ejecución de Dynamo en Revit.
