# Conocimiento BIM progresivo

- `playbooks/`: procedimientos breves por tarea; su índice separa análisis espacial, modelado, coordinación y revisión técnica. El panel carga solo la guía pertinente, no todas las fichas en cada conversación.
- `progressive-learning.md`: reglas para mantener conocimiento local sin contaminar la base compartida.
- `templates/project-memory.template.md`: formato inicial para observaciones verificadas de cada PC/proyecto.

La memoria activa permanece bajo `.arq-ia/` porque describe un modelo o preferencias de ese equipo; Git no sincroniza automáticamente esos datos.

## Guías disponibles

| Área | Guía | Cuándo usarla |
|---|---|---|
| Análisis espacial | `playbooks/ciclos-imagen.md` | Asociar colores o zonas de una imagen con niveles y elementos del modelo |
| Modelado arquitectónico | `playbooks/muros-cotas.md` | Crear o revisar muros, restricciones y cotas |
| Coordinación y estructura | `playbooks/estructura-copia.md` | Reproducir o trasladar elementos y revisar dependencias |
| Materiales y familias | `playbooks/fichas-materiales.md` | Leer fichas técnicas y mapear información al modelo |
| Diagnóstico | `playbooks/diagnostico-modelo.md` | Investigar advertencias, interferencias o anomalías |

Cada guía define alcance, datos a consultar, ambigüedades, vía de operación y comprobación posterior. No son estándares de diseño ni entrenamiento del modelo: sus reglas se contrastan con el proyecto activo y fuentes oficiales.
