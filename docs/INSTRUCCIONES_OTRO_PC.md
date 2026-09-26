# Instalar y probar en el otro PC

El otro PC indicado tiene Revit 2027 y Codex. Copia la carpeta del proyecto desde un release del repositorio o transfiérela como ZIP. Si el instalador no está en `vendor/`, `setup.ps1` lo descarga desde el release oficial v0.6.2 y verifica el SHA-256 antes de ejecutarlo.

## Instalación

1. Cierra Revit.
2. Abre PowerShell en la carpeta `BIM-AI-POC` y ejecuta:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\setup.ps1
   ```

   El script verifica SHA-256 antes de instalar. Instala rvt-mcp solo para Revit 2027 y el usuario actual; no registra un MCP global. Crea `.codex/config.toml` para este proyecto con una lista acotada de consultas y operaciones tipadas, generador/listado de rutinas y runner. Cada herramienta pide aprobación en Codex.

   Si rvt-mcp ya está instalado, puedes actualizar solo el cableado del proyecto sin reinstalar add-ins:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\setup.ps1 -ConfigureOnly
   ```
3. Instala el add-in puente local:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\revit\bridge\install.ps1
   ```

   Esta versión incluye un DLL precompilado para Revit/Dynamo 2027. El instalador vuelve a compilar desde fuente solo si falta ese archivo. El DLL del POC no está firmado; al iniciar Revit, puede aparecer el aviso de complemento sin firma. Para la prueba, verifica que cargue “BIM-AI Dynamo Bridge” y elige **Load Once**. Usa **Always Load** solo si decidiste confiar permanentemente en este DLL e ID de complemento. Autodesk explica estas opciones en [Security: Unsigned File or Add-In](https://help.autodesk.com/cloudhelp/2022/ENU/Revit-Troubleshooting/files/GUID-36D29367-A540-4CFD-9773-4995BF1DF08A.htm). Si Revit permanece abierto, usa `-StageWhileRevitOpen`; el puente se cargará solo en la próxima sesión. Usa `-Force` solo para actualizar conscientemente una instalación previa.
4. Abre `BIM-AI-POC` como proyecto en Codex y confía esa carpeta para que Codex lea su configuración `.codex/config.toml`; reinicia Codex si ya estaba abierto. En PowerShell, desde la raíz del proyecto, ejecuta `codex mcp list` y confirma que aparezcan habilitados `rvt_mcp`, `bim_ai_routines` y `bim_ai_dynamo_runner`. La lista confirma el cableado del proyecto, no la conexión viva con Revit.
5. Prepara primero el RVT desechable con `powershell -NoProfile -ExecutionPolicy Bypass -File .\revit\test-models\prepare-test-model.ps1`; luego inicia Revit 2027 con `revit/test-models/BIM-AI-Dynamo-Test.rvt`. No uses un proyecto de producción. Si no aparece el aviso, comprueba el diario de Revit y verifica que el DLL está cargado antes de ejecutar una rutina.

## Prueba 1: conexión Codex → Revit (consulta)

Selecciona unos pocos elementos en Revit y pídele a Codex:

> Consulta la vista activa y los elementos seleccionados mediante rvt-mcp. Devuelve vista, tipo de vista, IDs y categorías. No cambies el modelo.

Compara la respuesta con la interfaz. Este perfil también incluye herramientas tipadas de creación y edición; por eso, primero valida la lectura y trabaja solo en un modelo nuevo o descartable. Si solicitas una escritura, pide a Codex que describa elemento, ubicación, tipo/valores y resultado esperado. La herramienta aprobada puede aplicar el cambio inmediatamente: el MCP no ofrece una vista previa/rollback general.

## Prueba 2: ejecutar rutina genérica de solo lectura

El RVT descartable está excluido del ZIP/repositorio. En el PC de prueba prepara una copia del sample local incluido con Revit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\revit\test-models\prepare-test-model.ps1
```

El script copia `Samples\Snowdon Towers Sample HVAC.rvt` a `revit/test-models/BIM-AI-Dynamo-Test.rvt`, verifica SHA-256 y no sobrescribe un destino existente distinto. Si no está instalado ese sample, pasa `-SourceModelPath` con otro RVT local y desechable.

En la carpeta del proyecto ejecuta:

```powershell
python tests/run_dynamo_runner_mcp_smoke.py --live-generic
```

Abre primero `revit/test-models/BIM-AI-Dynamo-Test.rvt`, o una copia descartable local, en Revit 2027; guárdala y espera a que no haya operaciones pendientes. El resultado esperado incluye `REVIT_BRIDGE_PING=PASS` y `DYNAMO_GENERIC_READONLY_RUN=PASS`. La rutina consulta título, vista, selección y cantidad de elementos; verifica que el documento no cambió y elimina el resultado temporal. Si falla, revisa que el add-in cargó en esa sesión y que `bridge.json` apunta a esta carpeta.

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
