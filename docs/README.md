# Índice de documentación

Usa esta página para encontrar rápidamente instalación, arquitectura, guías BIM, estado y controles. Los documentos de `docs/` describen el producto; los datos por PC y por modelo se mantienen fuera de Git.

## Empezar y operar

| Necesidad | Documento |
|---|---|
| Clonar, instalar y resolver problemas del primer inicio | [Instalar en otro PC](INSTRUCCIONES_OTRO_PC.md) |
| Ver qué funciona, qué falta y qué pruebas existen | [Estado del prototipo](PROTOTYPE_STATUS.md) |
| Entender cómo interpreta y ejecuta una solicitud | [Loop del asistente](ASSISTANT_EXECUTION_LOOP.md) |
| Traducir vocabulario Revit español/inglés | [Glosario BIM](GLOSARIO_BIM_REVIT_ES_EN.md) |
| Revisar qué capacidades están implementadas o pendientes | [Capacidades y límites](CAPACIDADES_OBJETIVO.md) |

## BIM, arquitectura y materiales

| Necesidad | Documento |
|---|---|
| Colorear muros y distinguir vista, cara y material | [Caso de muros rojos](COLOR_MUROS_PRIMER_PISO.md) |
| Interpretar catálogo y propiedades de materiales | [Materiales en Revit](MATERIALES_REVIT.md) y [catálogo común](../materials/README.md) |
| Diseño contextual, ciclos, estructura, fichas y diagnóstico | [Índice de guías BIM](../knowledge/playbooks/INDEX.md) |
| Consultar referencias BIM y normativa chilena | [Registro de fuentes](../knowledge/references/REPOSITORIOS_Y_CONTENIDO.md) |

## Arquitectura, seguridad y configuración

| Necesidad | Documento |
|---|---|
| Ver procesos, límites y decisión técnica | [Decisión de arquitectura](ARCHITECTURE_DECISION.md) |
| Confirmar versiones objetivo y compatibilidad | [Compatibilidad del entorno](ENV_AUDIT.md) |
| Revisar riesgos, privacidad y hallazgos abiertos | [Auditoría de seguridad](SECURITY_AUDIT.md) |
| Consultar configuración MCP versionada y local | [Guía de configuración](../config/README.md) y [`rvt-mcp-tools.json`](../config/rvt-mcp-tools.json) |
| Localizar restricciones que gobiernan al agente | [Reglas rápidas](../AGENTS.md), [política BIM](../governance/assistant-policy.md) e [índice de governance](../governance/README.md) |

## Qué se publica

Los documentos aquí son guías de producto, arquitectura, compatibilidad y evidencia. No incluyas perfiles de modelos, historiales, adjuntos, inventarios extraídos de RVT, rutas personales, registros de ejecución ni configuración generada por PC. Esos datos quedan en `.arq-ia/`, `.codex/` y `logs/`, ignorados por Git. `SECURITY_AUDIT.md` registra el alcance y los límites de una revisión dirigida; no es una certificación de seguridad.
