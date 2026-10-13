# Estado de ARQ-IA

<!-- PRODUCT-STATE-20261013:BEGIN -->

### Publicación, limpieza y catálogo — actualización final

- Se creó y publicó el repositorio público [Desoxi1/ARQ-IA](https://github.com/Desoxi1/ARQ-IA). La rama `main` y el remoto `origin` usan la cuenta autenticada `Desoxi1`; el push se ejecutó con Git local, sin navegador ni credencial guardada en el repositorio.
- Se retiraron los ZIP/builds/vistas previas antiguos de `artifacts/` y las salidas temporales de `logs/materials/` y `logs/runner-results/`. Se preservaron `.arq-ia/` y `.codex/` locales; no forman parte del commit ni del paquete.
- `materials/base.csv` conserva 208 nombres únicos, ordenados, con clase, categoría, color y transparencia. La consulta local confirmó `total=208`, `returned=208`, `limit_hit=false`, `skipped=0`. IDs, assets, usos, cantidades, takeoff y datos del RVT quedan fuera del repositorio; cada equipo puede añadir preferencias en su catálogo ignorado local.
- La revisión de publicación del paquete pasó para 95 archivos: sin secretos, rutas personales ni modelos/artefactos locales. No se agregaron pruebas nuevas en esta limpieza.
- El diseño del panel está compilado y preparado en los binarios del proyecto, pero falta cargarlo y comprobarlo visualmente en una sesión nueva de Revit/Dynamo. La ejecución genérica y las escrituras complejas de Dynamo siguen sin aceptación en vivo.
- Los manifiestos muestran `VendorDescription=Branko Plaza`; las DLL siguen sin firma confiable, por lo que el diálogo de Revit puede seguir indicando fabricante desconocido. Cambiar ese aviso requiere un certificado de firma confiable.
- Los apartados siguientes conservan el registro histórico del desarrollo y pueden describir estados ya superados; para el estado vigente manda esta actualización.

## Registro histórico de avances — 2026-10-13

### Avance posterior — historial local y contexto por proyecto

- Revit y Dynamo comparten un botón de historial local. Las conversaciones se guardan por proyecto bajo `.arq-ia/conversations/`; se conservan mensajes y resultados MCP acotados, y de los adjuntos solo el nombre. Al abrir una conversación previa se restaura la interfaz; al continuar se inicia una sesión Codex nueva con hasta 24 mensajes/8.000 caracteres recientes y se consulta el estado Revit de nuevo. Conversaciones anteriores a esta versión no se importan.
- El encabezado compartido usa solo “ARQ-IA” y “Asistente BIM · Revit 2027”; no muestra el nombre personal junto al logo. El nombre **Branko Plaza** queda en `VendorDescription` de los manifiestos `.addin`.
- `materials/base.csv` incorpora una base curada de 208 filas con nombre, clase, categoría, color y transparencia; se omitieron IDs, assets, nombre del modelo, uso y cantidades. Cada PC puede extender sus preferencias en `.arq-ia/materials/local.csv`; el inventario vivo del modelo y la base común nunca se sustituyen automáticamente.
- Si falta `.arq-ia/model-profile.md`, el primer envío pide a Codex crear una ficha local compacta del documento Revit activo mediante herramientas MCP de solo lectura. Tras la respuesta, el panel muestra si la ficha sigue pendiente. Este es un flujo solicitado al modelo, no una captura determinista; no se probó contra una sesión viva de Revit. El RVT no se modifica.
- `.arq-ia/` está ignorada por Git. El historial, el perfil del modelo y las notas locales no entran en el clon ni en el paquete compartido. Las instrucciones permiten guardar correcciones explícitas y resultados verificados en `project-memory.md`; esto no entrena el modelo.
- Dynamo y Revit compilaron con 0 errores y 0 advertencias. El smoke aislado de historial pasó recarga, título, continuación, rechazo de IDs con forma de ruta y retención solo del nombre de adjunto. El módulo de paquete Dynamo pasó 9 pruebas. SHA-256 distribuidos e instalados: Dynamo `74bd8582397b5a9b466e4dd3f755104fb55639f6f3cf2211b59753ae4dee7e1f`; Revit `bffb5582a036f352195e55afbfdcf38ff6c911e14876459687da6deb56f8adbd`.
- **No verificado en vivo:** panel cargado en Revit/Dynamo y primer inventario de un modelo. Los dos componentes están instalados en modo `STAGED_RESTART_REQUIRED`; Revit ya abierto conserva el panel anterior hasta una sesión nueva. El checkout sigue sin remoto Git configurado, así que no se publicó.

- La interfaz compartida de Revit y Dynamo muestra un logo vectorial A celeste/azul, el título **Asistente BIM · Revit 2027**, botón de adjuntos `+`, selector de modelo **Automático/manual** y control **Acceso completo** al pie. Se retiró el rótulo inicial **MCP sin verificar**. La elección manual de modelo se conserva para el próximo envío.
- Dynamo y Revit compilaron con 0 errores y 0 advertencias. Las 55 pruebas unitarias pasan. El smoke real del cliente Codex pasó acceso completo, llamada MCP, guardado/listado de rutina y adjuntos de imagen/texto sin aprobaciones; limpió su archivo temporal.
- MCP en vivo sobre el modelo de inspección: inicialización, descubrimiento de 16 herramientas, vista activa y selección pasaron en modo lectura. No se escribió ni guardó nada en el modelo.
- Los binarios revisados quedaron preparados en las ubicaciones del usuario: Dynamo `a52560f05f5a4df4dbb9239d3f2f41209e8df6e35ffc07ae4e0963fc61822bee`; Revit `a795d0e1a414fd67624909ce215b7c355cc4769ad045c680cb7f3a5deb7eec35`. **Se requiere una sola sesión nueva de Revit/Dynamo para ver el diseño actualizado**; el proceso actual conserva los paneles ya cargados. El diagnóstico marca 15 PASS, 4 WAIT, 0 WARN y 0 FAIL; los WAIT son el reinicio y la falta de Dynamo abierto en el RVT descartable.
- Los manifiestos `.addin` del bridge y del panel contienen `VendorDescription=Branko Plaza`. El aviso de editor desconocido persiste porque la DLL no está firmada por un certificado de editor confiable. No se fabricó una firma ni se instaló una autoridad de confianza local. Un certificado de firma de código confiable es un requisito pendiente para cambiar el nombre que muestra ese aviso.
- El inventario privado de materiales y categorías del modelo tiene una copia maestra bajo `%LOCALAPPDATA%\ARQ-IA\ModelAnalysis\...` y una referencia local ignorada en `.arq-ia/` para que Codex la consulte en este PC. Ninguna copia se agrega al repositorio ni al paquete portable. Es contexto local, no entrenamiento de Codex ni una especificación de materiales.
- La limpieza de los ZIP y salidas viejas de `artifacts/` no se aplicó: la política automática de ejecución rechazó las operaciones de borrado. Los archivos siguen ignorados por Git y no se incluyen en el paquete.
- La publicación Git sigue pendiente: este clon no tiene remoto configurado ni `gh` autenticado. No se abrió el navegador.

## Estado reconciliado — 2026-10-13

**Corrección de carga de Dynamo y acceso Codex (13-10).** El log más reciente identificó el fallo: Dynamo había cargado `BimAiDynamoAssistant.1a341d349d4afff0.dll`, mientras el manifiesto pedía otra compilación con la misma identidad .NET; por eso la extensión no aparecía en **Extensiones**. El instalador ahora deja una única DLL canónica, retira copias ARQ-IA viejas y registra el menú antes de construir el panel. El intento de actualizar con Revit abierto se detuvo sin cambiar archivos porque Windows mantiene bloqueada la DLL activa; el nuevo manifiesto Dynamo aún está pendiente de instalar.

El chat ahora tiene los modos **Acceso controlado** y **Acceso completo** por sesión. Completo envía `approvalPolicy=never` y `danger-full-access` al app-server, responde automáticamente solicitudes MCP vacías y no cambia la configuración global de Codex. El smoke con Codex real creó/listó una rutina y procesó adjuntos de imagen/texto con **0 solicitudes de aprobación**; limpió la rutina al terminar. Las 55 pruebas offline pasan; ambos paneles compilaron sin advertencias. Dynamo SHA-256 `6d9461b0ea491981f0bd5a3d20805c3bb2aeb89b653fd18d4da878a579d7929d`; Revit SHA-256 `d427fc400c970877086db5c878173dfea17ecdf45603eb5f565da501054a7c38`, instalado en modo `STAGED_RESTART_REQUIRED`. **Pendiente:** cerrar Revit para limpiar la DLL que está en uso, instalar el paquete Dynamo actualizado y volver a abrir una vez para verificar menú, panel y acción desde la interfaz.

**Actualización posterior del mismo día.** Se corrigió `setup.ps1 -ConfigureOnly`: antes podía dejar vacío `mcp_servers.rvt_mcp.command`, por lo que Codex no disponía de las herramientas directas de Revit aunque el panel pareciera listo. La configuración local regenerada tiene comando válido. Una solicitud real mediante Codex app-server completó la lectura de la selección; la lectura de la vista tuvo un fallo intermitente de descubrimiento y pasó al repetirse secuencialmente. Otra solicitud con Luna siguió las reglas locales de ARQ-IA y no invocó NEXUS DX. No se modificó el modelo.

**Interfaz preparada, validación visual pendiente.** El panel Revit/Dynamo ahora consulta `model/list`, selecciona IDs reales, permite nueva conversación y copiar respuestas, conserva la conversación al cambiar de modelo, presenta texto con formato y muestra tarjetas de llamadas MCP según los eventos del app-server. La compilación de Dynamo y Revit terminó con 0 errores y 0 advertencias. SHA-256 distribuido: Dynamo `62b2ed005bb12b0426516409df399152d43f56e84e28b46708f5c3934646a61d`; Revit `923c982b275ceff3b0a0bd974c80bc04671c9127ccf32e942f49e32957bbb2b9`. Ambos componentes quedaron preparados por usuario para el próximo inicio; `doctor.ps1` dio 15 PASS, 4 WAIT, 0 WARN, 0 FAIL. Los WAIT corresponden al reinicio pendiente y a que el Revit activo contiene un proyecto del usuario, no la copia de prueba. No se ejecutaron cambios de modelo ni la UI nueva en vivo.

**Verificación offline actual:** 54 pruebas `unittest` PASS; smokes de Builder MCP, Runner MCP y circuito Builder → Runner → bridge simulado PASS; cliente C# de Codex app-server creó/listó una rutina de prueba con aprobación, procesó imagen y texto adjuntos y eliminó la rutina temporal. Esta evidencia cubre el flujo local y el contrato de herramientas, no la representación visual de la versión recién instalada en Revit.

**Editor de Windows:** los manifiestos y metadata dicen Branko Plaza; el aviso “fabricante desconocido” exige firmar los binarios con un certificado de firma de código confiable. No hay tal certificado configurado y no se suprime el aviso.

**Registro del reinicio de prueba:** Revit cargó `BimAiRevitAssistant.923c982b275ceff3.dll` y el bridge de ARQ-IA sin error de inicio. El `rvt-mcp` v0.6.2 instalado previamente registró un conflicto de referencias: sus `RevitAPI` y `RevitAPIUI` 27.0.10.0 frente a los módulos 27.0.20.0 ya cargados. El complemento terminó de iniciarse, pero este aviso impide declarar resuelta la lectura intermitente. El clon nuevo instala v1.1.0 cuando no existe una instalación compatible; falta probar esa combinación en otro PC. En el instante de ese arranque no se hicieron consultas ni cambios sobre el documento activo.

**Corrección de diseño pedida por Branko:** se sustituyó la composición anterior por la referencia de chat compacta: logo monocromo, estado MCP real en la cabecera, mensajes de usuario a la derecha, respuestas abiertas, tarjetas de herramientas dentro del hilo y compositor con adjuntos/modelo/envío. Se revisó un render independiente de 690 × 520 px en `artifacts/ARQ-IA-panel-preview.png` (local e ignorado por Git). Dynamo y Revit compilaron con 0 advertencias y 0 errores; hashes distribuidos: Dynamo `edd3c11a61fd3aedd2c4b1e1d5db94844d28403291b963aba4fc4006671e239a`, Revit `7ba500518add239ecb65f2327721a6849a4d87908f491839533bf0b2b4bf2f8c`. Ambos binarios quedaron preparados para el próximo inicio; la ficha actual de Revit aún usa el binario cargado antes del rediseño.

**Lectura en caso real designado para prueba:** MCP inicializó, descubrió 16 herramientas de consulta y completó `revit_get_current_view_info` y `revit_get_selected_elements`; el smoke no imprimió ni guardó datos del modelo. No hubo escritura. Para aceptar cambios de modelo se necesita una copia independiente del archivo de prueba.

**Cierre de esta revisión:** el binario final pasó de nuevo el smoke C# de Codex app-server: creación/listado de rutina con aprobación, adjuntos de imagen y texto y limpieza de la rutina temporal. Las 54 pruebas offline también pasaron. Branko pospuso el reinicio de Revit; por tanto, la nueva composición se verificó en un render WPF independiente, pero no todavía en la ficha visible de Revit/Dynamo. El `doctor.ps1` cerró con 16 PASS, 3 WAIT, 0 WARN y 0 FAIL; los WAIT corresponden al inicio pendiente y a la política de RVT descartable. No se hicieron escrituras sobre el caso real.

**Publicación:** el checkout Git y ZIP portable se prepararon localmente. La publicación GitHub queda pendiente porque este PC no tiene sesión autenticada en GitHub CLI ni remoto configurado; no se abrió el navegador ni se solicitó un token por chat.

**Borrador local detectado después del smoke:** apareció `colorear_muros_por_nivel` en `routines/`, con manifiesto `draft_not_validated` y `writes_model=true`. Su Python incluye una ruta absoluta de este PC. Se conservó sin ejecutar y se excluyó del paquete Git mediante `.gitignore`; una rutina validada y saneada puede promoverse explícitamente más adelante.

- **Catálogo de materiales, lectura comprobada:** en una sesión nueva de Revit 2027 con la copia local de `Snowdon Towers Sample Architectural.rvt`, `rvt-mcp` v0.6.2 devolvió los 300 materiales definidos (`returned=total=300`, sin límite ni omisiones). Se consultaron las propiedades detalladas de seis materiales representativos. ARQ-IA ahora expone las dos consultas de materiales en la configuración MCP del proyecto; el CSV completo queda en `logs/materials/` local e ignorado por Git. No se midió uso ni cantidad por elemento y no se tocó un proyecto productivo. [Método y límites](MATERIALES_REVIT.md).
- **Corrección de interpretación y portabilidad preparada:** el panel ya no intercepta “pintar el piso” antes de consultar Revit; Codex recibe la petición, distingue override de material y puede usar `revit_override_element_graphics`. El inventario MCP del binario local v0.6.2 confirmó esa herramienta y sus argumentos. El clon nuevo apunta a `rvt-mcp` v1.1.0 con SHA-256 fijo; esta combinación aún no se ha instalado en otro PC. Se retiraron tres scripts de migración/registro obsoletos; los instaladores de componentes necesarios permanecen.
- **Evidencia de esta sesión:** ambos paneles compilaron sin errores y quedaron instalados por usuario; hashes Dynamo `bf4653bb04cb141010020eb9b5591c740c0f28cee4e623611d1ecd3fcc91f94e` y Revit `45eb84c24c4157517c0ab78ef6bc4d82d7574dd882758864e0c218ed6cb76061`. La configuración MCP local ya expone `revit_override_element_graphics`. `doctor.ps1`: 14 PASS, 3 WAIT, 0 WARN, 0 FAIL con Revit cerrado. **Pendiente:** comprobar desde el chat visible una solicitud de color sobre RVT descartable. El ejemplo de muros rojos sigue siendo una ruta diseñada, no un éxito en Revit.
- **Estado: BLOCKED para aceptación de ejecución genérica.** La rutina fija de lectura tiene evidencia histórica aprobada; la ejecución genérica y escrituras complejas no se declaran validadas.
- **Versión anterior documentada:** HEAD 26626f4, 08-10, sin origin. Dynamo 0.4.0 agregó selector Económico/Estándar/Potente y una aclaración local que ahora se retiró porque bloqueaba la consulta del modelo y perdía contexto. No hay ahorro de cuota medido.
- **Instalación comprobada por archivos:** bridge distribuido e instalación referenciada coinciden, SHA-256 43ac7838fd55dfb7f67ad5a7ee102f72861c70d29c1350a1c86956c9780ced4f. Es el ajuste preparado el 05-10. Add-in distribuido/instalado y manifiesto Dynamo también contrastados por hashes.
- **Bloqueo vigente en evidencia:** PROTOTYPE_STATUS del 05-10 mantiene revit_execution_timeout_unknown al cambiar de grafo; no se encontró validación documental posterior que lo resuelva. Conservar causas probables como hipótesis.
- **Validación histórica:** build, render y 56 pruebas offline constan en la documentación anterior; no se reejecutaron. El panel visible, mensaje/adjunto y lectura genérica requieren comprobación viva aparte.
- **Siguiente acción:** en la próxima sesión normal sobre copia descartable, verificar modelo tras timeout, cargar el bridge preparado y comprobar lectura genérica con executed_and_verified_read_only; después probar mensaje/adjunto desde el panel. Una escritura requiere su alcance y aprobación específica.
- **Diseño/publicación:** arquitectura y bucle de ejecución documentados; kit manual ChatGPT→Dynamo es otro proyecto. ZIP ARQ-IA-26626f4.zip existe con SHA-256 22e93998c6887f1fc2b1a790c7ee06a0c5ca1c46be8261819b6af7edb4e485c6. Repositorio sin remoto; no se publicó en esta tarea.
- **Método:** revisión independiente Challenger de AGENTS, documentos, código puntual, Git, manifiestos y hashes. Sin ejecutar Revit/Dynamo, pruebas, scripts, modelos, logs o credenciales. Confianza alta sobre artefactos; aceptación viva no comprobada.

### Registro de avance

13-10-2026: se habilitó la capacidad gráfica tipada y se corrigió el bloqueo local de lenguaje natural; se actualizó el pin de instalación para clones nuevos y se simplificó la carpeta. Se conserva el bloqueo de ejecución genérica y la historia previa. Documentar preparación no cierra el timeout.

## Evidencia histórica previa (05-10-2026)

<!-- PRODUCT-STATE-20261013:END -->


**Fecha:** 5 de octubre de 2026. **Equipo comprobado:** Revit 2027.0, Dynamo 4.0.2, PythonNet3 Engine 1.4.8, Codex Desktop/CLI y `rvt-mcp` 0.6.2.

## Lo que funciona

- El instalador local colocó el add-in de Revit, el paquete de Dynamo por usuario y tres MCP del proyecto. El menú de Dynamo **Extensiones → Abrir ARQ-IA** y la ficha lateral aparecen en el log vivo: `Loaded Package ARQ-IA 0.3.0` y `ARQ-IA ... view extension is added`.
- El diagnóstico llegó a **18 PASS, 0 WAIT, 0 WARN, 0 FAIL** con Revit y Dynamo abiertos sobre una copia descartable. El bridge respondió al ping y confirmó Dynamo asociado al documento activo.
- El cliente del chat abrió Codex app-server, creó/listó una rutina mediante MCP y procesó adjuntos de imagen y texto. La consulta de vista/selección por `rvt-mcp` pasó en pruebas previas. Esto aún no equivale a una solicitud enviada por una persona desde la ficha visible.
- Compilan los tres DLL entregados y la suite offline pasó 56 pruebas. La compilación del bridge preparada para el próximo inicio tiene SHA-256 `43ac7838fd55dfb7f67ad5a7ee102f72861c70d29c1350a1c86956c9780ced4f`.
- La rutina fija de solo lectura **sí se ejecutó en vivo**: `REVIT_BRIDGE_PING=PASS` y `DYNAMO_READONLY_RUN=PASS` sobre el RVT descartable. PythonNet3 produjo el resultado y el bridge verificó cero cambios de elementos.
- El registro antiguo de Dynamo en Program Files se retiró. El paquete activo está bajo `%APPDATA%\Dynamo\Dynamo Revit\27.0\packages\ARQ-IA`.

## Bloqueo actual

La prueba posterior de una rutina genérica de solo lectura agotó el tiempo con `revit_execution_timeout_unknown` al cambiar de grafo. El log de Dynamo registró que intentó guardar el grafo temporal anterior; es una causa probable de la espera, aunque la traza no demuestra exactamente qué diálogo la produjo. **La ejecución genérica y las escrituras complejas no están validadas.** No se usó un proyecto productivo.

El [código oficial de DynamoRevit](https://github.com/DynamoDS/DynamoRevit/blob/master/src/DynamoRevit/DynamoRevit.cs) distingue ejecución con UI ya abierta de modo de automatización. El bridge devuelve el control a Revit, espera el archivo de resultado sin bloquear la interfaz y vuelve a verificar dentro de un evento de Revit. Esto hizo pasar la rutina fija. También se eliminó una segunda invocación de `Run()` que podía duplicar una escritura. Para la próxima sesión, el bridge conservará el archivo del grafo temporal mientras siga abierto en Dynamo, evitando que Dynamo lo trate como inexistente al cambiar de grafo. Este último ajuste está instalado pero no se ha probado en vivo; no se pedirá otro reinicio durante esta ronda.

## Próxima comprobación

1. En la siguiente sesión normal de Revit, el manifiesto cargará el bridge preparado. Abre solo el RVT descartable y Dynamo.
2. Ejecuta primero una rutina genérica de solo lectura y confirma que retorna `executed_and_verified_read_only` sin esperar un cuadro de guardado del grafo anterior.
3. Prueba un mensaje y un adjunto directamente desde el panel visible. Una escritura se probará únicamente en la copia descartable, con revisión del script y aprobación.

Los modelos, adjuntos temporales, credenciales y resultados locales están excluidos de Git y del ZIP portable. La metadata de fabricante es **Branko Plaza**; los DLL aún no tienen firma de editor confiable para Windows.
