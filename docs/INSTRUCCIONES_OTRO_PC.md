# Instalar ARQ-IA en otro PC

El otro PC indicado tiene Revit 2027 y Codex. Copia la carpeta del proyecto desde un release del repositorio o transfiérela como ZIP. Si el instalador no está en `vendor/`, `setup.ps1` lo descarga desde el release oficial v0.6.2 y verifica el SHA-256 antes de ejecutarlo.

Para generar el ZIP desde el PC de desarrollo, guarda los cambios en un commit y ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1`. El archivo se crea en `artifacts/` con el hash corto del commit; el script comprueba que no incluya RVT ni configuración/secrets locales.

## Instalación

Clona la carpeta como **ARQ-IA**, cierra Revit y haz doble clic en **Instalar-ARQ-IA.cmd**. También puedes ejecutar el instalador desde PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

El instalador comprueba Revit 2027, Dynamo y Codex CLI, instala/configura rvt-mcp y los MCP de esta copia, enlaza `routines/` con el bridge y registra los dos accesos **ARQ-IA**: panel acoplable en Revit y chat lateral en Dynamo. Verifica hashes de los ensamblados distribuidos y genera las rutas locales para este clon.

El panel Dynamo se instala en el perfil del usuario y no requiere administrador en un equipo nuevo. Si este equipo ya tenía una versión antigua en `Program Files`, ejecuta **Migrar-ARQ-IA-Anterior.cmd** una sola vez con Revit cerrado y acepta el aviso de Windows. El diagnóstico avisa si hace falta.

La instalación nueva de ARQ-IA usa el perfil del usuario y no necesita elevar permisos. Si Revit está abierto con rvt-mcp ya instalado, el comando prepara los complementos para el siguiente reinicio. Para instalar rvt-mcp por primera vez, guarda y cierra Revit antes de ejecutar el comando. `-CheckOnly` valida prerequisitos sin escribir archivos.

Después de instalar, abre esta carpeta como proyecto en Codex y confirma que confías en ella para habilitar sus MCP locales si aparece el aviso. Codex debe estar iniciado con tu cuenta; la integración no guarda claves de API. Abre Revit y Dynamo en una sesión nueva. Si Revit muestra el aviso de add-in sin firma, revisa que el nombre sea **ARQ-IA**/**ARQ-IA Dynamo Bridge** y el fabricante **Branko Plaza** antes de elegir **Load Once**.

En Dynamo, envía primero una consulta de solo lectura: **“Consulta la vista activa y la selección de Revit; devuelve vista, IDs y categorías. No cambies el modelo.”** El panel muestra **Gestor: Branko Plaza**, acepta archivos e imágenes y conecta Codex al enviar. Para cambios, revisa cada propuesta y aprobación; no uses un modelo productivo en esta primera prueba.

## Diagnosticar sin modificar Revit

Después de instalar y preparar el modelo, ejecuta:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\doctor.ps1
```

El diagnóstico es de solo lectura. Comprueba las instalaciones locales de Revit/Dynamo y rvt-mcp, la configuración MCP de esta carpeta, el manifiesto/hash del bridge, el RVT descartable, el pipe y si Dynamo UI está enlazado al documento activo. `WAIT` puede indicar que falta reiniciar Revit tras instalar el bridge o abrir el modelo/Dynamo UI; `FAIL` requiere corregir la instalación antes de ejecutar rutinas.

## Prueba 1: conexión Codex → Revit (consulta)

Selecciona unos pocos elementos en Revit y pídele a Codex:

> Consulta la vista activa y los elementos seleccionados mediante rvt-mcp. Devuelve vista, tipo de vista, IDs y categorías. No cambies el modelo.

Compara la respuesta con la interfaz. Este perfil también incluye herramientas tipadas de creación y edición; por eso, primero valida la lectura y trabaja solo en un modelo nuevo o descartable. Si solicitas una escritura, pide a Codex que describa elemento, ubicación, tipo/valores y resultado esperado. La herramienta aprobada puede aplicar el cambio inmediatamente: el MCP no ofrece una vista previa/rollback general.

## Prueba 2: ejecutar rutina genérica de solo lectura

El RVT descartable está excluido del ZIP/repositorio. En el PC de prueba prepara una copia del sample local incluido con Revit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\revit\test-models\prepare-test-model.ps1
```

El script copia `Samples\Snowdon Towers Sample HVAC.rvt` a `revit/test-models/BIM-AI-Dynamo-Test.rvt`, verifica SHA-256 y no sobrescribe un destino existente distinto. También copia los cinco RVT Snowdon que el HVAC referencia con rutas relativas y comprueba sus hashes; los modelos permanecen locales e ignorados por Git. Solo quita el atributo de solo lectura a la copia HVAC de prueba, no al sample instalado. Si no está instalado ese sample, pasa `-SourceModelPath` con otro RVT local y desechable; sus dependencias deben estar disponibles en sus rutas.

Abre primero `revit/test-models/BIM-AI-Dynamo-Test.rvt`, o una copia descartable local, en Revit 2027. Si Revit muestra referencias ausentes, puedes elegir **Ignorar y abrir** solo para este RVT de prueba. Abre Dynamo UI en el mismo documento y espera a que no haya operaciones ni diálogos pendientes. Si la primera activación de Dynamo marca cambios por la inicialización de materiales de vista previa, guarda solo esta copia descartable para dejarla limpia. Ejecuta `doctor.ps1`; debe confirmar `Dynamo UI está abierto y ligado al modelo Revit activo`.

Con Revit y Dynamo todavía abiertos, ejecuta desde la carpeta del proyecto:

```powershell
python -B tests/run_dynamo_runner_mcp_smoke.py --live-generic
```

El resultado esperado incluye `REVIT_BRIDGE_PING=PASS` y `DYNAMO_GENERIC_READONLY_RUN=PASS`. La rutina consulta título, vista, selección y cantidad de elementos; verifica que el documento no cambió y elimina el resultado temporal. Si falla, revisa que el add-in actualizado cargó en esa sesión, que Dynamo siga enlazado al documento y que `bridge.json` apunte a esta carpeta.

## Prueba 3: Codex prepara una rutina Dynamo

Pídele:

> Crea una rutina Dynamo PythonNet3 de solo lectura llamada `read_active_view_selection` que informe nombre/tipo de vista activa, cantidad de elementos seleccionados, IDs y categorías. Guarda el `.py` y el `.dyn` en `routines/`. No ejecutes el grafo.

El MCP local guarda fuente, grafo y manifiesto en `routines/`. El `.dyn` incluye un wrapper de resultados JSON limitado a 64 KiB; el bridge vuelve a comprobar hashes, engine PythonNet3, grafo Manual y estructura sin conectores/dependencias. Revisa el código antes de ejecutarlo.

El paquete incluye `routines/set_comments_on_selection.dyn`, un borrador que escribe `PRUEBA_IA` en Comments para los elementos seleccionados. Está protegido tanto en el bridge como en el propio script: solo acepta un RVT local guardado y no workshared dentro de `revit/test-models/`. Para validarlo, abre el RVT desechable, selecciona un elemento de prueba con Comments editable y pide a Codex que ejecute esa rutina. Codex debe mostrar la rutina y solicitar aprobación de esa llamada. Después comprueba el ID devuelto y vuelve a leer el parámetro con `rvt-mcp`.

## Alcance y limitación actual

El perfil de Revit incluye herramientas tipadas para niveles, ejes, muros u otros elementos lineales, pisos/superficies, recintos, instancias de familias, parámetros, tipos, textos, cotas y etiquetas. No se han verificado individualmente todas las operaciones del servidor y la lista se mantiene deliberadamente acotada. Las tareas complejas se deben dividir en pasos verificables y aprobar llamada por llamada.

El runner ejecuta rutinas guardadas genéricas, pero solo desde este proyecto y dentro de `revit/test-models/`. Toda ejecución genérica exige `authorize_possible_model_writes=true` y aprobación de la llamada MCP, incluso si el manifiesto dice solo lectura: PythonNet3 corre con los permisos completos del proceso y los metadatos no demuestran que el código no escriba. El resultado `affected_ids` debe coincidir exactamente con los IDs reportados por `DocumentChanged`; entonces marca el manifiesto `validated`. La ruta del modelo no es un sandbox para acceso a archivos/red. Revisa el código generado antes de autorizar la posible modificación. No pidas a Codex que use `revit_send_code_to_revit` como reemplazo.

## Pruebas reproducibles

Pruebas MCP que no modifican el modelo:

```powershell
python -m unittest discover -s tests -v
python tests/run_routine_builder_mcp_smoke.py
python tests/run_revit_mcp_readonly_smoke.py
python tests/run_dynamo_runner_mcp_smoke.py
python tests/run_dynamo_runner_pipe_smoke.py
```

`run_revit_mcp_readonly_smoke.py` requiere Revit abierto. El test `run_dynamo_runner_pipe_smoke.py` usa un fake bridge temporal y valida el transporte, no Dynamo real.

Con el add-in puente instalado, Codex conectado y un RVT descartable guardado y sin cambios pendientes, ejecuta la prueba integrada:

```powershell
python tests/run_dynamo_runner_mcp_smoke.py --live
```

Solo intenta la rutina fija de lectura. No pruebes escrituras en un modelo productivo; las operaciones tipadas de rvt-mcp pueden cambiar el documento al aprobar la llamada.

Este paquete se creó con Dynamo 4.0.2. Dynamo Core 4.2 incluye Autodesk Assistant y DynamoMCP para trabajar con grafos; la publicación oficial indica que la integración con aplicaciones anfitrionas llegaría después y que el Assistant de Dynamo no modifica directamente modelos Revit. No se ha demostrado una conexión de DynamoMCP con Codex en este POC. Autodesk documenta el Revit Public MCP Server como compatible solo con Revit 2027.2; no lo instales en Revit 2027.0. [Notas oficiales de Dynamo 4.2](https://dynamobim.org/dynamo-core-4-2-release/) · [Guía oficial del Revit Public MCP Server](https://help.autodesk.com/view/ADSKMCP/ENU/?guid=ADSKMCP_RevitMcp_setting_up_revit_mcp_server_html)

Para desinstalar rvt-mcp, usa el `uninstall.ps1` incluido en el ZIP y lee primero su `-WhatIf`. Los archivos del POC y sus rutinas se conservan aparte.
