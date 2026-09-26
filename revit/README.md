# Revit

El POC apunta a Revit 2027 con RvtMcp v0.6.2 instalado en el perfil del usuario. El instalador upstream es self-contained y se obtiene desde su release oficial con verificación SHA-256.

El 24-09-2026, las consultas de vista activa y selección respondieron en una sesión interactiva; no se modificó el modelo productivo. La configuración local de Codex ofrece herramientas Revit tipadas con aprobación por llamada, el generador de rutinas y el runner Dynamo. El bridge genérico solo admite RVT locales de `revit/test-models/`; toda ejecución PythonNet3 requiere confirmación de posible escritura, ya que los metadatos de una rutina no demuestran que su código sea de solo lectura. El add-in quedó instalado por usuario para la próxima sesión, sin cargarlo en la sesión productiva que ya estaba abierta. Falta validar la ejecución viva desde Codex y Dynamo.

Para regenerar el paquete desde el fuente, ejecuta `powershell.exe -NoProfile -ExecutionPolicy Bypass -File revit\bridge\build.ps1 -Package`. Esto compila a `artifacts/bridge-build/`, copia el DLL nuevo a `revit/bridge/dist/` y actualiza su SHA-256. Después actualiza la instalación con `powershell.exe -NoProfile -ExecutionPolicy Bypass -File revit\bridge\install.ps1 -Force -StageWhileRevitOpen`; la sesión nueva de prueba debe cargar el complemento sin firma con **Load Once**. Mantén intacta la sesión productiva.
