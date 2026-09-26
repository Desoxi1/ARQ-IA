# Loop del asistente BIM

Este documento define cómo Codex convierte una solicitud libre en una operación Revit verificable. La ventana de conversación es Codex; los MCP locales proporcionan las herramientas. El modelo sigue bajo control del usuario.

## Ruta de una solicitud

1. **Interpretar.** Aceptar términos cotidianos y técnicos en español e inglés. Consultar el glosario y traducir el pedido a elementos, clases, parámetros y unidades de Revit.
2. **Leer el modelo.** Consultar vista, selección y los niveles, tipos, familias, relaciones o parámetros que afecten la solicitud. No inventar IDs, nombres de tipo, anfitriones ni coordenadas.
3. **Resolver el alcance.** Determinar cuántos elementos se modificarán y si el efecto es de instancia, tipo, vista o documento. Preguntar solo cuando una decisión cambie la geometría o el resultado.
4. **Planificar.** Enumerar operaciones, objetivos, valores/unidades, dependencias y comprobaciones posteriores en lenguaje entendible para el arquitecto.
5. **Aprobar.** Mantener el modo de aprobación MCP `prompt`. Una aprobación cubre la llamada visible con esos argumentos; no ampliar la selección o el alcance en llamadas siguientes.
6. **Ejecutar.** Preferir operaciones Revit tipadas para tareas cubiertas. Para una tarea no cubierta, preparar un grafo Dynamo PythonNet3 y explicar qué código se guardó y dónde.
7. **Verificar.** Volver a consultar Revit después de cada operación o grupo pequeño. Comparar resultado, cantidades e IDs con el plan; informar errores parciales y no declarar éxito si la consulta posterior no coincide.
8. **Reutilizar.** Guardar una rutina con manifiesto y hashes. Mantenerla como `draft_not_validated` hasta probarla en un archivo descartable, revisar el resultado y registrar la versión de Revit/Dynamo.

## Enrutamiento disponible

| Necesidad | Ruta en esta versión | Efecto comprobado |
|---|---|---|
| Vista activa, selección y contexto de elementos | `rvt-mcp` con herramientas `query` | Solo lectura; comprobado en la sesión Revit abierta. |
| Crear o editar operaciones tipadas habilitadas | `rvt-mcp` con aprobación `prompt` | El inventario/esquemas está revisado; cada operación de escritura aún requiere un modelo descartable y verificación real. |
| Crear y conservar una rutina | MCP `bim_ai_routines` | Guarda fuente `.py`, grafo `.dyn` Manual, wrapper JSON y manifiesto con hashes bajo `routines/`. |
| Ejecutar una rutina personalizada | MCP `bim_ai_dynamo_runner` + add-in local | `execute_saved_routine` valida el paquete y opera solo en un RVT local bajo `revit/test-models/`; toda ejecución genérica requiere aprobación y `authorize_possible_model_writes=true`, y las escrituras declaradas se contrastan con `affected_ids`. El protocolo fake pasa; falta la ejecución viva. |

## Límites que no debe ocultar el asistente

- “Cualquier pedido” significa que Codex interpreta solicitudes y compone operaciones disponibles; no implica que toda API o herramienta exista ni que el sistema pueda adivinar decisiones de diseño.
- Una rutina PythonNet3 es código ejecutable con los permisos del proceso Dynamo/Revit. La protección del documento no es un sandbox de archivos o red; revisa el código antes de aprobarlo.
- Si el runner devuelve `revit_execution_timeout_unknown`, consulta el modelo antes de reintentar porque Revit pudo haber terminado la rutina.
- Una rutina guardada no constituye aprendizaje automático. Se vuelve reutilizable por versión y revisión explícita; no se incorpora sola a estándares de oficina.
- El asistente debe detener la ejecución ante un elemento ambiguo, una referencia no válida, una unidad desconocida o una diferencia entre el resultado y el plan.

## Criterio de aceptación del loop

| Etapa | Evidencia mínima |
|---|---|
| Interpretación | La solicitud cotidiana y su equivalente Revit apuntan al mismo alcance. |
| Contexto | MCP responde vista activa y selección sin exponer datos del modelo en logs. |
| Plan | Objetivos, valores, unidades y efectos son revisables antes de la escritura. |
| Aprobación | Codex pide aprobación para cada llamada que pueda cambiar el modelo. |
| Ejecución | La operación ocurre en una copia descartable identificada. |
| Verificación | Consultas posteriores coinciden con el plan; resultado o error queda informado. |
| Reutilización | Rutina revisada con hash, versión y prueba en RVT descartable; se repite sin GPT. |
