# Conocimiento BIM progresivo

- `playbooks/`: procedimientos breves por tarea; el panel carga solo la guía pertinente, no todas las fichas en cada conversación.
- `standards/operacion-y-validacion.md`: secuencia común de lectura, alcance, prevalidación, escritura, pruebas y verificación.
- `references/REPOSITORIOS_Y_CONTENIDO.md`: registro acotado de repositorios, fuentes oficiales y referencias audiovisuales, con estado y límites de reutilización.
- `progressive-learning.md`: reglas para mantener conocimiento local sin contaminar la base compartida.
- `templates/project-memory.template.md`: formato inicial para observaciones verificadas de cada PC/proyecto.

La memoria activa permanece bajo `.arq-ia/` porque describe un modelo o preferencias de ese equipo; Git no sincroniza automáticamente esos datos.

## Guías disponibles

| Área | Guía | Cuándo usarla |
|---|---|---|
| Diseño arquitectónico | `playbooks/arquitectura-chile.md` | Concepto, implantación, distribución, contexto chileno o alternativas de diseño |
| Análisis espacial | `playbooks/ciclos-imagen.md` | Asociar colores o zonas de una imagen con niveles y elementos del modelo |
| Modelado arquitectónico | `playbooks/muros-cotas.md` | Crear o revisar muros, restricciones y cotas |
| Coordinación y estructura | `playbooks/estructura-copia.md` | Reproducir o trasladar elementos y revisar dependencias |
| Materiales y familias | `playbooks/fichas-materiales.md` | Leer fichas técnicas y mapear información al modelo |
| Diagnóstico | `playbooks/diagnostico-modelo.md` | Investigar advertencias, interferencias o anomalías |

Cada guía define alcance, datos a consultar, ambigüedades, vía de operación y comprobación posterior. Son marcos de trabajo, no entrenamiento del modelo ni garantía de ausencia de errores: se contrastan con el proyecto activo y fuentes vigentes.
