# Asistente BIM para Dynamo

Extensión lateral para Dynamo for Revit 2027 / Dynamo 4.0.2. Permite conversar con Codex desde Dynamo y usa los MCP configurados en la carpeta del proyecto.

## Cómo funciona

1. Dynamo agrega la pestaña lateral **Asistente BIM**.
2. El panel inicia `codex app-server` desde la carpeta del POC y mantiene la conversación mientras Dynamo permanece abierto.
3. Codex consulta el modelo mediante los MCP disponibles y sigue el flujo definido en `AGENTS.md`.
4. Las solicitudes MCP de herramientas piden aprobación individual en una ventana de confirmación. Los permisos de archivos/red se limitan al turno y requieren aprobación; solicitudes de archivos del agente y formularios MCP con campos se rechazan.
5. El agente puede crear y ejecutar rutinas usando los MCP existentes. Las ejecuciones genéricas siguen sujetas a los controles del bridge: RVT local de prueba, hashes verificados y aprobación de posible escritura.

El panel no contiene una credencial de API. Usa la sesión local de Codex CLI, que debe estar instalada, autenticada y disponible en `PATH`. El protocolo local [`app-server`](https://developers.openai.com/siwc/token-sharing-open-source/codex-app-server) está marcado como experimental por Codex. La extensión sigue siendo un prototipo hasta pasar pruebas visuales y de Revit en una sesión limpia. Dynamo la carga con su API oficial de [View Extensions](https://github.com/DynamoDS/Dynamo/wiki/Dynamo-View-Extensions).

## Compilar

El equipo de desarrollo necesita .NET SDK 10 y Dynamo Core 4.0.x. La guía instala el SDK localmente para el usuario; los arquitectos reciben el DLL precompilado y no necesitan el SDK.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\dynamo\extension\build.ps1
```

El build genera `dist\BimAiDynamoAssistant.dll`, su SHA-256 y el manifiesto `dist\viewExtensions\BimAiDynamoAssistant_ViewExtensionDefinition.xml`.

## Instalar

1. Configura el POC y sus MCP con `setup.ps1`.
2. Guarda el trabajo y cierra todas las sesiones de Revit.
3. Abre PowerShell como Administrador en la carpeta del POC y ejecuta:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\dynamo\extension\install.ps1
   ```

4. Inicia Revit 2027 y abre Dynamo. La pestaña **Asistente BIM** debe quedar en la barra lateral de extensiones.

El instalador verifica el hash antes y después de copiar; no modifica modelos RVT. Para reemplazar una DLL instalada distinta, revisa el cambio y pasa `-Force`.

## Pruebas

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\dynamo\extension\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run_dynamo_extension_client_smoke.ps1
python -B .\tests\run_dynamo_extension_codex_smoke.py --mcp
```

El smoke C# usa el cliente real de la extensión para iniciar Codex app-server, pedir la creación aprobada de una rutina de solo lectura, verificar `.py`/`.dyn`/manifiesto/hash, volver a listarla y limpiar sus archivos temporales. El smoke Python comprueba aparte `list_saved_routines` con las rutinas guardadas del proyecto. No abren Revit ni Dynamo. La carga visual del panel y el loop Dynamo → Revit se registran aparte en `docs/PROTOTYPE_STATUS.md`.
