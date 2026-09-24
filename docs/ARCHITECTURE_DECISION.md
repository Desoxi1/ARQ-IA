# Decisión de arquitectura del POC

**Actualizado:** 24 de septiembre de 2026
**Estado:** lectura directa Revit ↔ rvt-mcp comprobada; generador de rutinas aprobado offline; runner propio compilado pero no instalado. Ejecución MCP → Dynamo y la integración desde Codex siguen pendientes de prueba viva.

## Decisión

Reutilizar `bimwright/rvt-mcp` como autoridad de herramientas Revit tipadas y mantener una pieza propia pequeña para crear/listar rutinas Dynamo/PythonNet3. Codex recibe consultas y una lista curada de operaciones de creación/edición, con aprobación por llamada. El perfil no habilita borrado, código arbitrario, ToolBaker ni ejecución automática de grafos. Los MCP de otros repositorios se evalúan como referencias o servidores especializados separados; no se copian indiscriminadamente al puente principal.

La rutina guardada queda como borrador. El grafo usa `PythonNet3` y modo `Manual`. El runner add-in compilado intenta ejecutarlo desde Revit, pero solo admite el paquete `read_active_view_selection` fijado por hashes y únicamente cuando el documento está guardado y sin cambios pendientes. El smoke vivo no encontró el puente instalado/cargado; por tanto no hay aún evidencia de que Dynamo se ejecute desde MCP. Esta entrega no es todavía un asistente que ejecute solicitudes complejas de extremo a extremo.

## Comparación

| Criterio | Reutilizar rvt-mcp + generador | Bridge MCP/Revit completamente propio |
|---|---|---|
| Revit 2027 | El paquete v0.6.2 incluye plugin compilado R27 y se instaló en perfil de usuario en este PC. Falta cargarlo en una sesión real. | Habría que implementar, compilar, firmar y probar el add-in/IPC desde cero. |
| Consultas y acciones tipadas | Ya incluye herramientas de lectura y varios grupos de creación/edición. La lista expuesta a Codex se limita a nombres conocidos y pide aprobación por llamada. | Habría que reconstruir y mantener las herramientas y probar compatibilidad. |
| Guardar scripts PythonNet3 | Se cubre en el MCP de rutinas del POC; almacena fuente, grafo, alcance, hash y estado. | Se puede integrar todo en una sola solución, con más código de transporte y manejo de errores. |
| Ejecución de `.dyn` desde MCP | No la ofrece rvt-mcp. Un add-in separado está compilado con `ExternalEvent` y `DynamoRevit.ExecuteCommand`; aún no se cargó ni probó. | Permite una lista explícita de rutinas conocidas, pero añade un add-in y requiere pruebas dentro de Revit. |
| Dependencias para el usuario | rvt-mcp es self-contained. El generador de rutinas usa Windows PowerShell integrado. | El puente propio usa la API Revit/Dynamo y .NET 10. Compila aquí con Roslyn `csc`; aún falta instalación, carga y prueba en Revit. El usuario no requiere SDK para ejecutar el DLL. |
| Riesgo inicial | Bajo: dos consultas Revit en lectura; la rutina se guarda, no se ejecuta. | Mayor superficie propia y más rutas de ejecución antes de probar los casos básicos. |

## Qué dice rvt-mcp

El release oficial v0.6.2 declara builds para Revit 2022–2027 e incluye un servidor Windows x64 self-contained. El inventario local de los grupos `query,create,modify,view,annotation` lista 51 herramientas, pero eso no prueba cada operación. El esquema revisado ofrece operaciones tipadas para varios objetos arquitectónicos y anotaciones. El perfil no usa `revit_send_code_to_revit` ni ToolBaker. No hay runner nativo de Dynamo/PythonNet3. Fuentes: [release v0.6.2](https://github.com/bimwright/rvt-mcp/releases/tag/v0.6.2) y [repositorio rvt-mcp](https://github.com/bimwright/rvt-mcp).

El ZIP descargado para este POC pasó SHA-256: `0d6239afcab47c729c30b8c5b763214c20c38c4b72de7e30c5a7191b4131b1ae`. Se instaló solo para Revit 2027 bajo `%APPDATA%` y el servidor bajo `%LOCALAPPDATA%`, usando `-Client none`; no se escribió configuración global de Codex.

## Alternativa Autodesk para evaluar antes de escribir un runner

Autodesk documenta `DynamoMCP` desde Dynamo 4.2.1 y dice que Autodesk Assistant puede usar las herramientas de Dynamo cuando Dynamo está abierto dentro de Revit. El Dynamo instalado aquí es 4.0.2; Codex como cliente externo todavía no se ha validado contra ese MCP. La integración oficial puede evitar mantener un runner propio si expone al cliente y las operaciones que necesita este flujo. [Notas oficiales de Dynamo 4.2.1](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-WhatsNew/files/GUID-ECD5B134-5A8A-4223-9DBA-565D708A4F47.htm)

Autodesk también publica su Revit Public MCP Server, pero su página de configuración limita ese add-in a Revit 2027.2 y lo presenta como Technical Preview con perfiles read-only, stable-write y experimental. La instalación presente es Revit 2027.0/build 27.0.20.10; por eso ese servidor no se instala ni se mezcla con `rvt-mcp` en esta sesión. [Guía oficial del Revit Public MCP Server](https://help.autodesk.com/view/ADSKMCP/ENU/?guid=ADSKMCP_RevitMcp_setting_up_revit_mcp_server_html)

## Decisión de siguiente prueba

Mantener `rvt-mcp` para las herramientas tipadas y Codex como interfaz. El prototipo de runner propio ya está escrito y compilado; el siguiente gate es instalarlo cuando Revit esté cerrado e iniciar una plantilla descartable. Antes de ampliar su lista de rutinas, comprobar si Revit 2027.2 + DynamoMCP permite conectar Codex con aprobación y resultado estructurado. La prueba fallida con `DynamoCLI` no habilita ejecución.

## Alcance configurado para Codex

- `rvt-mcp`: consultas y una lista explícita de creación/edición tipada; arranca con `--toolsets query,create,modify,view,annotation --disable-toolbaker`.
- `bim-ai-routines`: `list_saved_routines`, `create_dynamo_routine`; solo escribe archivos bajo `routines/`.
- La configuración solicita aprobación de Codex por llamada. Al aprobar una operación Revit tipada, esta puede cambiar el documento en esa misma llamada; no se debe asumir una vista previa ni rollback general.
- El runner configurado expone únicamente `ping_revit_dynamo_bridge` y `execute_saved_readonly_routine`, con la rutina y hashes fijos. No acepta cuerpos C#, grafos arbitrarios o escrituras PythonNet3.

## Interfaz para arquitectos

**Fase de prueba:** usar Codex como ventana de conversación separada, con esta carpeta abierta como proyecto confiable. El MCP local entrega a Codex las consultas y acciones tipadas de Revit; el MCP propio guarda rutinas y presenta una única ejecución de lectura por add-in. El add-in aún debe instalarse y probarse. Codex solo carga la configuración `.codex/config.toml` del proyecto cuando ese proyecto es confiable.

**Interfaz futura:** si los arquitectos necesitan conversar sin salir del modelado, evaluar un panel acoplable dentro de Revit. Revit permite paneles WPF acoplables y requiere `ExternalEvent` para tramitar acciones desde una interfaz modeless hacia la API. Dynamo también admite extensiones de interfaz como paquetes; eso serviría para controles propios de Dynamo, mientras Revit sigue siendo el anfitrión del modelo.

El panel sería una interfaz nueva para el agente y sus MCP, no una ventana embebida de la aplicación Codex. Para implementarlo se necesitarían UI, transporte al agente, autenticación, cola de solicitudes, aprobación de escrituras, despacho seguro a Revit y verificación posterior. Primero se validará el flujo con Codex; el panel se justificará después de observar tareas repetidas y necesidades reales de los arquitectos.

## Próximo incremento técnico

Para cerrar GPT → MCP → Dynamo → Revit falta cargar y probar el runner ya compilado sobre un archivo descartable; comprobar que Dynamo devuelve la vista/selección y que Revit queda sin modificaciones. Después se puede diseñar una rutina de escritura mínima con lista de permitidos, aprobación explícita, verificación posterior y registro reducido. Las solicitudes complejas deben componerse como pasos aprobados y verificados, no como código arbitrario. No habilitar `revit_send_code_to_revit` como sustituto del runner.

Referencias técnicas: [Autodesk Revit 2027 .NET 10](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-WhatsNew/files/GUID-8D7A4715-EAF8-4BD1-BE78-061F900D0BCE.htm), [Dynamo Core 4.0 / PythonNet3](https://dynamobim.org/dynamo-core-4-0-release/), [Revit dockable panes y modeless UI](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Advanced_Topics/Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Dockable_Dialog_Panes_html.html), [Dynamo extensions as packages](https://primer2.dynamobim.org/1_developer_primer_intro/3_developing_for_dynamo/2-extensions-as-packages) y [Codex project MCP config / confianza](https://developers.openai.com/plugins/build/plugins).
