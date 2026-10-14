# Loop del asistente BIM

Este documento define cómo Codex convierte una solicitud libre en una operación Revit verificable. La ventana de conversación es Codex; los MCP locales proporcionan las herramientas. El modelo sigue bajo control del usuario.

## Ruta de una solicitud

1. **Interpretar.** Aceptar términos cotidianos y técnicos en español e inglés. Consultar el glosario y traducir el pedido a elementos, clases, parámetros y unidades de Revit.
2. **Leer el modelo.** Consultar vista, selección y los niveles, tipos, familias, relaciones o parámetros que afecten la solicitud. No inventar IDs, nombres de tipo, anfitriones ni coordenadas.
3. **Resolver el alcance.** Determinar cuántos elementos se modificarán y si el efecto es de instancia, tipo, vista o documento. Preguntar solo cuando una decisión cambie la geometría o el resultado.
4. **Planificar.** Enumerar operaciones, objetivos, valores/unidades, dependencias y comprobaciones posteriores en lenguaje entendible para el arquitecto.
5. **Autorizar.** En **Acceso controlado**, Codex solicita aprobación MCP para cada llamada de escritura; una aprobación cubre solo esa llamada y sus argumentos. En **Acceso completo**, no hay aprobación repetida por llamada: la solicitud explícita del usuario autoriza el alcance pedido dentro de esa conversación. No amplíes selección ni alcance en llamadas posteriores. La ejecución genérica de PythonNet3 requiere además `authorize_possible_model_writes=true`; ese campo declara riesgo, no reemplaza la intención del usuario.
6. **Preparar.** Preferir operaciones Revit tipadas para tareas cubiertas. Para una tarea no cubierta, guardar un grafo Dynamo PythonNet3; si el usuario pidió solo revisar el grafo, solicitar su carga en Dynamo sobre el documento activo. Si pidió ejecutarlo, invocar directamente el ejecutor activo sin preparar primero el grafo.
7. **Verificar.** Volver a consultar Revit después de cada operación o grupo pequeño. Comparar resultado, cantidades e IDs con el plan; informar errores parciales y no declarar éxito si la consulta posterior no coincide.
8. **Reutilizar.** Guardar una rutina con manifiesto y hashes. Mantenerla como `draft_not_validated` hasta probarla en un archivo descartable, revisar el resultado y registrar la versión de Revit/Dynamo.

## Enrutamiento disponible

| Necesidad | Ruta en esta versión | Efecto comprobado |
|---|---|---|
| Vista activa, selección y contexto de elementos | `rvt-mcp` con herramientas `query` | Solo lectura; comprobado en la sesión Revit abierta. |
| Crear o editar operaciones tipadas habilitadas | `rvt-mcp`; aprobación por llamada en Acceso controlado o petición explícita dentro del alcance en Acceso completo | El inventario/esquemas está revisado; cada operación de escritura aún requiere un modelo descartable y verificación real. |
| Crear y conservar una rutina | MCP `bim_ai_routines` | Guarda fuente `.py`, grafo `.dyn` Manual, wrapper JSON y manifiesto con hashes bajo `routines/`. |
| Preparar una rutina en el documento activo | `prepare_saved_routine_in_dynamo` del MCP `bim_ai_dynamo_runner` + add-in local | Solicita cargar el `.dyn` Manual asociado al documento activo para revisión del usuario; no invoca el ejecutor MCP ni guarda el RVT. No reemplaza un workspace con nodos/notas. El comportamiento de no ejecución al abrir aún no se ha validado en Dynamo 4.0.2 instalado. |
| Ejecutar una rutina personalizada en copia de prueba | `execute_saved_routine` + bridge | Limitado a RVT local bajo `revit/test-models/` y exige `authorize_possible_model_writes=true`. La ejecución genérica viva sigue sin validarse. |
| Ejecutar una rutina en el RVT abierto | `execute_saved_routine_in_active_model` + bridge | Requiere petición expresa, script revisado sin cargar, Dynamo con workspace vacío o la misma rutina guardada sin cambios pendientes y diálogo Sí/No en Revit antes de cargar y ejecutar, incluso con Acceso completo. No guarda ni sincroniza el RVT. La ejecución viva sigue sin validarse. |

## Límites que no debe ocultar el asistente

- “Cualquier pedido” significa que Codex interpreta solicitudes y compone operaciones disponibles; no implica que toda API o herramienta exista ni que el sistema pueda adivinar decisiones de diseño.
- Una rutina PythonNet3 es código ejecutable con los permisos del proceso Dynamo/Revit. La protección del documento no es un sandbox de archivos o red; revisa el código antes de ejecutarlo.
- Preparar un grafo en el documento activo solo carga la rutina para que el usuario la revise. No afirmes que se probó “abrir sin ejecutar” hasta comprobarlo en la versión instalada. El usuario puede ejecutarlo manualmente desde Dynamo o pedirlo a ARQ-IA; este último camino exige un Sí/No en Revit. Abrir Dynamo puede dejar el RVT marcado como modificado.
- Si el runner devuelve `revit_execution_timeout_unknown`, consulta el modelo antes de reintentar porque Revit pudo haber terminado la rutina.
- Una rutina guardada no constituye aprendizaje automático. Se vuelve reutilizable por versión y revisión explícita; no se incorpora sola a estándares de oficina.
- El asistente debe detener la ejecución ante un elemento ambiguo, una referencia no válida, una unidad desconocida o una diferencia entre el resultado y el plan.

## Criterio de aceptación del loop

| Etapa | Evidencia mínima |
|---|---|
| Interpretación | La solicitud cotidiana y su equivalente Revit apuntan al mismo alcance. |
| Contexto | MCP responde vista activa y selección sin exponer datos del modelo en logs. |
| Plan | Objetivos, valores, unidades y efectos son revisables antes de la escritura. |
| Autorización | En Acceso controlado, Codex pide aprobación por llamada de escritura. En Acceso completo, la petición explícita del usuario debe cubrir la acción y el alcance. |
| Ejecución | La operación ocurre en una copia descartable identificada. |
| Verificación | Consultas posteriores coinciden con el plan; resultado o error queda informado. |
| Reutilización | Rutina revisada con hash, versión y prueba en RVT descartable; se repite sin GPT. |
