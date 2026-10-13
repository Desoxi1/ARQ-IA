# Rutinas Dynamo

El MCP crea tres archivos por rutina: `<nombre>.py`, `<nombre>.dyn` y `<nombre>.manifest.json`. El manifiesto registra alcance, si declara escritura, versiones, hashes y estado `draft_not_validated`.

El código queda guardado, no ejecutado. Revisa el Python, el manifiesto y los nodos del grafo. Ejecuta solo desde Dynamo sobre un modelo de prueba.

- `read_active_view_selection`: consulta la vista y selección activas. Tiene pruebas offline con dobles de Revit. El bridge solo la ejecuta si Dynamo UI ya está abierto y ligado al mismo RVT activo; no arranca Dynamo en modo invisible sobre el modelo.
- `read_test_model_summary`: rutina genérica de solo lectura para validar título, vista activa, selección y cantidad de elementos en el RVT de laboratorio.
- `set_comments_on_selection`: borrador de escritura que asigna `PRUEBA_IA` al parámetro integrado Comments de los elementos seleccionados. Omite parámetros ausentes o de solo lectura y devuelve IDs actualizados, sin cambios y omitidos. Las pruebas usan dobles de Revit; **no se ha ejecutado en un modelo**. Solo probar en una copia desechable.

El Builder MCP puede guardar código PythonNet3 nuevo como `.py`, `.dyn` Manual y manifiesto con hashes. `prepare_saved_routine_in_dynamo` puede solicitar cargar el grafo en la sesión Dynamo ligada al documento activo, para que el usuario lo inspeccione; no ejecuta ni guarda el RVT. No reemplaza un workspace que tenga nodos/notas. Aún falta validar en Dynamo 4.0.2 que abrirlo con las claves Manual/no-run no ejecute un nodo.

`execute_saved_routine` valida el paquete y lo manda al bridge; el bridge solo lo ejecuta con Dynamo UI abierto y ligado al mismo RVT guardado bajo `revit/test-models/`. Toda ejecución genérica requiere `authorize_possible_model_writes=true`, aunque el manifiesto diga solo lectura, porque el código corre con los permisos completos del proceso Revit. En Acceso controlado Codex pide aprobación MCP por llamada; en Acceso completo la solicitud explícita del usuario debe cubrir la acción y el alcance. La bandera declara riesgo, pero no sustituye esa autorización. El bridge verifica los cambios mediante `DocumentChanged`. Si el usuario decide manualmente pulsar Ejecutar sobre un documento de trabajo después de revisar el código, esa acción queda fuera de la verificación del runner MCP.

Las pruebas automáticas offline validan la lógica simulada, la estructura de los grafos y sus hashes; no sustituyen la ejecución real dentro de Revit/Dynamo. Revisa el Python antes de ejecutarlo: PythonNet3 corre con todos los permisos del proceso Revit y el límite de ruta protege el documento activo, no los archivos ni la red.
