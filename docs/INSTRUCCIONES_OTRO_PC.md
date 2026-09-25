# Instalar y probar en el otro PC

El otro PC indicado tiene Revit 2027 y Codex. Copia la carpeta del proyecto desde un release del repositorio o transfiérela como ZIP. Si el instalador no está en `vendor/`, `setup.ps1` lo descarga desde el release oficial v0.6.2 y verifica el SHA-256 antes de ejecutarlo.

## Instalación

1. Cierra Revit.
2. Abre PowerShell en la carpeta `BIM-AI-POC` y ejecuta:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\setup.ps1
   ```

   El script verifica SHA-256 antes de instalar. Instala rvt-mcp solo para Revit 2027 y el usuario actual; no registra un MCP global. Crea `.codex/config.toml` para este proyecto con una lista acotada de consultas y operaciones tipadas. Codex solicita aprobación en cada llamada. El runner expone una sola rutina de lectura fijada por hashes; no acepta código arbitrario ni escrituras Dynamo.

   Si rvt-mcp ya está instalado, puedes actualizar solo el cableado del proyecto sin reinstalar add-ins:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\setup.ps1 -ConfigureOnly
   ```
3. Instala el add-in puente local:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\revit\bridge\install.ps1
   ```

   Esta versión incluye un DLL precompilado para Revit/Dynamo 2027. El instalador vuelve a compilar desde fuente solo si falta ese archivo. No uses `-Force` salvo que vayas a actualizar conscientemente una instalación previa.
4. Abre `BIM-AI-POC` como proyecto en Codex, confía el proyecto cuando Codex lo solicite y reinicia Codex.
5. Inicia Revit 2027 con un modelo nuevo/desechable. No uses un proyecto de producción.

## Prueba 1: conexión Codex → Revit (consulta)

Selecciona unos pocos elementos en Revit y pídele a Codex:

> Consulta la vista activa y los elementos seleccionados mediante rvt-mcp. Devuelve vista, tipo de vista, IDs y categorías. No cambies el modelo.

Compara la respuesta con la interfaz. Este perfil también incluye herramientas tipadas de creación y edición; por eso, primero valida la lectura y trabaja solo en un modelo nuevo o descartable. Si solicitas una escritura, pide a Codex que describa elemento, ubicación, tipo/valores y resultado esperado. La herramienta aprobada puede aplicar el cambio inmediatamente: el MCP no ofrece una vista previa/rollback general.

## Prueba 2: ejecutar rutina Dynamo de solo lectura

En la carpeta del proyecto ejecuta:

```powershell
python tests/run_dynamo_runner_mcp_smoke.py --live
```

El resultado esperado incluye `REVIT_BRIDGE_PING=PASS` y `DYNAMO_READONLY_RUN=PASS`. Si el primer paso falla, revisa que el add-in cargó al iniciar Revit y que Codex abrió el proyecto correcto. La rutina informa la vista y selección, verifica que el documento no cambió y elimina el resultado temporal. No modifica el RVT.

## Prueba 3: Codex prepara una rutina Dynamo

Pídele:

> Crea una rutina Dynamo PythonNet3 de solo lectura llamada `read_active_view_selection` que informe nombre/tipo de vista activa, cantidad de elementos seleccionados, IDs y categorías. Guarda el `.py` y el `.dyn` en `routines/`. No ejecutes el grafo.

El MCP local guarda fuente, grafo y manifiesto en `routines/`. Revisa los tres archivos. Abre el `.dyn` en Dynamo desde Revit 2027; comprueba que el Python node diga PythonNet3 y que el modo sea Manual. Ejecuta solo sobre el modelo desechable.

El paquete también incluye `routines/set_comments_on_selection.dyn`, un borrador que escribe `PRUEBA_IA` en Comments para los elementos seleccionados. Sus pruebas actuales usan dobles de Revit y no demuestran que Dynamo lo ejecute. No lo ejecutes en un proyecto productivo; aún falta validarlo en una copia desechable y comprobar el cambio con una consulta posterior.

## Alcance y limitación actual

El perfil de Revit incluye herramientas tipadas para niveles, ejes, muros u otros elementos lineales, pisos/superficies, recintos, instancias de familias, parámetros, tipos, textos, cotas y etiquetas. No se han verificado individualmente todas las operaciones del servidor y la lista se mantiene deliberadamente acotada. Las tareas complejas se deben dividir en pasos verificables y aprobar llamada por llamada.

El runner solo ejecuta el ID y los archivos exactos fijados en el prototipo, que son de solo lectura. Rutinas nuevas se revisan y ejecutan manualmente en Dynamo; aún no pueden registrarse para ejecución automática. No pedir a Codex que use `revit_send_code_to_revit` como reemplazo.

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
