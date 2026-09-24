# Estado del prototipo

## Preparado en este PC

- Paquete oficial `rvt-mcp` v0.6.2 descargado y verificado con SHA-256.
- SDK .NET 10.0.100 instalado en una carpeta de herramientas local para evaluar un add-in propio; todavía no se ha compilado ningún runner.
- Add-in RvtMcp instalado en `%APPDATA%\Autodesk\Revit\Addins\2027`; servidor self-contained instalado en `%LOCALAPPDATA%\RvtMcp\rvt\server\0.6.2`.
- `setup.ps1` instala para Revit 2027 solamente y genera configuración local del proyecto, sin escribir en la configuración global de Codex.
- Configuración de proyecto preparada con herramientas Revit tipadas de lectura, creación y edición bajo aprobación por llamada, más crear/listar rutinas locales. La configuración de este paquete debe cargarse y validarse en Codex.
- MCP propio de rutinas implementado en Windows PowerShell: escribe fuente PythonNet3, un `.dyn` manual y un manifiesto con alcance, versión y hashes.
- Plantilla Dynamo: un nodo PythonNet3, sin conectores, sin dependencias de paquetes y `RunType=Manual`.
- Intercambio MCP stdio rvt-mcp probado: inicializa v0.6.2 y lista 51 herramientas para los grupos `query,create,modify,view,annotation`. El perfil Codex permite solo una lista curada; no incluye borrado, ejecución arbitraria, ToolBaker ni autoejecución Dynamo.

## Límite funcional actual

Codex puede crear y guardar una rutina según la instrucción del usuario. **No puede disparar todavía la ejecución de ese `.dyn` desde MCP.** Para probar el script ahora, el usuario revisa el `.py`, abre el `.dyn` en Dynamo y pulsa Run manualmente. El MCP de Revit no ve ni devuelve el resultado de Dynamo.

La conexión Codex se escribe como configuración de proyecto en `.codex/config.toml`, que Codex carga solo si se abre y confía este directorio. La configuración curada ofrece consultas y algunas acciones tipadas de creación/edición con aprobación por llamada. La integración MCP→herramientas desde Codex sigue pendiente de una prueba dentro de la app.

## Evidencia de validación

| Comprobación | Resultado |
|---|---|
| Hash publicado de rvt-mcp v0.6.2 | Coincide: `0d6239afcab47c729c30b8c5b763214c20c38c4b72de7e30c5a7191b4131b1ae`. |
| `install.ps1 -WhatIf -Years 2027 -Client none` | Solo previsualizó R27 + servidor en carpetas de usuario; no tocó Revit ni Codex. |
| Instalación real | Add-in y ejecutable presentes en perfil de usuario. Revit no estaba en ejecución durante la instalación. |
| MCP de rutinas | Aprobado: initialize, tools/list, create y list; generó `.py`, `.dyn` y manifiesto; el grafo valida como un nodo PythonNet3 Manual. Python 3.14 compiló el script de muestra. |
| Rutina base: pruebas offline | Aprobado: 4 pruebas verifican documento ausente, selección vacía, elementos/categorías y coherencia/hashes entre `.py`, `.dyn` y manifiesto. Usa dobles de Revit; no sustituye una ejecución visual de Dynamo. |
| Protección al guardar escritura | Aprobada: `writes_model=true` sin `confirm_model_write=true` devuelve error y no crea archivos. |
| Revit 2027 con sesión/modelo de prueba | Consulta directa de solo lectura aprobada el 24-09-2026: vista activa y un muro seleccionado respondieron correctamente. No se modificó el documento. Se omitieron IDs/nombres del modelo de esta copia pública. Falta comprobar el MCP desde Codex con el proyecto confiable abierto. |
| Esquemas MCP para perfil inicial | Revisados nombres y esquemas de consultas y operaciones tipadas de nivel, eje, línea (incluye muro), superficie (incluye piso), recinto, instancia, parámetro, tipo, texto, cotas y etiquetas. No equivale a una prueba de ejecución en el modelo. |
| Cargar y correr `.dyn` PythonNet3 | Pendiente de prueba visual en Dynamo 4.0.2. |
| Invocar Dynamo automáticamente desde MCP | No implementado. |

## Prueba integrada que falta

1. Ejecutar Revit 2027 desde el escritorio de Windows; aceptar el add-in RvtMcp si Revit muestra su cuadro de confianza. Usar una plantilla/proyecto local descartable.
2. Abrir esta carpeta como proyecto confiable en Codex y reiniciarlo.
3. Verificar que rvt-mcp informa una conexión activa; consultar vista activa y selección y contrastar con la interfaz.
4. Pedir a Codex que prepare `read_active_view_selection`. Revisar el `.py` y `.dyn`, abrir el grafo en Dynamo 4.0.2 y ejecutar manualmente.
5. Implementar el runner de grafos por identificador y repetir con un grafo de lectura. Solo después probar la escritura de Comentarios en un modelo desechable.
