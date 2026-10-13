# Instalar ARQ-IA en otro PC

## Antes de empezar

Revit 2027 con Dynamo for Revit, Windows x64 y Codex Desktop/CLI con sesión iniciada. La primera instalación requiere acceso a [GitHub Releases de `bimwright/rvt-mcp`](https://github.com/bimwright/rvt-mcp/releases/tag/v1.1.0). El instalador descarga exactamente v1.1.0 y comprueba el SHA-256 publicado para el ZIP. Los DLL propios de ARQ-IA ya vienen en el repositorio y se verifican por hash.

## Instalación

1. Clona el repositorio con Git en una carpeta a elección y abre esa carpeta. También sirve un ZIP del repositorio extraído.
2. Guarda tu trabajo y cierra Revit antes de la primera instalación.
3. Haz doble clic en `Instalar-ARQ-IA.cmd`. El programa comprueba Revit, Dynamo y Codex, instala el add-in y panel por usuario, y configura las rutas MCP locales para esa carpeta. No necesita .NET SDK.
4. Abre la carpeta en Codex y acepta la confianza del proyecto cuando la solicite. No copies `.codex/config.toml` desde otro PC: el instalador lo genera con rutas del equipo.
5. Abre Revit 2027. En la cinta aparece **ARQ-IA**; en Dynamo busca **Extensiones → Abrir ARQ-IA**. Si Windows avisa que el editor es desconocido, comprueba nombre y fabricante Branko Plaza. La metadata no equivale a una firma digital.

Alternativa PowerShell: `powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1`.

Si la descarga falla, la instalación termina con un error explícito. También puedes poner el ZIP oficial `RvtMcp.Setup-v1.1.0-win-x64.zip` en `vendor/`; debe coincidir con el SHA-256 fijado en `setup.ps1`. Una instalación anterior v0.6.2 detectada en el PC se conserva y se configura; no se migra automáticamente a 1.1.0.

## Primera comprobación

Con un modelo **descartable**, pregunta en el panel: “Dime la vista activa y qué tengo seleccionado. No cambies el modelo”. Compara con Revit. Si falla, ejecuta `doctor.ps1` desde PowerShell o abre `Instalar-ARQ-IA.cmd` de nuevo con Revit cerrado. `doctor.ps1` solo diagnostica; `WAIT` indica un paso pendiente, `FAIL` indica instalación incompleta.

Después prueba: “En esta vista, muestra en rojo los muros del Nivel 1; no cambies materiales. Lee primero cuántos son, enséñame el alcance y pide aprobación antes de aplicar”. Verifica nombre/ID del nivel: “Nivel 1” es un ejemplo, no una suposición. La ruta correcta usa el override gráfico tipado de Revit, no un script Dynamo. [Contrato de esta operación](COLOR_MUROS_PRIMER_PISO.md).

La ejecución de rutinas **genéricas** de Dynamo y las escrituras complejas siguen pendientes de validación viva. Para probarlas usa exclusivamente un RVT guardado en `revit/test-models/`, revisa el `.py` completo y aprueba la llamada concreta. Un resultado `revit_execution_timeout_unknown` obliga a consultar el modelo antes de repetir. No uses un proyecto productivo para aceptar esta versión.

## Qué copia Git

Fuente de los paneles/bridge, DLL propios con hashes, instalador, MCP de rutinas, plantillas, documentos y rutinas de ejemplo. Git excluye RVT/RFA, credenciales, adjuntos, logs, caché de terceros, binarios temporales, `.codex/config.toml` local y ZIP generados. La instalación crea los archivos locales que faltan en cada PC.

Para el equipo de desarrollo: `package.ps1` genera un ZIP desde un **commit limpio**. `build.ps1` en cada componente recompila los DLL si cambia la fuente; los arquitectos no necesitan ejecutarlo.
