# ARQ-IA para Dynamo

Extensión lateral para Dynamo for Revit 2027 / Dynamo 4.0.2. Permite conversar con Codex desde Dynamo y usa los MCP configurados en la carpeta del proyecto.

**Fabricante/desarrollador:** Branko Plaza. El paquete Dynamo guarda esta atribución en `copyright_holder`; el add-in de Revit la muestra como `VendorDescription`.

## Cómo funciona

1. Dynamo agrega el comando **Extensiones → Abrir ARQ-IA** y la pestaña lateral **ARQ-IA**. El comando vuelve a abrir el panel si cierras la pestaña.
2. El panel inicia `codex app-server` desde la carpeta del POC y mantiene la conversación mientras Dynamo permanece abierto.
3. Codex consulta el modelo mediante los MCP disponibles y sigue el flujo definido en `AGENTS.md`.
4. Las solicitudes MCP de herramientas piden aprobación individual en una ventana de confirmación. Los permisos de archivos/red se limitan al turno y requieren aprobación; solicitudes de archivos del agente y formularios MCP con campos se rechazan.
5. El agente puede crear y ejecutar rutinas usando los MCP existentes. Las ejecuciones genéricas siguen sujetas a los controles del bridge: RVT local de prueba, hashes verificados y aprobación de posible escritura.

## Chat, contexto y adjuntos

La interfaz usa la paleta oscura de Dynamo y botones celestes. El encabezado conserva al gestor **Branko Plaza**. Cuando falta la instalación inicial, la tarjeta indica **Instalar-ARQ-IA.cmd** y el panel comprueba periódicamente cuándo termina la configuración para activar el chat. El pie aclara que Codex inicia al enviar el primer mensaje.

- El chat muestra el estado de Codex y del contexto Revit/MCP. Al enviar una solicitud de Revit, Codex consulta la vista activa y la selección mediante los MCP configurados; el panel no lee ni cambia el modelo por sí mismo.
- El botón **Ver** consulta ahora la vista activa y los elementos seleccionados sin enviar el borrador ni los adjuntos que estén esperando en el compositor. El resultado aparece como respuesta dentro del chat.
- Puedes adjuntar archivos con **＋ Adjuntar**, arrastrarlos al panel o pegar una imagen desde el portapapeles con `Ctrl+V`. Se admiten imágenes PNG/JPG/GIF/WebP, documentos PDF/Office, archivos Dynamo/texto/código e intercambios BIM/CAD indicados en el selector.
- Límite actual: 8 archivos y 40 MB por envío; la sesión del panel acumula como máximo 120 MB. El panel prepara miniaturas para imágenes y conserva los adjuntos pendientes si el envío falla.
- El panel copia los archivos a `logs/attachments/<sesión>/` dentro del proyecto para que Codex los lea sin incluir la ruta original del usuario. Las imágenes se envían como imágenes al modelo de la sesión Codex; el texto compatible se incorpora como referencia acotada y los demás formatos quedan disponibles como archivos locales de solo lectura. El contenido adjunto es dato no confiable: nunca se ejecutan scripts ni macros adjuntos.
- La carpeta `logs/attachments/` está ignorada por Git y excluida del ZIP portable. Al cerrar correctamente el panel, se elimina su carpeta de sesión; un cierre abrupto puede dejar archivos que deben borrarse manualmente.
- Codex trabaja con la sesión local autenticada y los MCP configurados. No se requiere una clave API aparte. El panel pide aprobación para las herramientas que la solicitan.

El panel no contiene una credencial de API. Usa la sesión local autenticada de Codex CLI; busca el ejecutable en `PATH` y en la instalación de Codex Desktop. El protocolo local [`app-server`](https://developers.openai.com/siwc/token-sharing-open-source/codex-app-server) está marcado como experimental por Codex. La extensión sigue siendo un prototipo hasta pasar pruebas visuales y de Revit en una sesión limpia. Dynamo la carga con su API oficial de [View Extensions](https://github.com/DynamoDS/Dynamo/wiki/Dynamo-View-Extensions).

## Compilar

El equipo de desarrollo necesita .NET SDK 10 y Dynamo Core 4.0.x. La guía instala el SDK localmente para el usuario; los arquitectos reciben el DLL precompilado y no necesitan el SDK.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\dynamo\extension\build.ps1
```

El build genera `dist\BimAiDynamoAssistant.dll`, su SHA-256, `dist\pkg.json` y el manifiesto de View Extension con ruta relativa de paquete.

## Instalar

Desde la raíz clonada, ejecuta el instalador completo una vez; deja preparados los MCP, bridge y los paneles Revit/Dynamo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

El instalador coloca un paquete de Dynamo en el perfil del usuario. Con Revit abierto, los archivos quedan preparados para el próximo reinicio; en una primera instalación de rvt-mcp, guarda/cierra Revit antes. Para reparar solo esta extensión, se conserva el instalador específico de abajo.

El instalador Dynamo específico valida SHA-256 antes y después de copiar y comprueba que la ruta del manifiesto resuelva al DLL verificado. La DLL queda en `bin/` y el XML de la extensión en `extra/`, según el [formato de paquetes con extensiones de Dynamo](https://developer.dynamobim.org/05-Package-Deployment/deploying-extensions-on-the-package-manager.html). No modifica modelos RVT. Para reparar manualmente, ejecuta `dynamo/extension/install.ps1` desde la raíz; el instalador completo es el recomendado.

El instalador individual valida el SHA-256 antes y después de copiar y comprueba que la ruta del manifiesto resuelva al DLL verificado. Para reemplazar una versión distinta, usa `dynamo/extension/install.ps1 -Force` desde la raíz y revisa el destino. No modifica modelos RVT.

## Pruebas

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\dynamo\extension\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run_dynamo_extension_client_smoke.ps1
python -B .\tests\run_dynamo_extension_codex_smoke.py --mcp
```

El smoke C# usa el cliente real de la extensión para iniciar Codex app-server, pedir la creación aprobada de una rutina de solo lectura, verificar `.py`/`.dyn`/manifiesto/hash, volver a listarla y probar imagen + archivo de texto como adjuntos. Limpia su rutina y copias temporales. El smoke Python comprueba aparte `list_saved_routines` con las rutinas guardadas del proyecto. No abren Revit ni Dynamo. La carga visual del panel y el loop Dynamo → Revit se registran aparte en `docs/PROTOTYPE_STATUS.md`.
