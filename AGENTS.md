# Guía de trabajo del asistente BIM

## Interpretación del pedido

- Entiende sinónimos cotidianos y términos BIM/Revit en español usando `docs/GLOSARIO_BIM_REVIT_ES_EN.md` y `docs/CAPACIDADES_OBJETIVO.md`.
- Si faltan datos que cambian el resultado —nivel, tipo, ubicación, unidades, elementos objetivo, valor de parámetro— consulta primero Revit con las herramientas de lectura y pregunta lo mínimo necesario.
- Antes de una acción, resume en lenguaje simple qué cambiará, qué elementos toca y cómo se comprobará.

## Elegir la forma de operar

1. Usa primero las consultas `rvt-mcp` para identificar modelo, vista, selección, niveles, tipos, familias y parámetros existentes.
2. Para tareas cubiertas por una herramienta Revit tipada, úsala con aprobación por llamada. Haz cambios unitarios y verifica con una consulta posterior.
3. Para una tarea que requiere lógica propia, guarda una rutina con `create_dynamo_routine`. Usa `PythonNet3`, `RunType=Manual`, resultados JSON y valores de `affected_ids` exactos para escrituras.
4. Ejecuta una rutina genérica solo con `execute_saved_routine`, en un RVT local, guardado y no workshared dentro de `revit/test-models/`. Toda ejecución requiere aprobación de esa llamada y `authorize_possible_model_writes=true`, aunque el manifiesto diga solo lectura; revisa el `.py` y explica que autoriza posibles cambios al modelo.
5. Después de una ejecución correcta, consulta el modelo otra vez y compara el resultado con el pedido. Conserva la rutina para repetirla sin regenerar código.

## Límites de ejecución

- Nunca ejecutes rutinas generadas sobre modelos productivos. El bridge genérico debe rechazarlos.
- No asumas que la aprobación de guardar una rutina también aprueba ejecutarla; la ejecución requiere otra aprobación.
- Revisa el `.py` antes de ejecutarlo. PythonNet3 se ejecuta con los permisos del proceso Revit; la protección del documento no sandboxea archivos, red ni el resto del sistema.
- No habilites `revit_send_code_to_revit`, ToolBaker, borrado ni ejecución libre como atajos.
- Si el runner devuelve `revit_execution_timeout_unknown`, vuelve a consultar el modelo antes de reintentar: Revit pudo haber terminado la rutina aunque el cliente dejó de esperar.
- No declares una rutina validada si el runner no confirma cambios de documento y el resultado esperado.
- Si falla la verificación posterior, informa que pudo haber cambios parciales; no digas que Revit hizo rollback automáticamente.
