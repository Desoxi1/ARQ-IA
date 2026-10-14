# Aprendizaje local progresivo

ARQ-IA no entrena automáticamente ni cambia el modelo de Codex. Aprende a nivel de contexto: lee la ficha local del modelo, las preferencias del equipo y correcciones confirmadas. La fuente viva para el estado actual siempre es Revit.

## Qué conservar

- `.arq-ia/model-profile.md`: resumen compacto del documento abierto (fecha/documento, unidades, niveles, fases y conteos acotados); sin IDs, geometría ni dump de elementos.
- `.arq-ia/project-memory.md`: convenciones verificadas, decisión del usuario, límite conocido y cuándo volver a preguntar. Registrar `documento/versión`, `fecha`, `alcance`, `fuente`, `observación` y `condición de reutilización`.
- `.arq-ia/materials/local.csv`: preferencias locales explícitas con el esquema `materials/local-materials.template.csv`.
- `.arq-ia/conversations/`: historial privado de la interfaz, que no es memoria validada.

## Cuándo promover una observación

1. Confirmarla con el usuario o leer el resultado final desde Revit.
2. Distinguir hecho del modelo, preferencia de la oficina y regla general de Revit. Solo la preferencia confirmada se reutiliza como preferencia.
3. Añadir una nota breve con fuente/fecha y condición de vigencia. Si falla una operación o la evidencia es ambigua, registrar el bloqueo como tal; no transformarlo en una regla.
4. Al cambiar documento, versión, fase o estándar, consultar de nuevo. Nunca trasladar el perfil del RVT o la memoria personal a Git sin una revisión explícita que elimine datos identificables.

## Ahorro de contexto

El panel carga como máximo una ficha de tarea y envía únicamente adjuntos que el usuario eligió, en resolución alta cuando hay lectura de planos/ciclos o ficha técnica. Consulta categorías, IDs y propiedades pertinentes; resume resultados grandes. Mantén las notas locales cortas y no pegues conversación completa como instrucciones.
