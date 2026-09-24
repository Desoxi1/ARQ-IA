# Estado del prototipo

## Preparado en este PC

- Paquete oficial `rvt-mcp` v0.6.2 descargado y verificado con SHA-256.
- .NET Runtime 10.0.0 está instalado y es la versión usada por Revit 2027. El SDK .NET no está instalado; Visual Studio Build Tools aporta Roslyn `csc`, con el que el add-in propio ya compila. Aún no se ha instalado ni cargado en Revit.
- Add-in RvtMcp instalado en `%APPDATA%\Autodesk\Revit\Addins\2027`; servidor self-contained instalado en `%LOCALAPPDATA%\RvtMcp\rvt\server\0.6.2`.
- `setup.ps1` instala para Revit 2027 solamente y genera configuración local del proyecto, sin escribir en la configuración global de Codex.
- Configuración de proyecto preparada con herramientas Revit tipadas de lectura, creación y edición bajo aprobación por llamada, más crear/listar rutinas locales. La configuración de este paquete debe cargarse y validarse en Codex.
- MCP propio de rutinas implementado en Windows PowerShell: escribe fuente PythonNet3, un `.dyn` manual y un manifiesto con alcance, versión y hashes.
- Puente add-in/MCP implementado y compilado: named pipe local por usuario, despacho en Revit mediante `ExternalEvent` y solo dos operaciones. Permite consultar estado y ejecutar una única rutina de solo lectura, fijada por hashes. Aún no se ha instalado en Revit ni probado su ejecución en Dynamo.
- Plantilla Dynamo: un nodo PythonNet3, sin conectores, sin dependencias de paquetes y `RunType=Manual`.
- Intercambio MCP stdio rvt-mcp probado: inicializa v0.6.2 y lista 51 herramientas para los grupos `query,create,modify,view,annotation`. El perfil Codex permite solo una lista curada; no incluye borrado, ejecución arbitraria, ToolBaker ni autoejecución Dynamo.
- Smoke MCP de solo lectura repetible añadido en `tests/run_revit_mcp_readonly_smoke.py`.
- Smoke MCP repetible del generador de rutinas añadido en `tests/run_routine_builder_mcp_smoke.py`.

## Límite funcional actual

Codex puede consultar Revit por `rvt-mcp`, crear rutinas y solicitar al runner una ejecución de solo lectura de `read_active_view_selection`. El runner está compilado, pero la prueba viva terminó en `REVIT_BRIDGE_PING=FAIL`: el add-in no está instalado/cargado en la sesión actual. No hay evidencia todavía de que el runner haya abierto Dynamo o ejecutado el `.dyn`.

El producto no puede recibir y ejecutar con seguridad “cualquier pedido” todavía. Las escrituras tipadas de `rvt-mcp` están disponibles bajo aprobación, pero falta probarlas en un RVT descartable. El runner no acepta código nuevo, no ejecuta rutinas de escritura y solo admite la rutina fija de lectura. La rutina sigue siendo PythonNet3 con `RunType=Manual`; el runner intenta abrirla mediante la API de Dynamo dentro de Revit.

La conexión Codex se escribe como configuración de proyecto en `.codex/config.toml`, que Codex carga solo si se abre y confía este directorio. La configuración curada ofrece consultas y algunas acciones tipadas de creación/edición con aprobación por llamada. La integración MCP→herramientas desde Codex sigue pendiente de una prueba dentro de la app.

## Evidencia de validación

| Comprobación | Resultado |
|---|---|
| Hash publicado de rvt-mcp v0.6.2 | Coincide: `0d6239afcab47c729c30b8c5b763214c20c38c4b72de7e30c5a7191b4131b1ae`. |
| `install.ps1 -WhatIf -Years 2027 -Client none` | Solo previsualizó R27 + servidor en carpetas de usuario; no tocó Revit ni Codex. |
| Instalación real | Add-in y ejecutable presentes en perfil de usuario. Revit no estaba en ejecución durante la instalación. |
| .NET Runtime/SDK y compilación | Runtime 10 presente; SDK ausente. Roslyn de VS Build Tools compiló el add-in (`BimAiDynamoBridge.dll`, 20.992 bytes). Esto confirma compilación, no carga ni ejecución en Revit. |
| MCP de rutinas | Aprobado: initialize, tools/list, create y list; generó `.py`, `.dyn` y manifiesto; el grafo valida como un nodo PythonNet3 Manual. Python 3.14 compiló el script de muestra. |
| Rutina base: pruebas offline | Aprobado: 4 pruebas verifican documento ausente, selección vacía, elementos/categorías y coherencia/hashes entre `.py`, `.dyn` y manifiesto. Usa dobles de Revit; no sustituye una ejecución visual de Dynamo. |
| Protección al guardar escritura | Aprobada: `writes_model=true` sin `confirm_model_write=true` devuelve error y no crea archivos. |
| Revit 2027 con sesión/modelo de prueba | Consulta directa de solo lectura aprobada el 24-09-2026: vista activa y un muro seleccionado respondieron correctamente. No se modificó el documento. Se omitieron IDs/nombres del modelo de esta copia pública. Falta comprobar el MCP desde Codex con el proyecto confiable abierto. |
| Esquemas MCP para perfil inicial | Revisados nombres y esquemas de consultas y operaciones tipadas de nivel, eje, línea (incluye muro), superficie (incluye piso), recinto, instancia, parámetro, tipo, texto, cotas y etiquetas. No equivale a una prueba de ejecución en el modelo. |
| MCP/Revit solo lectura, repetido en esta fase | Aprobado: initialize, descubrimiento de 16 herramientas `query`, vista activa y selección respondieron desde el MCP local. El smoke test solo imprime PASS/FAIL; no guarda contenido del modelo. |
| MCP generador de rutinas, integración viva | Aprobado: initialize/tools/list/create; el test temporal generó `.py`, `.dyn` y manifiesto, validó PythonNet3/Manual y hashes, y limpió los tres archivos. No ejecutó el grafo. |
| Build del bridge propio | Aprobado: `revit/bridge/build.ps1` compiló contra RevitAPI, RevitAPIUI, DynamoRevitDS y .NET Runtime 10. No se instaló en Addins. |
| Contrato MCP del runner Dynamo | Aprobado: initialize/tools/list; anuncia solo ping y ejecución del identificador fijo de lectura. |
| Runner MCP → Revit, smoke vivo | Fallido/pendiente: el contrato MCP respondió, pero `REVIT_BRIDGE_PING=FAIL`; el puente no está cargado en la sesión. No se ejecutó Dynamo ni se cambió el modelo. |
| DynamoCLI 4.0.2 con grafo PythonNet3 sintético | Fallido: el proceso inició el perfil aislado, pero lanzó una aserción ProtoCore antes de crear el XML de evaluaciones. No se considera evidencia de ejecución. |
| Cargar y correr `.dyn` PythonNet3 en Dynamo dentro de Revit | Pendiente de prueba visual. |
| Instalar y cargar el add-in puente | Pendiente; el instalador exige cerrar Revit y el usuario tiene una sesión abierta. |
| Ejecutar `.dyn` PythonNet3 desde el runner | Pendiente; aún no se ha cargado el add-in ni iniciado Dynamo desde Revit. |
| Descargar SDK .NET oficial para compilar runner | Pendiente: la conexión TLS de PowerShell/curl falló con `SEC_E_NO_CREDENTIALS`; no se usaron espejos alternativos. |

## Prueba integrada que falta

1. Guardar el trabajo y cerrar Revit. Ejecutar `revit/bridge/install.ps1` desde esta carpeta; el instalador compila y copia el add-in al perfil actual.
2. Iniciar Revit 2027 con un RVT local nuevo/descartable; aceptar el aviso de confianza si Revit lo muestra.
3. Ejecutar `python tests/run_dynamo_runner_mcp_smoke.py --live`. Primero debe aprobar el ping; después debe abrir la única rutina fijada por hash y devolver `executed_and_verified_read_only`.
4. Si la prueba pasa, confirmar visualmente que Revit sigue estable y que el documento conserva su estado. Revisar el resultado y el log mínimo.
5. Validar Codex con la configuración local; probar después una escritura tipada pequeña y aprobada en el RVT descartable, con consulta posterior de verificación.
6. Solo tras estos gates ampliar el runner a rutinas adicionales; nunca aceptar código arbitrario o transformar la aprobación de una llamada en permiso general.
