# Compatibilidad de ARQ-IA

**Actualizado:** 13 de octubre de 2026. Esta es la plataforma objetivo y la referencia de compatibilidad del prototipo; no garantiza que cada PC tenga los mismos componentes o parches.

| Componente | Objetivo | Nota |
|---|---|---|
| Sistema | Windows x64 | Los instaladores están escritos para Windows PowerShell. |
| Revit | 2027 | La extensión y el bridge cargan mediante el sistema de add-ins de Revit 2027. |
| Dynamo | Dynamo for Revit 4.0.2 | La plantilla `.dyn` fija esta versión y usa PythonNet3. |
| Python | PythonNet3 integrado en Dynamo | No se instala Python separado para ejecutar las rutinas. |
| Codex | Codex Desktop/CLI con sesión iniciada | El add-in inicia `codex.exe app-server` localmente. |
| MCP Revit | `bimwright/rvt-mcp` v1.1.0 | Descarga desde el release oficial y verificación SHA-256 fijada en `setup.ps1`. |
| .NET | Runtime incluido por Revit | El PC de arquitectura no necesita .NET SDK; los scripts `build.ps1` son para desarrollo. |

## Evidencia disponible

Las consultas de vista/selección y la lectura de materiales se comprobaron históricamente en una instalación de desarrollo con Revit 2027. La rutina fija de solo lectura también tiene una ejecución histórica en un modelo descartable. La compatibilidad integral de `rvt-mcp` v1.1.0 con una instalación limpia, las escrituras y las rutinas Dynamo genéricas no se consideran verificadas. Revisa [el estado actual](PROTOTYPE_STATUS.md) y usa las instrucciones del instalador para la primera comprobación.

Esta documentación evita rutas personales, nombres de equipo y nombres de archivos de modelos usados durante el desarrollo.
