# Dynamo

El equipo tiene Dynamo Core 4.0.2.3852 y PythonNet3 integrado en Revit 2027. `pythonnet3_template.dyn` contiene un único nodo PythonNet3 sin conectores; su `RunType` es `Manual`.

El MCP de rutinas crea `.dyn` desde esta plantilla y coloca el mismo código en su `.py` asociado. El generador solo guarda archivos, nunca ejecuta código. La ejecución debe hacerse manualmente desde Dynamo después de revisar el script y usar un modelo de prueba. La invocación automática MCP → Dynamo aún no está implementada.
