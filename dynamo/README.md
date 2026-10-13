# Dynamo en ARQ-IA

ARQ-IA apunta a Dynamo for Revit 4.0.2. La plantilla `pythonnet3_template.dyn` tiene un nodo PythonNet3 en modo `Manual`.

El MCP Builder guarda una rutina como `.py`, `.dyn` y manifiesto; crearla no la ejecuta. Si el usuario quiere trabajar en el documento abierto, `prepare_saved_routine_in_dynamo` pide al bridge cargar el grafo en Manual en la sesión Dynamo vinculada al documento activo, para que el usuario lo revise. No llama al ejecutor ni guarda el RVT. Se detiene si otra gráfica contiene nodos o notas para evitar reemplazarla. Dynamo puede marcar el RVT como modificado al iniciar. La carga sin ejecución aún no está validada en la versión instalada 4.0.2.

El MCP Runner conserva una operación distinta para ejecutar rutinas: el bridge solo la acepta en un RVT local, guardado y bajo `revit/test-models/`. La preparación en el archivo activo no amplía ese permiso del ejecutor MCP. Si el usuario decide pulsar Ejecutar manualmente, PythonNet3 correrá con los permisos completos del proceso Revit; debe revisar el `.py` y el alcance antes de hacerlo.

La rutina fija de solo lectura pasó históricamente en un RVT descartable. La ejecución genérica al cambiar de grafo y las escrituras aún no tienen validación viva vigente. Por eso la existencia del Runner no significa que cualquier rutina esté probada. Consulta el [estado y la evidencia](../docs/PROTOTYPE_STATUS.md) antes de usarlo.

La interfaz de chat para Dynamo está en [`extension/`](extension/README.md); el instalador principal desde la raíz del clon es `Instalar-ARQ-IA.cmd`.
