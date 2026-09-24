# Instrucciones del asistente BIM para este POC

## Entender el pedido

- Interpreta español cotidiano y términos técnicos en ambos sentidos con el glosario de `docs/GLOSARIO_BIM_REVIT_ES_EN.md`.
- Antes de una escritura, consulta en el modelo activo IDs, tipos, niveles, parámetros, anfitriones y unidades pertinentes. Usa los datos del proyecto; no inventes IDs, nombres de tipos ni coordenadas.
- Pregunta cuando falten datos que cambian geometría, ubicación, selección, tipo, anfitrión, nivel, alcance de instancia/tipo o resultado esperado.
- Explica el plan en lenguaje simple y deja los nombres técnicos y unidades visibles para que el arquitecto pueda revisarlos.

## Cambios en Revit

- Este POC trabaja en un modelo temporal o descartable hasta que el equipo valide el flujo.
- Mantén la aprobación MCP en `prompt`. Antes de cada llamada que cree o modifique, explica los elementos objetivo, valores/unidades, resultado esperado y posible efecto. Espera la aprobación que Codex muestre para esa llamada.
- Empieza con operaciones acotadas a IDs o selección explícita. No amplíes una solicitud a todo el proyecto sin que el usuario lo haya pedido claramente.
- No borres elementos, no guardes/sobrescribas modelos y no exportes archivos desde herramientas MCP en este perfil.
- No llames herramientas de evaluación C#, Python arbitrario, `send_code`, ToolBaker o herramientas fuera de la lista habilitada.
- Tras una escritura aprobada, consulta de nuevo el modelo y reporta IDs creados/afectados y diferencias observadas. Si la verificación falla o difiere del plan, detente y explícalo.

## Dynamo y rutinas

- `create_dynamo_routine` guarda código `.py`, grafo `.dyn` y manifiesto; no los ejecuta.
- Muestra la ruta y resume el alcance de la rutina. Pide revisar el código y probar en modelo descartable antes de usarla en un proyecto real.
- Dynamo usa milímetros en las herramientas que así lo declaran; confirma unidades antes de convertir o calcular.
- La biblioteca común incorpora una rutina solo después de revisión y prueba documentadas.
