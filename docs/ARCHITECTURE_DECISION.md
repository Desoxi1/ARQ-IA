# Decisión de arquitectura del POC

**Actualizado:** 2 de octubre de 2026
**Estado al 2-10-2026:** 49 pruebas unitarias pasan. El smoke de Codex app-server + MCP + adjuntos había pasado anteriormente; hoy volvió a pasar `--mcp` en modo de solo lectura. La build Dynamo `0.2.0` tiene SHA-256 `99edb5d37b33e7154a236425b7b5eccdd5511aaee0c496a0d8ae897c9850e86e`. La sesión actual de Dynamo no cargó el panel. Se compiló el instalador para usar una DLL versionada en la carpeta oficial del host y actualizar el manifiesto sin reemplazar archivos en uso; la elevación requerida para `Program Files` no se completó y la instalación quedó pendiente. El add-in nativo Revit compiló con SHA-256 `12fc55814122aff4420e2fa46df17fa31683f066cf27422108cee70a4aa6d58a` y quedó instalado por usuario para la siguiente sesión. El bridge sigue staged con SHA-256 `adf38e853f958fe37d866b8b5c21da2c071f4afabf0f71e503a52ead903f053c`; todo requiere validación visual y viva tras reiniciar Revit.

## Decisión

Reutilizar `bimwright/rvt-mcp` para herramientas Revit tipadas y mantener un bridge propio para crear/listar y ejecutar rutinas guardadas Dynamo/PythonNet3. La interfaz tiene dos entradas al mismo asistente: panel acoplable en Revit 2027 bajo la cinta **BIM-AI** y extensión lateral **Asistente BIM** en Dynamo 4.0.2. Ambas compilan los mismos archivos de chat, cliente Codex y adjuntos, no referencian el ensamblado del otro host y usan la configuración local de Codex/MCP. La ficha muestra **Gestor: Branko Plaza**. Codex interpreta pedidos, consulta el modelo, prefiere herramientas tipadas y prepara rutinas cuando no hay una acción adecuada. La configuración no habilita borrado ni ToolBaker; cada llamada solicita aprobación.

El add-in de Revit registra un `IDockablePaneProvider` y un comando de cinta. Su DLL se instala por usuario bajo `%APPDATA%\Autodesk\Revit\Addins\2027`; puede prepararse mientras Revit está abierto porque usa un nombre de DLL por hash, pero la pestaña aparece al iniciar una sesión nueva. El paquete portable no incluye `RevitAPI.dll`, `RevitAPIUI.dll` ni ensamblados de Dynamo: los resuelve el host instalado.

El generador guarda fuente, grafo de un nodo `PythonNet3` en modo `Manual`, wrapper de resultado JSON y manifiesto con hashes. El bridge ejecuta sobre un RVT guardado, idle y local dentro de `revit/test-models/`. Dynamo puede dejar la copia de prueba marcada como modificada en memoria al iniciar: el código actualizado permite esa línea base, compara `DocumentChanged` durante la rutina y exige que una rutina de solo lectura no agregue cambios. Toda rutina genérica requiere autorización explícita de posible escritura; sus `affected_ids` deben coincidir con los IDs observados por Revit `DocumentChanged`. Los metadatos no demuestran que el código sea de solo lectura. El DLL actualizado ya está instalado para la siguiente sesión; el DLL antiguo sigue en la sesión Revit abierta hasta reiniciarla.

## Comparación

| Criterio | Reutilizar rvt-mcp + generador | Bridge MCP/Revit completamente propio |
|---|---|---|
| Revit 2027 | El paquete v0.6.2 incluye plugin compilado R27; lecturas en serie de vista/selección pasaron incluso con Dynamo abierto. La llamada originada en el panel sigue sin comprobar. | Habría que implementar, compilar, firmar y probar el add-in/IPC desde cero. |
| Consultas y acciones tipadas | Ya incluye herramientas de lectura y varios grupos de creación/edición. La lista expuesta a Codex se limita a nombres conocidos y pide aprobación por llamada. | Habría que reconstruir y mantener las herramientas y probar compatibilidad. |
| Guardar scripts PythonNet3 | Se cubre en el MCP de rutinas del POC; almacena fuente, grafo, alcance, hash y estado. | Se puede integrar todo en una sola solución, con más código de transporte y manejo de errores. |
| Ejecución de `.dyn` desde MCP | No la ofrece rvt-mcp. El add-in propio usa `ExternalEvent` y DynamoRevit. La sesión actual rechazó el gate de modelo modificado antes de ejecutar PythonNet3; el ajuste está staged y falta cargarlo tras reiniciar Revit y probarlo en un RVT descartable. | Permite ejecutar rutinas guardadas con aprobación y verificación; añade un add-in y debe probarse en Revit real. |
| Dependencias para el usuario | rvt-mcp es self-contained. El generador de rutinas usa Windows PowerShell integrado. | El puente propio usa la API Revit/Dynamo y .NET 10. Compila aquí con Roslyn `csc`; el usuario no requiere SDK para ejecutar el DLL. La instalación del panel Dynamo escribe en la carpeta oficial de Revit 2027 y requiere elevación una vez. |
| Riesgo inicial | Bajo para consultas de solo lectura; rutinas PythonNet3 quedan bajo confirmación y control de deltas. | Mayor superficie propia. La inicialización UI-less marcó el modelo como modificado; ahora se requiere Dynamo UI y se valida el delta en una copia de prueba. Falta verificar el nuevo flujo en vivo. |

**Decisión de interfaz:** se conserva una extensión dedicada de Dynamo y se añade un panel nativo de Revit. Dynamo da acceso al grafo; Revit da acceso directo al chat aunque Dynamo no esté abierto. Ambos comparten implementación y conexión de Codex/MCP, mientras las entradas permanecen compatibles con su host. La compilación del add-in nativo se validó contra las API instaladas de Revit 2027; falta verificar carga visual al iniciar una sesión nueva.

## Qué dice rvt-mcp

El release oficial v0.6.2 declara builds para Revit 2022–2027 e incluye un servidor Windows x64 self-contained. El inventario local de los grupos `query,create,modify,view,annotation` lista 51 herramientas, pero eso no prueba cada operación. El esquema revisado ofrece operaciones tipadas para varios objetos arquitectónicos y anotaciones. El perfil no usa `revit_send_code_to_revit` ni ToolBaker. No hay runner nativo de Dynamo/PythonNet3. Fuentes: [release v0.6.2](https://github.com/bimwright/rvt-mcp/releases/tag/v0.6.2) y [repositorio rvt-mcp](https://github.com/bimwright/rvt-mcp).

El ZIP descargado para este POC pasó SHA-256: `0d6239afcab47c729c30b8c5b763214c20c38c4b72de7e30c5a7191b4131b1ae`. Se instaló solo para Revit 2027 bajo `%APPDATA%` y el servidor bajo `%LOCALAPPDATA%`, usando `-Client none`; no se escribió configuración global de Codex.

## Alternativa Autodesk para evaluar antes de escribir un runner

Autodesk publicó Dynamo Core 4.2 el 26 de agosto de 2026. Incluye Autodesk Assistant y DynamoMCP integrados para consultar y editar el grafo, ejecutar grafos confiables, leer advertencias y guardar. La publicación indica que 4.2 estaba disponible primero en Dynamo Sandbox y que las integraciones con aplicaciones anfitrionas llegarían después. También aclara que el Assistant dentro de Dynamo no cambia directamente documentos Revit; en Revit se usa su propio Assistant. Por ello, esta publicación no demuestra que Codex pueda conectarse a DynamoMCP ni reemplaza el puente de ejecución de Revit de este POC. [Notas oficiales de Dynamo Core 4.2](https://dynamobim.org/dynamo-core-4-2-release/)

El Dynamo instalado en este PC es 4.0.2. Para considerar DynamoMCP como reemplazo habría que verificar una integración de Revit que lo incluya y una conexión MCP desde Codex; ninguna está demostrada aquí. Autodesk también publica Revit Public MCP Server, pero su guía actual lo limita a Revit 2027.2. Este PC tiene Revit 2027.0/build 27.0.20.10, por lo que ese add-in no se instalará en esta sesión. La guía documenta ejemplos de clientes Claude Desktop y Cursor, no una conexión probada con Codex. [Guía oficial del Revit Public MCP Server](https://help.autodesk.com/view/ADSKMCP/ENU/?guid=ADSKMCP_RevitMcp_setting_up_revit_mcp_server_html)

Autodesk también publica su Revit Public MCP Server, pero su página de configuración limita ese add-in a Revit 2027.2 y lo presenta como Technical Preview con perfiles read-only, stable-write y experimental. La instalación presente es Revit 2027.0/build 27.0.20.10; por eso ese servidor no se instala ni se mezcla con `rvt-mcp` en esta sesión. [Guía oficial del Revit Public MCP Server](https://help.autodesk.com/view/ADSKMCP/ENU/?guid=ADSKMCP_RevitMcp_setting_up_revit_mcp_server_html)

## Decisión de siguiente prueba

Mantener `rvt-mcp` para herramientas tipadas, el panel **Asistente BIM** dentro de Dynamo y el bridge propio para rutinas guardadas. El ping y las consultas rvt-mcp ya responden con Dynamo abierto; lo que queda es reiniciar Revit para cargar el bridge staged, ejecutar `read_active_view_selection` y comprobar que no agrega cambios. Después verificar la primera llamada desde el panel. Solo entonces probar el resumen genérico y escrituras aprobadas en la copia. Revaluar DynamoMCP cuando su integración con Revit y conexión desde Codex estén disponibles. La prueba fallida con `DynamoCLI` no habilita ejecución.

## Alcance configurado para Codex

- `rvt-mcp`: consultas y una lista explícita de creación/edición tipada; arranca con `--toolsets query,create,modify,view,annotation --disable-toolbaker`.
- `bim-ai-routines`: `list_saved_routines`, `create_dynamo_routine`; crea fuente, grafo y manifiesto bajo `routines/`.
- `bim-ai-dynamo-runner`: ping, rutina read-only fijada y `execute_saved_routine`; esta última solo opera en RVT locales bajo `revit/test-models/`. Toda ejecución genérica requiere confirmación de posible escritura y aprobación MCP.
- La configuración solicita aprobación de Codex por llamada. Al aprobar una operación Revit tipada, esta puede cambiar el documento en esa misma llamada; no se debe asumir una vista previa ni rollback general.
- La rutina PythonNet3 corre dentro del proceso Revit con los permisos del usuario; la ruta de documento no es un sandbox de archivos o red. Revisa el script antes de ejecutarlo.

## Interfaz para arquitectos

**Prototipo actual:** el paquete `0.2.0` entrega el panel **Asistente BIM** para Dynamo 4.0.2 y un add-in nativo acoplable para Revit 2027, con chat local, botón **Ver** para consultar contexto Revit, selección/arrastre/pegado de adjuntos y miniaturas. Las imágenes llegan al modelo Codex; texto compatible se incorpora como referencia; los demás documentos se mantienen como archivos de solo lectura dentro de la carpeta temporal del proyecto. El contenido adjunto no se ejecuta. El smoke de Codex app-server/MCP pasó en modo solo lectura. El add-in Revit compila contra la API local con cero advertencias ni errores y no copia dependencias de Dynamo; su instalador por usuario verifica el hash y registra Branko Plaza como fabricante/gestor. Revit permanece abierto, así que la instalación nativa se prepara para la siguiente sesión. El panel Dynamo aún requiere completar la elevación para quedar instalado en la ruta oficial. Ningún panel se ha vuelto a confirmar visualmente en la sesión actual. Codex CLI autenticado y configuración MCP del proyecto son requisitos; no se incrusta la web de ChatGPT ni se usa una API key adicional.

**Distribución:** Dynamo 4.0.2 de Revit 2027 descubre View Extensions mediante XML en `C:\Program Files\Autodesk\Revit 2027\AddIns\DynamoForRevit\viewExtensions`. El instalador coloca el DLL junto a las bibliotecas de Dynamo y registra `..\BimAiDynamoAssistant.dll` en el manifiesto. El add-in Revit se registra por usuario con `.addin` en `%APPDATA%\Autodesk\Revit\Addins\2027`; crea la pestaña **BIM-AI** y botón **Abrir chat**. Ambos declaran **Branko Plaza** como responsable.

## Próximo incremento técnico

Para cerrar Codex → MCP → Dynamo → Revit falta reiniciar Revit para cargar el bridge staged, validar el grafo read-only fijado y enviar una consulta desde el panel. Después probar el resumen genérico y finalmente una escritura controlada con lectura posterior. Las solicitudes complejas se componen en pasos verificados y aprobados. No habilitar `revit_send_code_to_revit` como sustituto del runner.

Referencias técnicas: [Autodesk Revit 2027 .NET 10](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-WhatsNew/files/GUID-8D7A4715-EAF8-4BD1-BE78-061F900D0BCE.htm), [Dynamo Core 4.0 / PythonNet3](https://dynamobim.org/dynamo-core-4-0-release/), [Revit dockable panes y modeless UI](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Advanced_Topics/Revit_API_Revit_API_Developers_Guide_Advanced_Topics_Dockable_Dialog_Panes_html.html), [Dynamo extensions as packages](https://primer2.dynamobim.org/1_developer_primer_intro/3_developing_for_dynamo/2-extensions-as-packages) y [Codex project MCP config / confianza](https://developers.openai.com/plugins/build/plugins).
