# ARQ-IA

**Asistente para Revit 2027 y Dynamo 4.0.2.** Desarrollado por Branko Plaza. Abre un chat en Revit y en **Dynamo → Extensiones → Abrir ARQ-IA**, acepta texto e imágenes/archivos, y usa Codex para interpretar solicitudes BIM.

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
- **Corregido en el código:** se quitó el bloqueo local de frases como “pinta el piso”, se expuso la herramienta gráfica tipada y se actualizó la descarga para clones nuevos.
- **Pendiente de validar en Revit:** la solicitud de muros rojos desde el panel, la ejecución de rutinas genéricas al cambiar de grafo y cualquier escritura compleja. No se promete “cualquier pedido” ni edición autónoma de modelos productivos.

Las herramientas que escriben requieren aprobación por llamada. Los scripts PythonNet3 se ejecutan con los permisos de Revit y el runner los limita a modelos locales bajo `revit/test-models/`; esa restricción **no** es un sandbox del sistema. Revisa el código antes de ejecutarlo. [Estado y evidencia](docs/PROTOTYPE_STATUS.md).

## Carpetas y archivos necesarios

| Ruta | Función |
|---|---|
| `Instalar-ARQ-IA.cmd`, `install.ps1`, `setup.ps1` | Instalación de un clon y configuración local. |
| `doctor.ps1` | Diagnóstico sin cambiar Revit. |
| `revit/assistant/`, `dynamo/extension/` | Paneles y binarios precompilados con hashes. |
| `revit/bridge/`, `mcp/` | Ejecución y herramientas MCP locales. |
| `routines/`, `dynamo/` | Rutinas guardadas, plantilla y wrapper PythonNet3. |
| `docs/`, `tests/` | Diseño, estado y verificaciones de desarrollo. |

Los scripts `build.ps1` sirven al desarrollador; no son necesarios para instalar en otro PC. `package.ps1` crea un ZIP desde un commit Git limpio. `logs/`, `artifacts/`, modelos RVT, adjuntos, cachés y `.codex/config.toml` local quedan fuera de Git. El nombre de fabricante **Branko Plaza** figura en la metadata, pero un aviso de Windows de “fabricante desconocido” solo se elimina con firma de código confiable.

## Más información

- [Arquitectura y evaluación de repositorios](docs/ARCHITECTURE_DECISION.md)
- [Glosario español ↔ Revit](docs/GLOSARIO_BIM_REVIT_ES_EN.md)
- [Instalar y diagnosticar en otro PC](docs/INSTRUCCIONES_OTRO_PC.md)
- [Estado actual del prototipo](docs/PROTOTYPE_STATUS.md)
- [Materiales: catálogo, lectura e interpretación](docs/MATERIALES_REVIT.md)
