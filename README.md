# ARQ-IA

**Repositorio público:** [Desoxi1/ARQ-IA](https://github.com/Desoxi1/ARQ-IA) · Clonar: `git clone https://github.com/Desoxi1/ARQ-IA.git`

**Asistente para Revit 2027 y Dynamo 4.0.2.** Desarrollado por Branko Plaza. Abre un chat en Revit y en **Dynamo → Extensiones → Abrir ARQ-IA**, acepta texto e imágenes/archivos, conserva el historial local por carpeta de proyecto y usa Codex para interpretar solicitudes BIM.

## Cómo funciona

```text
Arquitecto → panel ARQ-IA → Codex → MCP rvt-mcp → API de Revit
                                  └→ MCP de rutinas → grafo Dynamo/PythonNet3 → bridge Revit
```

Un **MCP** ofrece herramientas concretas a Codex; no es otro modelo entrenado. `rvt-mcp` proporciona consultas y operaciones Revit tipadas. Los MCP propios guardan scripts `.py`, grafos `.dyn` y manifiestos en `routines/`. Cuando el usuario pide revisar una rutina en el archivo abierto, `prepare_saved_routine_in_dynamo` solicita cargar el grafo Manual en la sesión Dynamo asociada al documento activo; no llama al ejecutor ni guarda el RVT. Si la sesión contiene otro grafo con nodos/notas o está ligada a otro documento, se detiene para no reemplazarlo. El usuario revisa y decide si pulsa Ejecutar. El ejecutor MCP genérico sigue limitado a un RVT local descartable bajo `revit/test-models/`. La carga sin ejecución todavía debe verificarse con Dynamo 4.0.2 instalado; la rutina fija de solo lectura tiene una prueba viva histórica y la ejecución genérica actual no está validada. El chat usa la sesión local de Codex y no guarda una API key.

Para ahorrar tokens, ARQ-IA consulta solo la vista, los elementos y parámetros necesarios. Prefiere una herramienta Revit existente antes de generar un grafo. Mantiene rutinas revisadas en disco y responde con resúmenes e IDs, sin enviar el modelo entero en cada solicitud.

### Ejemplo: “pinta de rojo los muros del primer piso”

En Revit puede significar **sobrescritura gráfica en una vista**, **pintura de caras** o **cambio de material del elemento/tipo**. El asistente debe consultar el nivel real y los muros, y preguntar cuál de esos efectos se busca si sigue ambiguo. Para color de presentación usa `revit_override_element_graphics` con los IDs encontrados y `#FF0000`; comprueba la vista y los elementos después. No hace falta Dynamo para esa operación. [Detalle técnico](docs/COLOR_MUROS_PRIMER_PISO.md).

## Instalar desde un clon

Requiere Windows x64, Revit **2027**, Dynamo incluido con Revit, Codex Desktop/CLI instalado y sesión iniciada, y acceso a GitHub para descargar `rvt-mcp` en la primera instalación. No requiere .NET SDK en el PC del arquitecto.

1. Clona el repositorio en cualquier carpeta; no hay rutas personales dentro del código distribuido.
2. Cierra Revit y haz doble clic en **`Instalar-ARQ-IA.cmd`**.
3. Abre esa carpeta en Codex y confía en el proyecto cuando lo solicite para activar los MCP locales.
4. Abre Revit 2027 y Dynamo. Busca **ARQ-IA** en la cinta de Revit y en **Extensiones** de Dynamo.
5. Pide primero: “¿Qué vista está activa y qué elementos tengo seleccionados? No cambies el modelo”.

Alternativa PowerShell: `powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1`.

El instalador detecta rutas del equipo, instala los dos paneles y el bridge por usuario, registra los MCP y comprueba los componentes. Descarga el release **v1.1.0** de `bimwright/rvt-mcp` con SHA-256 fijo solo si no existe una instalación compatible. En este PC de desarrollo conserva la instalación anterior v0.6.2; el perfil habilita la misma herramienta gráfica ya presente. El instalador no actualiza un `rvt-mcp` existente automáticamente. [Pasos y diagnóstico](docs/INSTRUCCIONES_OTRO_PC.md).

## Estado real

La versión declarada por el paquete Dynamo es **0.5.1**. El producto incluye paneles para Revit y Dynamo, adjuntos, historial local, selección de modelo Codex y MCP para contexto Revit, rutinas y bridge.

- **Evidencia histórica:** consultas de vista y selección por `rvt-mcp` v0.6.2; carga de una versión anterior de la extensión Dynamo; intercambio Codex app-server con texto/imagen; y una rutina fija de lectura ejecutada en un RVT descartable. El listado de materiales de un modelo de muestra devolvió 300/300; `materials/base.csv` conserva 208 nombres curados, no el inventario del RVT.
- **Pendiente:** validar una instalación limpia en otro PC, `rvt-mcp` v1.1.0 integrado, ejecución genérica de rutinas después de cambiar de grafo, escritura y el caso de muros rojos. Compilar o instalar un archivo no sustituye esas comprobaciones.
- **Límite del producto:** ARQ-IA interpreta solicitudes y usa las herramientas disponibles; no garantiza cualquier operación ni modifica proyectos productivos mediante el runner genérico.

La extensión visible en la sesión de desarrollo no demuestra que una instalación limpia en otro equipo vaya a funcionar igual. Consulta [el estado y la evidencia](docs/PROTOTYPE_STATUS.md) antes de considerar una capacidad validada.

El chat ofrece **Acceso controlado** y **Acceso completo** por conversación. Acceso completo pide una confirmación al activarlo y evita las aprobaciones repetidas de Codex para esa conversación; al abrir o crear otra, vuelve a Acceso controlado. Codex puede usar archivos, comandos y red con permisos amplios en el PC. Los scripts PythonNet3 se ejecutan con los permisos de Revit y el runner limita qué RVT activo acepta, pero esa restricción **no** aísla el código del resto del sistema. Revisa la rutina antes de ejecutarla y usa solo un RVT descartable. [Estado y evidencia](docs/PROTOTYPE_STATUS.md).

## Carpetas y archivos necesarios

| Ruta | Función |
|---|---|
| `Instalar-ARQ-IA.cmd` | Punto de entrada: ejecuta la instalación y el diagnóstico. |
| `install.ps1` | Orquesta la instalación de Revit, Dynamo, bridge y MCP. |
| `setup.ps1` | Obtiene/verifica `rvt-mcp` y genera la configuración Codex local para este clon. |
| `doctor.ps1` | Comprueba componentes, rutas y conexión sin instalar ni modificar el modelo. |
| `package.ps1` | Crea un ZIP portable desde un commit Git limpio y verifica los archivos/binarios requeridos. |
| `AGENTS.md` | Instrucciones de Codex para lenguaje BIM, alcance, aprobaciones y verificación. |
| `revit/assistant/`, `dynamo/extension/` | Paneles y binarios precompilados con hashes. |
| `revit/bridge/`, `mcp/` | Ejecución y herramientas MCP locales. |
| `routines/`, `dynamo/` | Rutinas guardadas, plantilla y wrapper PythonNet3. |
| `materials/` | Base común curada y plantilla para preferencias locales. |
| `docs/`, `tests/` | Arquitectura, límites, instalación y evidencia de desarrollo. |

Los scripts `build.ps1` sirven al desarrollador; no hacen falta para instalar en otro PC. `install.ps1` coordina `setup.ps1` y los instaladores de cada complemento; `doctor.ps1` diagnostica y no instala. `Instalar-ARQ-IA.cmd` es la entrada recomendada para arquitectos. `.codex/` guarda la configuración MCP específica de este clon; `.arq-ia/` guarda perfiles, preferencias e historial locales. Ambas se excluyen de Git y no deben copiarse entre PCs. Los archivos de runtime bajo `logs/`, las cachés bajo `vendor/` y los resultados bajo `artifacts/` son locales. Git conserva los README de orientación de `logs/` y `vendor/`; `artifacts/` no forma parte del clon. Modelos RVT y adjuntos no se incluyen en el repo.

Los manifiestos identifican a **Branko Plaza** como fabricante. Revit puede seguir mostrando «Fabricante desconocido» porque las DLL no tienen firma digital de un certificado de editor confiable. Para que el aviso identifique a un editor verificado se requiere esa firma. [Seguridad de complementos de Revit](https://help.autodesk.com/cloudhelp/2022/ENU/Revit-Troubleshooting/files/GUID-36D29367-A540-4CFD-9773-4995BF1DF08A.htm) · [Firma digital de complementos](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Introduction/Add_In_Integration/Digitally_Signing_Your_Revit_Add_in/Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Digitally_Signing_Your_App_html.html).

`materials/base.csv` contiene la base de referencia inicial con 208 nombres y atributos visuales, saneada sin IDs, archivos, cantidades ni takeoffs. Sirve para encontrar vocabulario y candidatos; Revit activo sigue siendo la fuente de verdad y la base no es una especificación técnica. Cada PC puede mantener preferencias propias en `.arq-ia/materials/local.csv`; todo `.arq-ia/`, perfil, inventario del modelo e historial quedan ignorados y no se incluyen en Git ni en el paquete. La base común no se modifica automáticamente.

El botón de historial `◷` abre las conversaciones guardadas en `.arq-ia/conversations/`. Al clonar el repositorio en otro PC, el historial empieza vacío y permanece en esa carpeta local; solo conserva texto, estados/resúmenes de herramientas y nombres de adjuntos, no los archivos adjuntos. Al abrir una conversación anterior y continuar, ARQ-IA entrega a Codex los últimos mensajes de texto con un límite de contexto y vuelve a consultar Revit antes de proponer cambios.

En el primer envío de cada copia local sin perfil, ARQ-IA pide a Codex una ficha concisa del proyecto abierto mediante MCP de solo lectura: documento, niveles, fases, cantidades por categoría y un resumen acotado de materiales. La ficha queda en `.arq-ia/model-profile.md`, no se sube a Git. También puede existir `.arq-ia/project-memory.md` con convenciones y correcciones locales verificadas y `.arq-ia/materials/local.csv` con preferencias de materiales explícitas y verificadas; esto conserva contexto local, pero no entrena el modelo ni comparte información entre PCs.

La carpeta `.arq-ia` contiene texto local sin cifrar; no guardes secretos allí. Los adjuntos se copian temporalmente en `logs/attachments/` y el historial conserva texto de la conversación; ambos quedan excluidos de Git. Considera lo que adjuntes como contenido que Codex puede procesar. En **Acceso controlado**, Codex puede pedir permiso para crear o actualizar el perfil y las notas; **Acceso completo** aplica los permisos elegidos solo a la conversación actual.

Las rutinas nuevas de `routines/` quedan como borradores locales ignorados por Git. Revisa su código, elimina rutas específicas del PC y valida su comportamiento en un RVT descartable antes de compartirlas; una rutina aprobada puede añadirse explícitamente al repositorio.

## Más información

- [Arquitectura y evaluación de repositorios](docs/ARCHITECTURE_DECISION.md)
- [Glosario español ↔ Revit](docs/GLOSARIO_BIM_REVIT_ES_EN.md)
- [Instalar y diagnosticar en otro PC](docs/INSTRUCCIONES_OTRO_PC.md)
- [Estado actual del prototipo](docs/PROTOTYPE_STATUS.md)
- [Materiales: catálogo, lectura e interpretación](docs/MATERIALES_REVIT.md)

## Uso previsto y permisos

El uso previsto es con colegas del equipo. El repositorio está **público** y no tiene un archivo `LICENSE`: cualquiera puede verlo en GitHub, por lo que la intención de uso interno no restringe quién puede acceder al código. No se ha elegido una licencia de código abierto. Si más adelante debe ser realmente interno, cambia la visibilidad del repositorio a privado. [Qué implica publicar un repositorio sin licencia](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository).
