# Asistente BIM para Revit

Add-in de Revit 2027 que abre un chat acoplable en la pestaña **BIM-AI**. Comparte el mismo panel WPF, cliente local de Codex, almacenamiento de adjuntos y configuración MCP que la extensión de Dynamo.

El encabezado identifica a **Branko Plaza** como gestor. La integración usa la sesión local autenticada de Codex CLI y los MCP de la carpeta del proyecto; no contiene una API key. Ejecuta `install.ps1` desde la raíz del clon para configurar MCP, bridge, panel Revit y panel Dynamo en un solo paso.

## Compilar

En el PC de desarrollo con Revit 2027 y el SDK .NET 10 disponibles:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\revit\assistant\build.ps1
```

El resultado portátil queda en `dist/` con un SHA-256. El proyecto enlaza los archivos compartidos desde `dynamo/extension/`; solo refiere `RevitAPI.dll` y `RevitAPIUI.dll` durante la compilación. Dynamo, Revit y sus API no se copian al paquete.

## Instalar por usuario

El instalador completo de la raíz es el camino recomendado:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

Para reparar solo el panel Revit, ejecuta el instalador específico:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\revit\assistant\install.ps1
```

Si Revit está abierto, guarda el modelo cuando corresponda y usa `-StageWhileRevitOpen`. El manifiesto se prepara para la próxima sesión; no altera la sesión actual. Tras iniciar Revit, abre **BIM-AI → Abrir chat**. El panel aparece acoplado a la derecha y puede moverse o cerrarse desde la interfaz de Revit.

Si Windows no inicia la elevación automática, abre PowerShell como administrador y ejecuta el mismo comando desde la raíz del proyecto.

Para validar el registro sin instalar, agrega `-WhatIf`. Para reemplazar conscientemente una instalación previa, agrega `-Force`. El instalador verifica el SHA-256 y registra el add-in bajo `%APPDATA%\Autodesk\Revit\Addins\2027`; no toca archivos del modelo ni la instalación de Revit.

La prueba inicial recomendada es preguntar por la vista activa y selección, sin solicitar cambios al modelo. Codex puede proponer rutinas `.py`/`.dyn`; revisa el código y aprueba explícitamente cada acción que pueda escribir.
