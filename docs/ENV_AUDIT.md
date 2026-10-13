# Entorno de referencia

**Actualizado:** 26 de septiembre de 2026
**Alcance:** versiones y compatibilidad usadas para este POC. Se omiten rutas del usuario, nombres de equipo y datos identificables del modelo.

> **Línea base histórica del PC de desarrollo.** Este registro describe la instalación local observada el 26-09-2026, incluido `rvt-mcp` v0.6.2; no describe lo que instala un clon nuevo. El instalador actual fija `rvt-mcp` v1.1.0 y verifica su SHA-256. Para requisitos e instalación vigentes, consulta [INSTRUCCIONES_OTRO_PC.md](INSTRUCCIONES_OTRO_PC.md) y [PROTOTYPE_STATUS.md](PROTOTYPE_STATUS.md).

| Componente | Referencia validada | Implicación |
|---|---|---|
| Revit | Revit 2027, build 27.0.20.10 | Host objetivo del paquete. |
| Dynamo for Revit | Dynamo Core 4.0.2.3852 | Versión usada al crear la plantilla `.dyn`. |
| Python en Dynamo | Engine PythonNet3 integrado | Las rutinas se guardan como nodos PythonNet3. |
| .NET | Revit 2027 usa .NET 10; Runtime 10.0.0 presente; `dotnet --list-sdks` no encuentra SDK | Visual Studio Build Tools incluye Roslyn `csc`; el bridge compila contra RevitAPI/DynamoRevitDS. El DLL instalado por usuario coincide por SHA-256 con el paquete `9205c035…`. Falta cargarlo en la sesión de prueba. |
| rvt-mcp | v0.6.2, paquete Windows x64 verificado | El release declara plugins para Revit 2022–2027. |
| Git | Disponible en el equipo de desarrollo | El proyecto se mantiene como repositorio Git. |

## Evidencia de Revit

El 24-09-2026 y nuevamente el 26-09-2026 se consultó Revit mediante `rvt-mcp` en modo de solo lectura. Hoy respondieron las herramientas de vista activa y selección. No se hicieron cambios y los identificadores y nombres del modelo se excluyeron de esta copia pública.

Una primera conexión iniciada sin la sesión interactiva respondió que el plugin no estaba ejecutándose. La consulta funcionó después de que Revit se iniciara normalmente. La conexión de estas herramientas desde Codex todavía requiere abrir y confiar el proyecto y comprobarlas en la aplicación.

## Límites pendientes

- El listado de herramientas y las consultas de vista/selección están comprobados; no se han probado todas las herramientas de escritura.
- No se ha ejecutado visualmente el grafo `.dyn` de muestra desde Dynamo.
- El runner MCP → Dynamo valida contratos y paquetes offline. Se inició una sesión nueva de Revit con el RVT descartable; el paquete de bridge está instalado, pero el add-in todavía no se ha cargado y el ping vivo devuelve timeout. La ejecución del grafo sigue pendiente.
- La copia `revit/test-models/BIM-AI-Dynamo-Test.rvt` proviene de `Snowdon Towers Sample HVAC.rvt` incluida en la instalación local de Autodesk Revit 2027 y está excluida de Git.
- `DynamoCLI.exe` 4.0.2 no completó el smoke de un grafo PythonNet3 sintético: el proceso no creó XML de evaluación y lanzó una aserción ProtoCore. No se considera un método de ejecución funcional.

## Referencias oficiales

- [Autodesk: migración de Revit 2027 a .NET 10](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-WhatsNew/files/GUID-8D7A4715-EAF8-4BD1-BE78-061F900D0BCE.htm)
- [Dynamo Core 4.0 y PythonNet3](https://dynamobim.org/dynamo-core-4-0-release/)
- [Release oficial rvt-mcp v0.6.2](https://github.com/bimwright/rvt-mcp/releases/tag/v0.6.2)
