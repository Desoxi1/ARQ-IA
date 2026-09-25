# Rutinas Dynamo

El MCP crea tres archivos por rutina: `<nombre>.py`, `<nombre>.dyn` y `<nombre>.manifest.json`. El manifiesto registra alcance, si declara escritura, versiones, hashes y estado `draft_not_validated`.

El código queda guardado, no ejecutado. Revisa el Python, el manifiesto y los nodos del grafo. Ejecuta solo desde Dynamo sobre un modelo de prueba.

- `read_active_view_selection`: consulta la vista y selección activas. Tiene pruebas offline con dobles de Revit; falta ejecutar el `.dyn` dentro de Dynamo.
- `set_comments_on_selection`: borrador de escritura que asigna `PRUEBA_IA` al parámetro integrado Comments de los elementos seleccionados. Omite parámetros ausentes o de solo lectura y devuelve IDs actualizados, sin cambios y omitidos. Las pruebas usan dobles de Revit; **no se ha ejecutado en un modelo**. Solo probar en una copia desechable.

Las pruebas automáticas offline validan la lógica simulada, la estructura de los grafos y sus hashes; no sustituyen la ejecución real dentro de Revit/Dynamo. El runner MCP solo admite actualmente `read_active_view_selection`; la ejecución por identificador de rutinas nuevas sigue pendiente.
