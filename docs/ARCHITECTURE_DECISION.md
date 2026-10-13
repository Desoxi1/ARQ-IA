# Decisión de arquitectura — ARQ-IA

**Actualizada:** 13 de octubre de 2026.

## Decisión

ARQ-IA combina tres piezas locales:

1. `bimwright/rvt-mcp` ofrece consultas y acciones tipadas sobre Revit 2027. La instalación local 0.6.2 pasó consultas de vista activa/selección y expone `revit_override_element_graphics`, que no estaba habilitada en ARQ-IA. Un clon nuevo descarga la versión 1.1.0 fijada por hash. No se ha probado cada herramienta de escritura ni la versión 1.1.0 integrada con ARQ-IA.
2. Un MCP propio crea/lista rutinas versionables (`.py`, `.dyn`, manifiesto con hashes) y otro pide su ejecución al add-in Revit. El grafo usa un nodo PythonNet3 en modo Manual.
3. El chat en el panel Revit y la extensión lateral de Dynamo usan Codex app-server con los MCP configurados para la carpeta clonada. Aceptan texto, imágenes y archivos. El contenido adjunto se trata como referencia, no como código ejecutable por sí mismo.

Se reutiliza `rvt-mcp` porque ya aporta una superficie de Revit tipada para la versión instalada. El bridge propio cubre la función que `rvt-mcp` no ofrece aquí: guardar y lanzar rutinas Dynamo. Reescribir todas las herramientas de Revit aumentaría mantenimiento y no resolvería el problema actual de sincronización de Dynamo.

### Revisión de repositorios y decisión de reutilización

| Fuente oficial del proyecto | Hallazgo aplicable | Decisión |
|---|---|---|
| [bimwright/rvt-mcp](https://github.com/bimwright/rvt-mcp) | Incluye herramientas gráficas y materiales; el release 0.6.2 quedó archivado y 1.1.0 publica un ZIP de instalación para Revit 2022–2027. Su instalador puede mover datos de una instalación antigua y escribir configuraciones de clientes. | Usar la herramienta gráfica existente para el caso de muros rojos. Fijar 1.1.0 para equipos nuevos con `-Client none` y SHA-256; conservar 0.6.2 instalada en este PC hasta validar una migración separada. No habilitar `send_code` ni ToolBaker. |
| [KenLP/RevitMCPServer](https://github.com/KenLP/RevitMCPServer) | Documenta `dry-run`, diffs y transacciones agrupadas para Revit 2025–2027. | Tomar el patrón de vista previa y recibo como referencia. No instalar un segundo puente que pueda escribir en el mismo modelo. |
| [HorizunGroup/horizun-revit-mcp](https://github.com/HorizunGroup/horizun-revit-mcp) | Documenta una superficie BIM amplia y perfiles de operación. | Referencia de cobertura; no fusionar repositorios ni asumir compatibilidad por su README. |
| [Autodesk Dynamo / Revit MCP](https://help.autodesk.com/view/ADSKMCP/ENU/?guid=ADSKMCP_RevitMcp_revit_mcp_server_html) | La opción oficial depende de la actualización y el host instalados. | Evaluar cuando Revit/Dynamo del equipo soporten la versión requerida; no duplicar un servidor por anticipado. |

La revisión fue de documentación, esquemas MCP del binario local y lectura puntual del manejador gráfico 0.6.2. No se ejecutó código de los otros repositorios ni se hizo auditoría completa de su cadena de suministro. Red saliente de la instalación: descarga HTTPS desde GitHub; el instalador externo también puede gestionar sus carpetas y configuraciones locales, por lo que ARQ-IA lo invoca con `-Client none`. La sesión Codex transmite mensajes y adjuntos según la configuración de Codex; el MCP local no convierte esos datos en entrenamiento permanente.

## Estado probado

El paquete Dynamo 0.3.0 carga desde `%APPDATA%\Dynamo\Dynamo Revit\27.0\packages\ARQ-IA`. El log vivo registró `Loaded Package ARQ-IA` y `ARQ-IA ... view extension is added`; el diagnóstico llegó a 18 PASS, 0 WAIT, 0 WARN y 0 FAIL. El cliente C# de chat pudo abrir Codex, crear/listar una rutina mediante MCP y procesar una imagen y un texto adjuntos. El add-in Revit y la extensión compilan con las API instaladas.

**La rutina fija de solo lectura pasó en Revit real:** PythonNet3 devolvió el resultado y el bridge verificó que no cambió el modelo descartable. El [código de DynamoRevit](https://github.com/DynamoDS/DynamoRevit/blob/master/src/DynamoRevit/DynamoRevit.cs) ejecuta el grafo de forma asíncrona cuando la UI ya está abierta; el bridge ahora espera el resultado sin bloquear la interfaz y lo verifica en otro evento de Revit. También eliminó una segunda llamada a `Run()` que podía duplicar una escritura. La rutina genérica de lectura aún agotó su tiempo al cambiar de grafo. Quedó preparado, sin prueba viva todavía, un ajuste para conservar el grafo temporal mientras Dynamo lo tenga abierto. Por ello **no se considera terminada la ejecución genérica ni la de escrituras**.

El fallo de “pinta los muros del primer piso de rojo” tuvo dos causas de producto: la configuración no exponía `revit_override_element_graphics`, aunque el binario sí la tenía, y el panel interceptaba la frase antes de consultar Revit mediante una regla local que no conservaba el pedido para el siguiente turno. Se retiró esa interceptación, se habilitó `graphics` con una sola herramienta de escritura gráfica, y se documentó la interpretación [vista frente a material](COLOR_MUROS_PRIMER_PISO.md). **Esta corrección aún necesita ejecución viva en una copia descartable.**

## Límites de ejecución

- El runner propio solo acepta un RVT local y descartable bajo `revit/test-models/`; para rutinas genéricas exige confirmación de posibles escrituras. Compara los cambios declarados por la rutina con `DocumentChanged` de Revit. No se ha demostrado todavía una escritura exitosa y verificada.
- Los scripts PythonNet3 corren con los permisos del usuario dentro de Revit. La restricción de RVT no es un sandbox de sistema operativo. La aprobación debe mostrar el script y el alcance antes de una escritura.
- `rvt-mcp` puede modificar el modelo al aprobar una acción tipada; el perfil de Codex pide aprobación por llamada. No existe garantía general de previsualización o reversión.
- Los DLL tienen `Company`/`VendorDescription` de **Branko Plaza**. Siguen sin firma de editor confiable, por lo que Windows puede indicar «Fabricante desconocido».

## pyRevit y otras opciones

PythonNet3 ya cubre el motor Python de Dynamo. pyRevit aporta un entorno para comandos, pestañas y paneles directamente en Revit; añadirlo no arregla el runner Dynamo ni es requisito del chat actual. Sus [releases oficiales](https://github.com/pyrevitlabs/pyRevit/releases) declaran soporte para Revit 2027 y su [documentación](https://docs.pyrevitlabs.io/reference/pyrevit/extensions/) explica las extensiones. Se evaluará para tareas que necesiten una herramienta Revit independiente de un grafo Dynamo.

Autodesk publicó Dynamo Core 4.2 con DynamoMCP, pero la versión instalada con este Revit es Dynamo 4.0.2. Las [notas oficiales](https://dynamobim.org/dynamo-core-4-2-release/) no prueban que el MCP de 4.2 esté disponible en este host ni conectado a Codex. Tampoco se instalará el Revit Public MCP Server en esta fase: su [guía](https://help.autodesk.com/view/ADSKMCP/ENU/?guid=ADSKMCP_RevitMcp_setting_up_revit_mcp_server_html) requiere Revit 2027.2, mientras este PC tiene 2027.0.

## Distribución

El repositorio incluye `Instalar-ARQ-IA.cmd`, scripts de instalación, DLL y hashes. El instalador configura rutas por clon, instala los paneles por usuario y registra los tres MCP. No incluye RVT ni secretos. El paquete Dynamo sigue el [formato oficial de extensiones como paquetes](https://developer.dynamobim.org/05-Package-Deployment/deploying-extensions-on-the-package-manager.html). Un nuevo PC debe tener Revit 2027 y Codex Desktop con sesión iniciada; la prueba de ejecución se hará primero en un modelo descartable. La versión de `rvt-mcp` se fija por digest de su release; actualizarla requiere revisar herramienta, esquema, instalador y compatibilidad de panel antes de cambiar ese pin.
