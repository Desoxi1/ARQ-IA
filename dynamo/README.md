# Dynamo en ARQ-IA

ARQ-IA apunta a Dynamo for Revit 4.0.2. La plantilla `pythonnet3_template.dyn` tiene un nodo PythonNet3 en modo `Manual`.

El MCP Builder guarda una rutina como `.py`, `.dyn` y manifiesto; crearla no la ejecuta. El MCP Runner puede enviar una rutina guardada al bridge de Revit, que la ejecuta en Dynamo cuando la interfaz está abierta y vinculada al mismo RVT local, guardado y ubicado bajo `revit/test-models/`. El bridge no inicia Dynamo oculto ni ejecuta una rutina al guardarla.

La rutina fija de solo lectura pasó históricamente en un RVT descartable. La ejecución genérica al cambiar de grafo y las escrituras aún no tienen validación viva vigente. Por eso la existencia del Runner no significa que cualquier rutina esté probada. Consulta el [estado y la evidencia](../docs/PROTOTYPE_STATUS.md) antes de usarlo.

La interfaz de chat para Dynamo está en [`extension/`](extension/README.md); el instalador principal desde la raíz del clon es `Instalar-ARQ-IA.cmd`.
