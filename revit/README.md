# Revit

ARQ-IA apunta a Revit 2027. El equipo de desarrollo conserva `rvt-mcp` v0.6.2; un clon nuevo descarga v1.1.0 del release oficial con SHA-256 fijo. El instalador no migra automáticamente una versión existente.

Las consultas de vista activa/selección respondieron en Revit. La configuración local de Codex ofrece herramientas tipadas con aprobación por llamada, incluido el override gráfico de elementos; el generador de rutinas y el runner Dynamo son MCP separados. La rutina fija PythonNet3 de lectura pasó en un RVT descartable, pero la ejecución genérica al cambiar de grafo y las escrituras complejas siguen sin validación viva. El bridge genérico solo admite RVT locales de `revit/test-models/`; toda ejecución PythonNet3 requiere confirmación de posible escritura, ya que los metadatos de una rutina no demuestran que su código sea de solo lectura.

Para regenerar el paquete desde el fuente, ejecuta `powershell.exe -NoProfile -ExecutionPolicy Bypass -File revit\bridge\build.ps1 -Package`. Esto compila a `artifacts/bridge-build/`, copia el DLL nuevo a `revit/bridge/dist/` y actualiza su SHA-256. Después actualiza la instalación con `powershell.exe -NoProfile -ExecutionPolicy Bypass -File revit\bridge\install.ps1 -Force -StageWhileRevitOpen`; la sesión nueva de prueba debe cargar el complemento sin firma con **Load Once**. Mantén intacta la sesión productiva.
