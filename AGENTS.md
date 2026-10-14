# ARQ-IA: instrucciones del proyecto

ARQ-IA es independiente de NEXUS DX. Antes de cambiar código, configuración, materiales o rutinas, lee [la política y reglas del asistente](governance/assistant-policy.md) y el estado vigente en `docs/PROTOTYPE_STATUS.md`.

## Mapa rápido

- `governance/`: restricciones, reglas de alcance y seguridad del asistente.
- `config/`: fuentes versionadas de configuración por defecto; `.codex/` contiene configuración generada local y no se sube a Git.
- `knowledge/`: guías BIM recuperadas según la tarea y reglas para memoria progresiva local.
- `materials/`: catálogo común con categorías, plantilla local y límites de uso.
- `docs/`: arquitectura, instalación, estado, referencias técnicas e instrucciones para arquitectos.
- `revit/`, `dynamo/`, `mcp/`: add-ins, bridge y servidores MCP.
- `routines/`: rutinas compartidas revisadas; borradores personales se guardan fuera de Git.

Usa una guía de `knowledge/playbooks/` por solicitud cuando corresponda. Consulta el modelo activo antes de inferir niveles, tipos, materiales o IDs. No declares éxito hasta leer el resultado en Revit. La memoria de cada clon vive bajo `.arq-ia/` y nunca se copia automáticamente al repositorio.
