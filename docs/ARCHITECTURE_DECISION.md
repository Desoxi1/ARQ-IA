# Decisión de arquitectura — ARQ-IA

**Actualizada:** 14 de octubre de 2026.

## Decisión

ARQ-IA combina tres piezas locales:

1. `bimwright/rvt-mcp` ofrece consultas y acciones tipadas sobre Revit 2027. Un clon descarga la versión 1.1.0 fijada por hash. El perfil ARQ-IA habilita `revit_override_element_graphics` y las consultas necesarias para contexto, geometría, salas, documentación, estructura, MEP, familias y vínculos. No se ha probado cada herramienta de escritura ni la versión 1.1.0 integrada con ARQ-IA.
2. Un MCP propio crea/lista rutinas versionables (`.py`, `.dyn`, manifiesto con hashes); otro puede pedir que un borrador se cargue en la sesión Dynamo del documento activo en Manual para que el usuario lo revise. Una operación separada pide la ejecución al add-in Revit y conserva la puerta de `revit/test-models/`.
3. El chat en el panel Revit y la extensión lateral de Dynamo usan Codex app-server con los MCP configurados para la carpeta clonada. Aceptan texto, imágenes y archivos. El contenido adjunto se trata como referencia, no como código ejecutable por sí mismo.

Se reutiliza `rvt-mcp` porque ya aporta una superficie de Revit tipada para la versión instalada. El bridge propio cubre la función que `rvt-mcp` no ofrece aquí: guardar y lanzar rutinas Dynamo. Reescribir todas las herramientas de Revit aumentaría mantenimiento y no resolvería el problema actual de sincronización de Dynamo.

### Revisión de repositorios y decisión de reutilización

| Fuente oficial del proyecto | Hallazgo aplicable | Decisión |
|---|---|---|
| [bimwright/rvt-mcp](https://github.com/bimwright/rvt-mcp) | El release fijado 1.1.0 publica un ZIP de instalación para Revit 2022–2027 y ofrece toolsets por dominio; su instalador puede migrar datos previos y escribir configuraciones de clientes. | Fijar 1.1.0 por SHA-256. Seleccionar toolsets por dominio y una allowlist de herramientas tipadas; excluir `meta`, `delete`, `export`, `workflows`, ToolBaker y `send_code`. |
| [KenLP/RevitMCPServer](https://github.com/KenLP/RevitMCPServer) | Documenta `dry-run`, diffs y transacciones agrupadas para Revit 2025–2027. | Tomar el patrón de vista previa y recibo como referencia. No instalar un segundo puente que pueda escribir en el mismo modelo. |
| [HorizunGroup/horizun-revit-mcp](https://github.com/HorizunGroup/horizun-revit-mcp) | Documenta una superficie BIM amplia y perfiles de operación. | Referencia de cobertura; no fusionar repositorios ni asumir compatibilidad por su README. |
| [BhaveshY/revit-mcp-next](https://github.com/BhaveshY/revit-mcp-next) | Su documentación lista herramientas tipadas adicionales, como `set_element_pinned`, y advierte que debe usarse un solo servidor Revit MCP por cliente; no se hizo auditoría estática de su código ni de sus flujos salientes. | No agregarlo junto a `rvt-mcp`: implicaría dos integraciones sobre la misma sesión. Mantenerlo como candidato para una migración evaluada si se necesita desanclar/dividir elementos con herramienta tipada. |
| [Autodesk Dynamo / Revit MCP](https://help.autodesk.com/view/ADSKMCP/ENU/?guid=ADSKMCP_RevitMcp_revit_mcp_server_html) | La opción oficial depende de la actualización y el host instalados. | Evaluar cuando Revit/Dynamo del equipo soporten la versión requerida; no duplicar un servidor por anticipado. |

La revisión fue de documentación, esquemas MCP del binario local y lectura puntual del manejador gráfico 0.6.2. No se ejecutó código de los otros repositorios ni se hizo auditoría completa de su cadena de suministro. Red saliente de la instalación: descarga HTTPS desde GitHub; el instalador externo también puede gestionar sus carpetas y configuraciones locales, por lo que ARQ-IA lo invoca con `-Client none`. La sesión Codex transmite mensajes y adjuntos según la configuración de Codex; el MCP local no convierte esos datos en entrenamiento permanente.

La respuesta incorrecta «no tengo herramientas Revit» se corrigió en las instrucciones de ARQ-IA: una llamada Revit completada prueba que existe conectividad y el asistente debe precisar qué operación concreta falta. El catálogo integrado conserva acciones tipadas y usa el runner Dynamo del modelo activo para lógica de edición que no esté cubierta, siempre con revisión de la rutina y confirmación Sí/No en Revit. En este catálogo no hay herramienta tipada dedicada a desanclar/dividir un muro; `revit-mcp-next` es solo referencia, no una dependencia incorporada.

## Evidencia histórica y límites actuales

El paquete Dynamo actual declara la versión **0.5.1**. Un log histórico de la versión 0.3.0 registró `Loaded Package ARQ-IA` y `ARQ-IA ... view extension is added`; el diagnóstico de esa sesión llegó a 18 PASS, 0 WAIT, 0 WARN y 0 FAIL. Un smoke del cliente C# abrió Codex, creó/listó una rutina mediante MCP y procesó una imagen y un texto adjuntos. Estos resultados pertenecen a sus versiones y sesiones indicadas; no certifican la versión 0.5.1 ni una instalación limpia en otro PC.

**La rutina fija de solo lectura pasó en Revit real el 5 de octubre de 2026:** PythonNet3 devolvió el resultado y el bridge verificó que no cambió el modelo descartable. El [código de DynamoRevit](https://github.com/DynamoDS/DynamoRevit/blob/master/src/DynamoRevit/DynamoRevit.cs) ejecuta el grafo de forma asíncrona cuando la UI ya está abierta; el bridge espera el resultado sin bloquear la interfaz y lo verifica en otro evento de Revit. También se eliminó una segunda llamada a `Run()` que podía duplicar una escritura. La rutina genérica de lectura agotó su tiempo al cambiar de grafo; el ajuste posterior para conservar el grafo temporal todavía no tiene prueba viva. Por ello **la ejecución genérica y las escrituras siguen pendientes de validación**.

El fallo de “pinta los muros del primer piso de rojo” tuvo dos causas de producto: la configuración no exponía `revit_override_element_graphics`, aunque el binario sí la tenía, y el panel interceptaba la frase antes de consultar Revit mediante una regla local que no conservaba el pedido para el siguiente turno. Se retiró esa interceptación, se habilitó `graphics` con una sola herramienta de escritura gráfica, y se documentó la interpretación [vista frente a material](COLOR_MUROS_PRIMER_PISO.md). **Esta corrección aún necesita ejecución viva en una copia descartable.**

## Límites de ejecución

- La preparación `prepare_saved_routine_in_dynamo` acepta el documento activo incluso si es un modelo de trabajo, pero solo solicita abrir el grafo; no llama al ejecutor ni guarda el RVT. Rechaza una sesión Dynamo ligada a otro documento y no reemplaza un workspace con nodos/notas. La apertura sin ejecución no está aún validada con Dynamo 4.0.2 instalado.
- `execute_saved_routine` sigue limitado a un RVT local descartable bajo `revit/test-models/`. La operación separada `execute_saved_routine_in_active_model` exige Dynamo abierto con workspace vacío o la misma rutina guardada sin cambios pendientes, petición expresa del usuario y un diálogo Sí/No en Revit antes de cargar y ejecutar; Acceso completo no lo omite. El bridge compara los cambios declarados por la rutina con `DocumentChanged`. La ejecución en el RVT de trabajo aún no tiene validación viva.
- El usuario puede revisar el `.py` cargado y decidir si ejecutarlo manualmente en Dynamo sobre el documento abierto. Esa ejecución no pasa por las restricciones ni por la verificación de cambios del runner MCP; PythonNet3 tiene permisos del proceso Revit.
- Los scripts PythonNet3 corren con los permisos del usuario dentro de Revit. La restricción de RVT y el diálogo de confirmación no son un sandbox de sistema operativo. Revisa el script y el alcance antes de cualquier escritura; Acceso completo no elimina la confirmación para el RVT abierto.
- `rvt-mcp` puede modificar el modelo mediante acciones tipadas. El panel ahora ofrece un modo controlado y otro de acceso completo por sesión; Codex app-server confirmó creación/listado de rutina sin ventanas de aprobación en el smoke. Las escrituras de Revit aún deben validarse en vivo, y no existe garantía general de reversión.
- Los manifiestos `.addin` registran `VendorDescription=Branko Plaza`, pero las DLL siguen sin firma de editor confiable. Revit puede indicar «Fabricante desconocido» en el diálogo de seguridad hasta que se use un certificado de firma de código confiable.

## pyRevit y otras opciones

PythonNet3 ya cubre el motor Python de Dynamo. pyRevit aporta un entorno para comandos, pestañas y paneles directamente en Revit; añadirlo no arregla el runner Dynamo ni es requisito del chat actual. Sus [releases oficiales](https://github.com/pyrevitlabs/pyRevit/releases) declaran soporte para Revit 2027 y su [documentación](https://docs.pyrevitlabs.io/reference/pyrevit/extensions/) explica las extensiones. Se evaluará para tareas que necesiten una herramienta Revit independiente de un grafo Dynamo.

La documentación de DynamoMCP y Revit Public MCP depende de la actualización instalada de Autodesk. No se considera disponible en ARQ-IA hasta confirmar la versión compatible del host y su conexión real con Codex; consulta las páginas oficiales actuales antes de reevaluarlo.

## Distribución

El repositorio incluye `Instalar-ARQ-IA.cmd`, scripts de instalación, DLL y hashes. El instalador configura rutas por clon, instala los paneles por usuario y registra los tres MCP. No incluye RVT ni secretos. El paquete Dynamo sigue el [formato oficial de extensiones como paquetes](https://developer.dynamobim.org/05-Package-Deployment/deploying-extensions-on-the-package-manager.html). Un nuevo PC debe tener Revit 2027 y Codex Desktop con sesión iniciada; la prueba de ejecución se hará primero en un modelo descartable. La versión de `rvt-mcp` se fija por digest de su release; actualizarla requiere revisar herramienta, esquema, instalador y compatibilidad de panel antes de cambiar ese pin.
