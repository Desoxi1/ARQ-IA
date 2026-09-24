# Instalar y probar en el otro PC

El otro PC indicado tiene Revit 2027 y Codex. Copia la carpeta del proyecto desde un release del repositorio o transfiérela como ZIP. Si el instalador no está en `vendor/`, `setup.ps1` lo descarga desde el release oficial v0.6.2 y verifica el SHA-256 antes de ejecutarlo.

## Instalación

1. Cierra Revit.
2. Abre PowerShell en la carpeta `BIM-AI-POC` y ejecuta:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\setup.ps1
   ```

   El script verifica SHA-256 antes de instalar. Instala rvt-mcp solo para Revit 2027 y el usuario actual; no registra un MCP global. Crea `.codex/config.toml` para este proyecto con una lista acotada de consultas y operaciones tipadas. Codex solicita aprobación en cada llamada. No se habilitan borrado, ejecución arbitraria de C#/Python, ToolBaker ni ejecución de Dynamo desde MCP.
3. Abre `BIM-AI-POC` como proyecto en Codex, confía el proyecto cuando Codex lo solicite y reinicia Codex.
4. Inicia Revit 2027 con un modelo nuevo/desechable. No uses un proyecto de producción.

## Prueba 1: conexión Codex → Revit (consulta)

Selecciona unos pocos elementos en Revit y pídele a Codex:

> Consulta la vista activa y los elementos seleccionados mediante rvt-mcp. Devuelve vista, tipo de vista, IDs y categorías. No cambies el modelo.

Compara la respuesta con la interfaz. Este perfil también incluye herramientas tipadas de creación y edición; por eso, primero valida la lectura y trabaja solo en un modelo nuevo o descartable. Si solicitas una escritura, pide a Codex que describa elemento, ubicación, tipo/valores y resultado esperado. La herramienta aprobada puede aplicar el cambio inmediatamente: el MCP no ofrece una vista previa/rollback general.

## Prueba 2: Codex prepara una rutina Dynamo

Pídele:

> Crea una rutina Dynamo PythonNet3 de solo lectura llamada `read_active_view_selection` que informe nombre/tipo de vista activa, cantidad de elementos seleccionados, IDs y categorías. Guarda el `.py` y el `.dyn` en `routines/`. No ejecutes el grafo.

El MCP local guarda fuente, grafo y manifiesto en `routines/`. Revisa los tres archivos. Abre el `.dyn` en Dynamo desde Revit 2027; comprueba que el Python node diga PythonNet3 y que el modo sea Manual. Ejecuta solo sobre el modelo desechable.

## Alcance y limitación actual

El perfil de Revit incluye herramientas tipadas para niveles, ejes, muros u otros elementos lineales, pisos/superficies, recintos, instancias de familias, parámetros, tipos, textos, cotas y etiquetas. No se han verificado individualmente todas las operaciones del servidor y la lista se mantiene deliberadamente acotada. Las tareas complejas se deben dividir en pasos verificables y aprobar llamada por llamada.

El POC **no** dispara automáticamente Dynamo desde MCP. Tras guardar la rutina, el usuario debe abrir Dynamo y pulsar Run. No pedir a Codex que use `revit_send_code_to_revit` como reemplazo. La ejecución MCP → Dynamo requiere desarrollar y probar un runner acotado en un incremento posterior.

Para desinstalar rvt-mcp, usa el `uninstall.ps1` incluido en el ZIP y lee primero su `-WhatIf`. Los archivos del POC y sus rutinas se conservan aparte.
