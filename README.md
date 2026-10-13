# ARQ-IA

**Asistente para Revit 2027 y Dynamo 4.0.2.** Desarrollado por Branko Plaza. Abre un chat en Revit y en **Dynamo → Extensiones → Abrir ARQ-IA**, acepta texto e imágenes/archivos, conserva el historial local por carpeta de proyecto y usa Codex para interpretar solicitudes BIM.

## Cómo funciona

```text
Arquitecto → panel ARQ-IA → Codex → MCP rvt-mcp → API de Revit
                                  └→ MCP de rutinas → grafo Dynamo/PythonNet3 → bridge Revit
```

Un **MCP** es el protocolo que ofrece herramientas concretas a Codex; no es otro modelo entrenado. `rvt-mcp` permite leer y ejecutar operaciones Revit tipadas. El MCP propio guarda scripts `.py`, grafos `.dyn` y manifiestos en `routines/`. El bridge puede abrir y ejecutar esas rutinas en Dynamo sobre un **RVT descartable**. Una rutina aprobada se conserva y puede repetirse sin volver a generarla con IA. El chat usa la sesión local de Codex; no guarda una API key.

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

- **Comprobado antes de este cambio:** paneles instalados, conexión Codex app-server, consulta MCP de vista/selección y ejecución de una rutina fija PythonNet3 de solo lectura en un RVT descartable.
- **Comprobado ahora:** consulta completa de 300 materiales definidos en el modelo arquitectónico de muestra, con propiedades detalladas de seis materiales. La lectura de materiales queda habilitada en la configuración local; el catálogo bruto se conserva solo en este PC.
- **Interfaz preparada para el próximo inicio:** diseño de chat compacto según la referencia de Branko, selector con los modelos reales publicados por el Codex local, conversación nueva, respuestas con formato y tarjetas de llamadas MCP recibidas. El indicador verde aparece solo después de una respuesta MCP. La versión se compiló e instaló por usuario mientras Revit estaba abierto; todavía falta verla cargada en una sesión nueva.
- **Corregido en el código:** se quitó el bloqueo local de frases como “pinta el piso”, se expuso la herramienta gráfica tipada y se actualizó la descarga para clones nuevos.
- **Pendiente de validar en Revit:** la solicitud de muros rojos desde el panel, la ejecución de rutinas genéricas al cambiar de grafo y cualquier escritura compleja. No se promete “cualquier pedido” ni edición autónoma de modelos productivos.

El chat ofrece **Acceso controlado** y **Acceso completo** por sesión. Completo elimina las aprobaciones repetidas de Codex y permite sus herramientas de archivos, comandos y red con permisos amplios en el PC. Los scripts PythonNet3 se ejecutan con los permisos de Revit y el runner los limita a modelos locales bajo `revit/test-models/`; esa restricción **no** es un sandbox del sistema. [Estado y evidencia](docs/PROTOTYPE_STATUS.md).

## Carpetas y archivos necesarios

| Ruta | Función |
|---|---|
| `Instalar-ARQ-IA.cmd`, `install.ps1`, `setup.ps1` | Instalación de un clon y configuración local. |
| `doctor.ps1` | Diagnóstico sin cambiar Revit. |
| `revit/assistant/`, `dynamo/extension/` | Paneles y binarios precompilados con hashes. |
| `revit/bridge/`, `mcp/` | Ejecución y herramientas MCP locales. |
| `routines/`, `dynamo/` | Rutinas guardadas, plantilla y wrapper PythonNet3. |
| `docs/`, `tests/` | Diseño, estado y verificaciones de desarrollo. |

Los scripts `build.ps1` sirven al desarrollador; no son necesarios para instalar en otro PC. `package.ps1` crea un ZIP desde un commit Git limpio. `logs/`, `artifacts/`, modelos RVT, adjuntos, cachés y `.codex/config.toml` local quedan fuera de Git. Los manifiestos ya identifican a **Branko Plaza** como fabricante. Revit seguirá mostrando el aviso de fabricante desconocido porque las DLL aún no tienen una firma digital de un certificado de confianza; para mostrar un editor verificado y retirar ese aviso se necesita firmar la publicación con un certificado de firma de código confiable. [Seguridad de complementos de Revit](https://help.autodesk.com/cloudhelp/2022/ENU/Revit-Troubleshooting/files/GUID-36D29367-A540-4CFD-9773-4995BF1DF08A.htm) · [Firma digital de complementos](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Introduction/Add_In_Integration/Digitally_Signing_Your_Revit_Add_in/Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Digitally_Signing_Your_App_html.html).

En este PC puede existir `.arq-ia/model-profile.md` con un perfil de solo lectura y el catálogo de materiales del modelo actualmente revisado. `.arq-ia/` está ignorada y no se incluye en Git ni en el paquete; cada arquitecto construye su propio contexto a partir de su modelo.

El botón de historial `◷` abre las conversaciones guardadas en `.arq-ia/conversations/`. Al clonar el repositorio en otro PC, el historial empieza vacío y permanece en esa carpeta local; solo conserva texto, estados/resúmenes de herramientas y nombres de adjuntos, no los archivos adjuntos. Al abrir una conversación anterior y continuar, ARQ-IA entrega a Codex los últimos mensajes de texto con un límite de contexto y vuelve a consultar Revit antes de proponer cambios.

En el primer envío de cada copia local sin perfil, ARQ-IA pide a Codex una ficha concisa del proyecto abierto mediante MCP de solo lectura: documento, niveles, fases, cantidades por categoría y un resumen acotado de materiales. La ficha queda en `.arq-ia/model-profile.md`, no se sube a Git. También puede existir `.arq-ia/project-memory.md` con convenciones y correcciones locales verificadas; esto conserva contexto para el proyecto, pero no entrena el modelo ni comparte información entre PCs.

La carpeta `.arq-ia` contiene texto local sin cifrar; no guardes secretos allí. En **Acceso controlado**, Codex puede pedir permiso para crear o actualizar el perfil y las notas; **Acceso completo** aplica los permisos elegidos para esa sesión.

Las rutinas nuevas de `routines/` quedan como borradores locales ignorados por Git. Revisa su código, elimina rutas específicas del PC y valida su comportamiento en un RVT descartable antes de compartirlas; una rutina aprobada puede añadirse explícitamente al repositorio.

## Más información

- [Arquitectura y evaluación de repositorios](docs/ARCHITECTURE_DECISION.md)
- [Glosario español ↔ Revit](docs/GLOSARIO_BIM_REVIT_ES_EN.md)
- [Instalar y diagnosticar en otro PC](docs/INSTRUCCIONES_OTRO_PC.md)
- [Estado actual del prototipo](docs/PROTOTYPE_STATUS.md)
- [Materiales: catálogo, lectura e interpretación](docs/MATERIALES_REVIT.md)
