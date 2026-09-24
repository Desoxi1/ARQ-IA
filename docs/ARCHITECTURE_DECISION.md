# Decisión de arquitectura del POC

**Actualizado:** 24 de septiembre de 2026
**Estado:** lectura directa Revit ↔ rvt-mcp comprobada; perfil curado de operaciones tipadas preparado; Codex integrado y ejecución MCP → Dynamo pendientes de validación.

## Decisión

Reutilizar `bimwright/rvt-mcp` como autoridad de herramientas Revit tipadas y mantener una pieza propia pequeña para crear/listar rutinas Dynamo/PythonNet3. Codex recibe consultas y una lista curada de operaciones de creación/edición, con aprobación por llamada. El perfil no habilita borrado, código arbitrario, ToolBaker ni ejecución automática de grafos. Los MCP de otros repositorios se evalúan como referencias o servidores especializados separados; no se copian indiscriminadamente al puente principal.

La rutina guardada queda como borrador. El grafo usa `PythonNet3` y modo `Manual`; el usuario revisa el código, lo abre en Dynamo y pulsa **Run**. El servidor de rutinas no ejecuta código. La ejecución automática de un grafo desde MCP no está implementada ni validada, así que esta entrega es una integración parcial del flujo, no una afirmación de que el ciclo completo ya funciona.

## Comparación

| Criterio | Reutilizar rvt-mcp + generador | Bridge MCP/Revit completamente propio |
|---|---|---|
| Revit 2027 | El paquete v0.6.2 incluye plugin compilado R27 y se instaló en perfil de usuario en este PC. Falta cargarlo en una sesión real. | Habría que implementar, compilar, firmar y probar el add-in/IPC desde cero. |
| Consultas y acciones tipadas | Ya incluye herramientas de lectura y varios grupos de creación/edición. La lista expuesta a Codex se limita a nombres conocidos y pide aprobación por llamada. | Habría que reconstruir y mantener las herramientas y probar compatibilidad. |
| Guardar scripts PythonNet3 | Se cubre en el MCP de rutinas del POC; almacena fuente, grafo, alcance, hash y estado. | Se puede integrar todo en una sola solución, con más código de transporte y manejo de errores. |
| Ejecución de `.dyn` desde MCP | No la ofrece rvt-mcp. El grafo generado se ejecuta manualmente desde Dynamo. | Es posible diseñarla con Revit ExternalEvent + API Dynamo, pero requiere prueba real y control de efectos. |
| Dependencias para el usuario | rvt-mcp es self-contained. El generador de rutinas usa Windows PowerShell integrado. No requiere SDK .NET. | Un add-in Revit 2027 propio requiere .NET SDK 10.0.100 para compilarlo. |
| Riesgo inicial | Bajo: dos consultas Revit en lectura; la rutina se guarda, no se ejecuta. | Mayor superficie propia y más rutas de ejecución antes de probar los casos básicos. |

## Qué dice rvt-mcp

El release oficial v0.6.2 declara builds para Revit 2022–2027 e incluye un servidor Windows x64 self-contained. El inventario local de los grupos `query,create,modify,view,annotation` lista 51 herramientas, pero eso no prueba cada operación. El esquema revisado ofrece operaciones tipadas para varios objetos arquitectónicos y anotaciones. El perfil no usa `revit_send_code_to_revit` ni ToolBaker. No hay runner nativo de Dynamo/PythonNet3. Fuentes: [release v0.6.2](https://github.com/bimwright/rvt-mcp/releases/tag/v0.6.2) y [repositorio rvt-mcp](https://github.com/bimwright/rvt-mcp).

El ZIP descargado para este POC pasó SHA-256: `0d6239afcab47c729c30b8c5b763214c20c38c4b72de7e30c5a7191b4131b1ae`. Se instaló solo para Revit 2027 bajo `%APPDATA%` y el servidor bajo `%LOCALAPPDATA%`, usando `-Client none`; no se escribió configuración global de Codex.

## Alcance configurado para Codex

- `rvt-mcp`: consultas y una lista explícita de creación/edición tipada; arranca con `--toolsets query,create,modify,view,annotation --disable-toolbaker`.
- `bim-ai-routines`: `list_saved_routines`, `create_dynamo_routine`; solo escribe archivos bajo `routines/`.
- La configuración solicita aprobación de Codex por llamada. Al aprobar una operación Revit tipada, esta puede cambiar el documento en esa misma llamada; no se debe asumir una vista previa ni rollback general.
- Ninguna herramienta activa ejecuta cuerpos C#, grafos Dynamo o PythonNet3. Las rutinas generadas quedan guardadas como borradores para revisión y ejecución manual.

## Interfaz para arquitectos

**Fase de prueba:** usar Codex como ventana de conversación separada, con esta carpeta abierta como proyecto confiable. El MCP local entrega a Codex las consultas y acciones tipadas de Revit; el MCP propio guarda rutinas Dynamo para abrirlas y ejecutarlas desde Dynamo. Codex solo carga la configuración `.codex/config.toml` del proyecto cuando ese proyecto es confiable.

**Interfaz futura:** si los arquitectos necesitan conversar sin salir del modelado, evaluar un panel acoplable dentro de Revit. Revit permite paneles WPF acoplables y requiere `ExternalEvent` para tramitar acciones desde una interfaz modeless hacia la API. Dynamo también admite extensiones de interfaz como paquetes; eso serviría para controles propios de Dynamo, mientras Revit sigue siendo el anfitrión del modelo.

El panel sería una interfaz nueva para el agente y sus MCP, no una ventana embebida de la aplicación Codex. Para implementarlo se necesitarían UI, transporte al agente, autenticación, cola de solicitudes, aprobación de escrituras, despacho seguro a Revit y verificación posterior. Primero se validará el flujo con Codex; el panel se justificará después de observar tareas repetidas y necesidades reales de los arquitectos.

## Próximo incremento técnico

Para cerrar GPT → MCP → Dynamo → Revit hace falta implementar un runner limitado a rutinas guardadas por identificador, con lista de permitidos, despacho a Revit mediante `ExternalEvent`, verificación del modelo activo, confirmación antes de escritura, captura de errores/resultados y registro mínimo sin secretos. Primero se debe validar desde Codex la consulta de vista/selección; después, en archivo descartable, probar una sola operación tipada y comprobar el resultado. La ejecución Dynamo comienza con `read_active_view_selection`. No habilitar `revit_send_code_to_revit` como sustituto del runner.

Referencias técnicas: [Autodesk Revit 2027 .NET 10](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-WhatsNew/files/GUID-8D7A4715-EAF8-4BD1-BE78-061F900D0BCE.htm), [Dynamo Core 4.0 / PythonNet3](https://dynamobim.org/dynamo-core-4-0-release/), [Revit dockable panes y modeless UI](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Advanced_Topics/Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Dockable_Dialog_Panes_html.html), [Dynamo extensions as packages](https://primer2.dynamobim.org/1_developer_primer_intro/3_developing_for_dynamo/2-extensions-as-packages) y [Codex project MCP config / confianza](https://developers.openai.com/plugins/build/plugins).
