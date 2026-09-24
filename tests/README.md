# Validación

El registro de instalación y de pruebas está en [MCP_SMOKE_TEST.md](MCP_SMOKE_TEST.md). Primero se validan consultas MCP/Revit en un proyecto local descartable. El perfil curado permite también herramientas tipadas de creación y edición bajo aprobación por llamada; la primera escritura debe ser una operación acotada, aprobada con sus argumentos visibles y verificada después. El `.dyn` PythonNet3 se abre y ejecuta manualmente en Dynamo 4.0.2. La ejecución automática desde MCP no está habilitada ni forma parte del resultado validado.

Ejecuta las pruebas offline desde la raíz del repositorio:

```powershell
python -m unittest discover -s tests -v
```

Estas pruebas simulan `RevitServices` y comprueban la rutina de solo lectura, la correspondencia entre `.py`, `.dyn` y manifiesto, y los hashes. No cargan Dynamo ni acceden a un modelo RVT.
