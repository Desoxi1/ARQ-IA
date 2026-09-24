# Rutinas Dynamo

El MCP crea tres archivos por rutina: `<nombre>.py`, `<nombre>.dyn` y `<nombre>.manifest.json`. El manifiesto registra alcance, si declara escritura, versiones, hashes y estado `draft_not_validated`.

El código queda guardado, no ejecutado. Revisa el Python, el manifiesto y los nodos del grafo. Ejecuta solo desde Dynamo sobre un modelo de prueba. La rutina `read_active_view_selection` solo consulta la vista y selección activas. Las pruebas automáticas offline validan su salida con dobles de Revit, la estructura del grafo y sus hashes; no sustituyen la ejecución visual real en Dynamo. La ejecución por identificador desde MCP está pendiente.
