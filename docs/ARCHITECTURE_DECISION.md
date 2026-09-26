# Decisión de arquitectura del POC

**Actualizado:** 26 de septiembre de 2026
**Estado:** generador y contrato MCP del runner probados offline; bridge genérico compilado e instalado por usuario. Se inició una sesión nueva con el RVT descartable, pero el ensamblado del bridge no se cargó y el ping devuelve timeout. Ejecución Dynamo dentro de Revit y carga desde Codex siguen pendientes de prueba viva.

## Decisión

Reutilizar `bimwright/rvt-mcp` para herramientas Revit tipadas y mantener un bridge propio para crear/listar y ejecutar rutinas guardadas Dynamo/PythonNet3. Codex interpreta pedidos, consulta el modelo, prefiere herramientas tipadas y prepara rutinas cuando no hay una acción adecuada. La configuración no habilita borrado ni ToolBaker; cada llamada solicita aprobación.

El generador guarda fuente, grafo de un nodo `PythonNet3` en modo `Manual`, wrapper de resultado JSON y manifiesto con hashes. El bridge acepta rutinas genéricas solo en un RVT local dentro de `revit/test-models/`; las escrituras requieren aprobación explícita y sus `affected_ids` deben coincidir con los IDs de `DocumentChanged`. El add-in está instalado por usuario. La sesión nueva de prueba no cargó el DLL; no hay todavía evidencia de ejecución Dynamo en vivo ni de tareas complejas de extremo a extremo.

## Comparación

| Criterio | Reutilizar rvt-mcp + generador | Bridge MCP/Revit completamente propio |
|---|---|---|
| Revit 2027 | El paquete v0.6.2 incluye plugin compilado R27 y se instaló en perfil de usuario en este PC. Falta cargarlo en una sesión real. | Habría que implementar, compilar, firmar y probar el add-in/IPC desde cero. |
| Consultas y acciones tipadas | Ya incluye herramientas de lectura y varios grupos de creación/edición. La lista expuesta a Codex se limita a nombres conocidos y pide aprobación por llamada. | Habría que reconstruir y mantener las herramientas y probar compatibilidad. |
| Guardar scripts PythonNet3 | Se cubre en el MCP de rutinas del POC; almacena fuente, grafo, alcance, hash y estado. | Se puede integrar todo en una sola solución, con más código de transporte y manejo de errores. |
| Ejecución de `.dyn` desde MCP | No la ofrece rvt-mcp. El add-in propio usa `ExternalEvent` y `DynamoRevit.ExecuteCommand`; está instalado y pendiente de carga/prueba en una sesión con RVT descartable. | Permite ejecutar rutinas guardadas con aprobación y verificación; añade un add-in y debe probarse en Revit real. |
| Dependencias para el usuario | rvt-mcp es self-contained. El generador de rutinas usa Windows PowerShell integrado. | El puente propio usa la API Revit/Dynamo y .NET 10. Compila aquí con Roslyn `csc`; aún falta instalación, carga y prueba en Revit. El usuario no requiere SDK para ejecutar el DLL. |
| Riesgo inicial | Bajo: dos consultas Revit en lectura; la rutina se guarda, no se ejecuta. | Mayor superficie propia y más rutas de ejecución antes de probar los casos básicos. |

## Qué dice rvt-mcp

El release oficial v0.6.2 declara builds para Revit 2022–2027 e incluye un servidor Windows x64 self-contained. El inventario local de los grupos `query,create,modify,view,annotation` lista 51 herramientas, pero eso no prueba cada operación. El esquema revisado ofrece operaciones tipadas para varios objetos arquitectónicos y anotaciones. El perfil no usa `revit_send_code_to_revit` ni ToolBaker. No hay runner nativo de Dynamo/PythonNet3. Fuentes: [release v0.6.2](https://github.com/bimwright/rvt-mcp/releases/tag/v0.6.2) y [repositorio rvt-mcp](https://github.com/bimwright/rvt-mcp).

El ZIP descargado para este POC pasó SHA-256: `0d6239afcab47c729c30b8c5b763214c20c38c4b72de7e30c5a7191b4131b1ae`. Se instaló solo para Revit 2027 bajo `%APPDATA%` y el servidor bajo `%LOCALAPPDATA%`, usando `-Client none`; no se escribió configuración global de Codex.

## Alternativa Autodesk para evaluar antes de escribir un runner

Autodesk publicó Dynamo Core 4.2 el 26 de agosto de 2026. Incluye Autodesk Assistant y DynamoMCP integrados para consultar y editar el grafo, ejecutar grafos confiables, leer advertencias y guardar. La publicación indica que 4.2 estaba disponible primero en Dynamo Sandbox y que las integraciones con aplicaciones anfitrionas llegarían después. También aclara que el Assistant dentro de Dynamo no cambia directamente documentos Revit; en Revit se usa su propio Assistant. Por ello, esta publicación no demuestra que Codex pueda conectarse a DynamoMCP ni reemplaza el puente de ejecución de Revit de este POC. [Notas oficiales de Dynamo Core 4.2](https://dynamobim.org/dynamo-core-4-2-release/)

El Dynamo instalado en este PC es 4.0.2. Para considerar DynamoMCP como reemplazo habría que verificar una integración de Revit que lo incluya y una conexión MCP desde Codex; ninguna está demostrada aquí. Autodesk también publica Revit Public MCP Server, pero su guía actual lo limita a Revit 2027.2. Este PC tiene Revit 2027.0/build 27.0.20.10, por lo que ese add-in no se instalará en esta sesión. La guía documenta ejemplos de clientes Claude Desktop y Cursor, no una conexión probada con Codex. [Guía oficial del Revit Public MCP Server](https://help.autodesk.com/view/ADSKMCP/ENU/?guid=ADSKMCP_RevitMcp_setting_up_revit_mcp_server_html)

Autodesk también publica su Revit Public MCP Server, pero su página de configuración limita ese add-in a Revit 2027.2 y lo presenta como Technical Preview con perfiles read-only, stable-write y experimental. La instalación presente es Revit 2027.0/build 27.0.20.10; por eso ese servidor no se instala ni se mezcla con `rvt-mcp` en esta sesión. [Guía oficial del Revit Public MCP Server](https://help.autodesk.com/view/ADSKMCP/ENU/?guid=ADSKMCP_RevitMcp_setting_up_revit_mcp_server_html)

## Decisión de siguiente prueba

Mantener `rvt-mcp` para herramientas tipadas y Codex como interfaz. El siguiente gate es conseguir que el add-in cargue en la sesión de prueba ya iniciada y que `ping_revit_dynamo_bridge` responda; luego ejecutar `read_test_model_summary` por MCP. Si Dynamo devuelve el resultado y el documento queda intacto, probar una escritura en el RVT descartable y volver a leer el parámetro. Revaluar DynamoMCP cuando su integración con Revit y conexión desde Codex estén disponibles. La prueba fallida con `DynamoCLI` no habilita ejecución.

## Alcance configurado para Codex

- `rvt-mcp`: consultas y una lista explícita de creación/edición tipada; arranca con `--toolsets query,create,modify,view,annotation --disable-toolbaker`.
- `bim-ai-routines`: `list_saved_routines`, `create_dynamo_routine`; crea fuente, grafo y manifiesto bajo `routines/`.
- `bim-ai-dynamo-runner`: ping, rutina read-only fijada y `execute_saved_routine`; esta última solo opera en RVT locales bajo `revit/test-models/`. Las escrituras requieren confirmación y aprobación MCP.
- La configuración solicita aprobación de Codex por llamada. Al aprobar una operación Revit tipada, esta puede cambiar el documento en esa misma llamada; no se debe asumir una vista previa ni rollback general.
- La rutina PythonNet3 corre dentro del proceso Revit con los permisos del usuario; la ruta de documento no es un sandbox de archivos o red. Revisa el script antes de ejecutarlo.

## Interfaz para arquitectos

**Fase de prueba:** usar Codex como ventana de conversación separada, con esta carpeta abierta como proyecto confiable. El MCP local entrega a Codex las consultas y acciones tipadas de Revit; el MCP propio guarda rutinas y presenta ejecución de lectura y rutinas guardadas por add-in. El add-in está instalado, pero falta resolver su carga y validarlo en vivo. Codex solo carga la configuración `.codex/config.toml` del proyecto cuando ese proyecto es confiable.

**Interfaz futura:** si los arquitectos necesitan conversar sin salir del modelado, evaluar un panel acoplable dentro de Revit. Revit permite paneles WPF acoplables y requiere `ExternalEvent` para tramitar acciones desde una interfaz modeless hacia la API. Dynamo también admite extensiones de interfaz como paquetes; eso serviría para controles propios de Dynamo, mientras Revit sigue siendo el anfitrión del modelo.

El panel sería una interfaz nueva para el agente y sus MCP, no una ventana embebida de la aplicación Codex. Para implementarlo se necesitarían UI, transporte al agente, autenticación, cola de solicitudes, aprobación de escrituras, despacho seguro a Revit y verificación posterior. Primero se validará el flujo con Codex; el panel se justificará después de observar tareas repetidas y necesidades reales de los arquitectos.

## Próximo incremento técnico

Para cerrar GPT → MCP → Dynamo → Revit falta resolver la carga del add-in en la sesión abierta con el RVT descartable y validar el ping. Después se prueba el resumen read-only y una escritura con lectura posterior. Las solicitudes complejas se componen en pasos verificados y aprobados. No habilitar `revit_send_code_to_revit` como sustituto del runner.

Referencias técnicas: [Autodesk Revit 2027 .NET 10](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-WhatsNew/files/GUID-8D7A4715-EAF8-4BD1-BE78-061F900D0BCE.htm), [Dynamo Core 4.0 / PythonNet3](https://dynamobim.org/dynamo-core-4-0-release/), [Revit dockable panes y modeless UI](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Advanced_Topics/Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Dockable_Dialog_Panes_html.html), [Dynamo extensions as packages](https://primer2.dynamobim.org/1_developer_primer_intro/3_developing_for_dynamo/2-extensions-as-packages) y [Codex project MCP config / confianza](https://developers.openai.com/plugins/build/plugins).
