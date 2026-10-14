# Mapa de carpetas y archivos de ARQ-IA

Este mapa explica qué necesita el producto, qué sirve para desarrollarlo y qué se crea localmente en cada PC. Para instalar desde un clon, los únicos pasos normales son abrir `Instalar-ARQ-IA.cmd` y seguir el asistente. No copies datos de `.arq-ia/`, `.codex/`, `logs/`, `artifacts/` ni `vendor/` a otro equipo.

## Vista rápida

```text
ARQ-IA/
├── AGENTS.md              entrada breve de instrucciones
├── governance/            reglas completas del asistente y seguridad
├── config/                lista versionada de herramientas MCP permitidas
├── knowledge/             guías BIM y aprendizaje local progresivo
├── materials/             catálogo común y taxonomía
├── docs/                  manuales, arquitectura y evidencia
├── revit/                 panel Revit, bridge y espacio de modelos descartables
├── dynamo/                extensión Dynamo y plantilla PythonNet3
├── mcp/                   servidores MCP propios de ARQ-IA
├── routines/              rutinas revisadas y ejemplos
├── tests/                 verificaciones automatizadas y sus informes
└── install/setup/doctor   instalación, configuración y diagnóstico
```

## Carpetas del producto

| Ruta | Qué contiene | Quién la necesita |
|---|---|---|
| `governance/` | Política de contexto BIM, datos, aprobaciones, escritura y verificación | Codex y quienes mantienen el producto |
| `config/` | `rvt-mcp-tools.json`, fuente revisable de las herramientas habilitadas | Instalador y mantenedores; `.codex/config.toml` se genera localmente |
| `knowledge/playbooks/` | Cinco guías breves por intención; consulta `INDEX.md` | ARQ-IA recupera una guía pertinente por solicitud |
| `knowledge/progressive-learning.md` | Qué puede recordar localmente, cómo verificarlo y qué no promover | Cada clon/equipo |
| `knowledge/templates/` | Plantilla para notas de aprendizaje, no memoria de un proyecto real | Cada clon/equipo al crear su memoria local |
| `materials/` | `base.csv`, `TAXONOMIA.md`, instrucciones y plantilla de preferencias | Arquitectos y consultas del asistente |
| `revit/assistant/` | Add-in de chat para Revit, compilación e instalación | Usuarios y mantenedores |
| `revit/bridge/` | Bridge tipado entre Revit y Dynamo para operaciones acotadas | Add-ins; no se ejecuta directamente desde una conversación normal |
| `revit/test-models/` | README y preparador; los RVT de prueba son locales y se excluyen de Git | Mantenedores al validar integraciones |
| `dynamo/extension/` | Panel de chat para Dynamo, paquete, instalador y DLL publicado | Arquitectos y mantenedores |
| `dynamo/pythonnet3_template.dyn` | Grafo base PythonNet3 en modo manual | Arquitectos al revisar o iniciar rutinas |
| `mcp/` | MCP para listar/crear rutinas y comunicarse con el runner | Configuración local de Codex |
| `routines/` | Rutinas comunes revisadas y ejemplos versionados; los borradores nuevos se mantienen locales hasta revisarlos | Arquitectos y mantenedores |
| `tests/` | Pruebas del cliente, configuración, bridge, add-ins y rutinas | Mantenedores; no son un instalador |

## Archivos de entrada y scripts

| Archivo | Función | ¿Ejecutarlo normalmente? |
|---|---|---|
| `Instalar-ARQ-IA.cmd` | Entrada recomendada para instalar y ejecutar el diagnóstico final | Sí, una vez por equipo o para reparar |
| `install.ps1` | Coordina instalaciones de Revit, Dynamo y componentes | Lo llama el `.cmd`; también puede ejecutarse manualmente |
| `setup.ps1` | Descarga/verifica `rvt-mcp` cuando falta y genera configuración MCP local | Lo coordina el instalador; permite configurar el clon |
| `doctor.ps1` | Comprueba instalación, rutas y componentes, sin editar el RVT | Sí, para diagnosticar |
| `package.ps1` | Empaqueta desde un commit limpio y revisa archivos requeridos, hashes y patrones sensibles | Solo al preparar una distribución ZIP |
| `build.ps1` dentro de add-ins | Compila DLLs desde el código fuente | Solo desarrollo; el arquitecto instala los binarios incluidos |
| `*install.ps1` dentro de `revit/` o `dynamo/` | Instala o repara un componente individual | Solo reparación dirigida; el `.cmd` es el camino normal |

`setup.ps1` no es un agente ni una configuración universal: genera rutas absolutas propias del PC. `doctor.ps1` no repara ni instala. `package.ps1` crea una distribución; no se requiere para clonar e instalar.

## Datos locales que no deben publicarse

| Ruta | Uso | Estado en Git |
|---|---|---|
| `.codex/` | Configuración MCP generada para esta ruta y PC | Ignorada; regenerar con el instalador |
| `.arq-ia/` | Perfil del RVT abierto, preferencias de materiales e historial/memoria local | Ignorada; no copiar entre proyectos sin revisar privacidad |
| `logs/` | Adjuntos temporales, respuestas y registros de ejecución | Ignorados salvo su README de orientación |
| `artifacts/` | ZIP, DLL intermedias y archivos de build/diagnóstico | Ignorada; se puede regenerar |
| `vendor/` | Caché del instalador de terceros verificado por hash | Ignorada salvo su README; descargable otra vez |
| `revit/test-models/*.rvt` | Modelos descartables de integración | Ignorados; no incluir modelos de trabajo |
| `.desoxi/` | Scripts y diseños para mostrar la marca DESOXI en terminales | Excluida; no pertenece al producto ARQ-IA ni a su instalador |

Que una ruta esté ignorada significa que Git no la publica. No significa que sea necesaria para instalar ni que deba borrarse automáticamente: revisa su origen y si contiene datos locales antes de limpiar el equipo.

## Documentación: dónde buscar

| Necesitas… | Lee… |
|---|---|
| Clonar e instalar en un PC nuevo | `README.md` y `INSTRUCCIONES_OTRO_PC.md` |
| Ver qué está probado y qué falta | `PROTOTYPE_STATUS.md` y `MCP_SMOKE_TEST.md` |
| Entender el diseño técnico | `ARCHITECTURE_DECISION.md` y `ASSISTANT_EXECUTION_LOOP.md` |
| Buscar términos Revit en español/inglés | `GLOSARIO_BIM_REVIT_ES_EN.md` |
| Revisar herramientas BIM objetivo | `CAPACIDADES_OBJETIVO.md` |
| Resolver el caso de muros rojos | `COLOR_MUROS_PRIMER_PISO.md` |
| Consultar materiales y su alcance | `MATERIALES_REVIT.md` y `../materials/TAXONOMIA.md` |
| Revisar seguridad | `SECURITY_AUDIT.md` y `ENV_AUDIT.md` |

El punto de partida para las guías operativas es [`knowledge/playbooks/INDEX.md`](../knowledge/playbooks/INDEX.md). El catálogo de materiales se navega con la columna `catalog_group` de [`materials/base.csv`](../materials/base.csv); el significado y cantidad de cada grupo está en [`materials/TAXONOMIA.md`](../materials/TAXONOMIA.md).
