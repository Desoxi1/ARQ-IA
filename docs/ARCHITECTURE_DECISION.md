# Decisión de arquitectura — ARQ-IA

**Actualizada:** 5 de octubre de 2026.

## Decisión

ARQ-IA combina tres piezas locales:

1. `bimwright/rvt-mcp` 0.6.2 ofrece consultas y acciones tipadas sobre Revit 2027. Se instaló y las consultas de vista activa y selección funcionaron en este PC. No se ha probado cada una de sus herramientas de escritura.
2. Un MCP propio crea/lista rutinas versionables (`.py`, `.dyn`, manifiesto con hashes) y otro pide su ejecución al add-in Revit. El grafo usa un nodo PythonNet3 en modo Manual.
3. El chat en el panel Revit y la extensión lateral de Dynamo usan Codex app-server con los MCP configurados para la carpeta clonada. Aceptan texto, imágenes y archivos. El contenido adjunto se trata como referencia, no como código ejecutable por sí mismo.

Se reutiliza `rvt-mcp` porque ya aporta una superficie de Revit tipada para la versión instalada. El bridge propio cubre la función que `rvt-mcp` no ofrece aquí: guardar y lanzar rutinas Dynamo. Reescribir todas las herramientas de Revit aumentaría mantenimiento y no resolvería el problema actual de sincronización de Dynamo.

## Estado probado

El paquete Dynamo 0.3.0 carga desde `%APPDATA%\Dynamo\Dynamo Revit\27.0\packages\ARQ-IA`. El log vivo registró `Loaded Package ARQ-IA` y `ARQ-IA ... view extension is added`; el diagnóstico llegó a 18 PASS, 0 WAIT, 0 WARN y 0 FAIL. El cliente C# de chat pudo abrir Codex, crear/listar una rutina mediante MCP y procesar una imagen y un texto adjuntos. El add-in Revit y la extensión compilan con las API instaladas.

**La rutina fija de solo lectura pasó en Revit real:** PythonNet3 devolvió el resultado y el bridge verificó que no cambió el modelo descartable. El [código de DynamoRevit](https://github.com/DynamoDS/DynamoRevit/blob/master/src/DynamoRevit/DynamoRevit.cs) ejecuta el grafo de forma asíncrona cuando la UI ya está abierta; el bridge ahora espera el resultado sin bloquear la interfaz y lo verifica en otro evento de Revit. También eliminó una segunda llamada a `Run()` que podía duplicar una escritura. La rutina genérica de lectura aún agotó su tiempo al cambiar de grafo. Quedó preparado, sin prueba viva todavía, un ajuste para conservar el grafo temporal mientras Dynamo lo tenga abierto. Por ello **no se considera terminada la ejecución genérica ni la de escrituras**.

## Límites de ejecución

- El runner propio solo acepta un RVT local y descartable bajo `revit/test-models/`; para rutinas genéricas exige confirmación de posibles escrituras. Compara los cambios declarados por la rutina con `DocumentChanged` de Revit. No se ha demostrado todavía una escritura exitosa y verificada.
- Los scripts PythonNet3 corren con los permisos del usuario dentro de Revit. La restricción de RVT no es un sandbox de sistema operativo. La aprobación debe mostrar el script y el alcance antes de una escritura.
- `rvt-mcp` puede modificar el modelo al aprobar una acción tipada; el perfil de Codex pide aprobación por llamada. No existe garantía general de previsualización o reversión.
- Los DLL tienen `Company`/`VendorDescription` de **Branko Plaza**. Siguen sin firma de editor confiable, por lo que Windows puede indicar «Fabricante desconocido».

## pyRevit y otras opciones

PythonNet3 ya cubre el motor Python de Dynamo. pyRevit aporta un entorno para comandos, pestañas y paneles directamente en Revit; añadirlo no arregla el runner Dynamo ni es requisito del chat actual. Sus [releases oficiales](https://github.com/pyrevitlabs/pyRevit/releases) declaran soporte para Revit 2027 y su [documentación](https://docs.pyrevitlabs.io/reference/pyrevit/extensions/) explica las extensiones. Se evaluará para tareas que necesiten una herramienta Revit independiente de un grafo Dynamo.

Autodesk publicó Dynamo Core 4.2 con DynamoMCP, pero la versión instalada con este Revit es Dynamo 4.0.2. Las [notas oficiales](https://dynamobim.org/dynamo-core-4-2-release/) no prueban que el MCP de 4.2 esté disponible en este host ni conectado a Codex. Tampoco se instalará el Revit Public MCP Server en esta fase: su [guía](https://help.autodesk.com/view/ADSKMCP/ENU/?guid=ADSKMCP_RevitMcp_setting_up_revit_mcp_server_html) requiere Revit 2027.2, mientras este PC tiene 2027.0.

## Distribución

El repositorio incluye `Instalar-ARQ-IA.cmd`, scripts de instalación, DLL y hashes. El instalador configura rutas por clon, instala los paneles por usuario y registra los tres MCP. No incluye RVT ni secretos. El paquete Dynamo sigue el [formato oficial de extensiones como paquetes](https://developer.dynamobim.org/05-Package-Deployment/deploying-extensions-on-the-package-manager.html). Un nuevo PC debe tener Revit 2027 y Codex Desktop con sesión iniciada; la prueba de ejecución se hará primero en un modelo descartable.
