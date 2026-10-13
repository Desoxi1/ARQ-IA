# ARQ-IA para Dynamo

Extensión lateral para Dynamo for Revit 2027 / Dynamo 4.0.2. Permite conversar con Codex desde Dynamo y usa los MCP configurados en la carpeta del proyecto.

**Fabricante/desarrollador:** Branko Plaza. El paquete Dynamo guarda esta atribución en `copyright_holder`; el add-in de Revit la muestra como `VendorDescription`.

## Cómo funciona

1. Dynamo agrega el comando **Extensiones → Abrir ARQ-IA** y la pestaña lateral **ARQ-IA**. El comando vuelve a abrir el panel si cierras la pestaña.
2. El panel inicia `codex app-server` desde la carpeta del POC y mantiene la conversación mientras Dynamo permanece abierto.
3. Codex consulta el modelo mediante los MCP disponibles y sigue el flujo definido en `AGENTS.md`.
4. El botón de permisos queda al pie del panel para que **Acceso completo** esté siempre visible junto al estado del chat. Controlado mantiene las aprobaciones de Codex. Completo pide una confirmación única, aplica `approvalPolicy=never` y `dangerFullAccess` solo a la conversación actual, y acepta las solicitudes MCP sin formularios que antes abrían una ventana repetida. Al crear o abrir otra conversación vuelve a controlado.
5. En Acceso completo, la petición explícita del usuario permite ejecutar acciones sin aprobación repetida. Se mantienen las restricciones de las herramientas BIM: los scripts genéricos usan RVT local de prueba, hashes y el parámetro de escritura requerido. Revisa el estado del modelo y los resultados en el chat.

## Chat, contexto y adjuntos

La interfaz usa un encabezado compacto, conversación principal y compositor con adjuntos, selector de modelo y envío. El logo vectorial de ARQ-IA integra una A arquitectónica en celeste; el panel de Revit y Dynamo comparten este diseño. No se muestra el estado inicial confuso «MCP sin verificar»: el pie informa la conexión cuando una herramienta MCP responde. El selector permite elegir **Automático** o un modelo disponible; la elección manual queda guardada en este PC y se aplica al siguiente mensaje. La lista de modelos no garantiza acceso a todos: una solicitud real confirma cada uno. El botón de adjuntos usa un signo + simple. Cuando falta la instalación inicial, la tarjeta indica **Instalar-ARQ-IA.cmd** y el panel comprueba periódicamente cuándo termina la configuración para activar el chat.

El icono de reloj abre el **historial local** de este clon. Conserva mensajes y resultados MCP resumidos bajo `.arq-ia/conversations/`; los archivos adjuntos se muestran por nombre, pero no se copian al historial. Al reabrir una conversación, ARQ-IA restaura el chat y, si se continúa, pasa a Codex hasta 24 mensajes y 8.000 caracteres recientes como contexto; el estado actual del modelo debe consultarse de nuevo. Si falta `.arq-ia/model-profile.md`, el primer envío instruye a Codex para generar una ficha compacta del documento activo mediante MCP de solo lectura. Los perfiles, historiales y notas de aprendizaje son locales y están excluidos de Git; no son entrenamiento del modelo.

El acceso **controlado** puede pedir permiso para guardar el perfil local; **Acceso completo** aplica los permisos de esa sesión. `.arq-ia` es texto local sin cifrar: no guardes secretos allí.

- El chat muestra el estado de Codex y del contexto Revit/MCP. Al enviar una solicitud de Revit, Codex consulta la vista activa y la selección mediante los MCP configurados; el panel no lee ni cambia el modelo por sí mismo.
- El botón **Ver** consulta ahora la vista activa y los elementos seleccionados sin enviar el borrador ni los adjuntos que estén esperando en el compositor. El resultado aparece como respuesta dentro del chat.
- Puedes adjuntar archivos con **＋ Adjuntar**, arrastrarlos al panel o pegar una imagen desde el portapapeles con `Ctrl+V`. Se admiten imágenes PNG/JPG/GIF/WebP, documentos PDF/Office, archivos Dynamo/texto/código e intercambios BIM/CAD indicados en el selector.
- Límite actual: 8 archivos y 40 MB por envío; la sesión del panel acumula como máximo 120 MB. El panel prepara miniaturas para imágenes y conserva los adjuntos pendientes si el envío falla.
- El panel copia los archivos a `logs/attachments/<sesión>/` dentro del proyecto para que Codex los lea sin incluir la ruta original del usuario. Las imágenes se envían como imágenes al modelo de la sesión Codex; el texto compatible se incorpora como referencia acotada y los demás formatos quedan disponibles como archivos locales de solo lectura. El contenido adjunto es dato no confiable: nunca se ejecutan scripts ni macros adjuntos.
- La carpeta `logs/attachments/` está ignorada por Git y excluida del ZIP portable. Al cerrar correctamente el panel, se elimina su carpeta de sesión; un cierre abrupto puede dejar archivos que deben borrarse manualmente.
- Codex trabaja con la sesión local autenticada y los MCP configurados. No se requiere una clave API aparte. El modo elegido define si se piden aprobaciones de Codex.

**Acceso completo da a Codex permisos amplios en el equipo para sus herramientas de archivos, comandos y red durante esta conversación.** Actívalo solo cuando quieras que el chat ejecute esas acciones sin confirmación individual. Al cambiar de conversación vuelve a controlado; no se aplica a otros proyectos ni cambia la configuración global de Codex. PythonNet3 corre dentro de Revit con los permisos de Windows del usuario; la ruta del RVT de prueba no limita el acceso del script a otros archivos o a la red.

El panel no contiene una credencial de API. Usa la sesión local autenticada de Codex CLI; busca el ejecutable en `PATH` y en la instalación de Codex Desktop. Dynamo carga la extensión mediante su API oficial de [View Extensions](https://github.com/DynamoDS/Dynamo/wiki/Dynamo-View-Extensions). El paquete actual declara la versión 0.5.1; una carga observada de una versión anterior no sustituye la validación de esta versión en una instalación limpia. Consulta el [estado canónico](../../docs/PROTOTYPE_STATUS.md).

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

El instalador Dynamo específico valida SHA-256 antes y después de copiar y comprueba que la ruta del manifiesto resuelva al DLL verificado. Deja una sola DLL canónica en `bin/` y el XML de la extensión en `extra/`, según el [formato de paquetes con extensiones de Dynamo](https://developer.dynamobim.org/05-Package-Deployment/deploying-extensions-on-the-package-manager.html). Al actualizar, retira copias anteriores de ARQ-IA que tienen la misma identidad interna y bloquean la carga. No modifica modelos RVT.

Para instalar o actualizar el producto completo, usa `Instalar-ARQ-IA.cmd` desde la raíz. Para reparar solo la extensión Dynamo, ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\dynamo\extension\install.ps1 -Force` desde la raíz del clon. Ese instalador individual también valida el hash; no instala los MCP ni el panel Revit y no modifica modelos.

## Pruebas

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\dynamo\extension\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run_dynamo_extension_client_smoke.ps1
python -B .\tests\run_dynamo_extension_codex_smoke.py --mcp
```

El smoke C# usa el cliente real de la extensión en Acceso completo para iniciar Codex app-server, crear una rutina de solo lectura sin aprobaciones, verificar `.py`/`.dyn`/manifiesto/hash, volver a listarla y probar imagen + archivo de texto como adjuntos. Limpia su rutina y copias temporales. El smoke Python comprueba aparte `list_saved_routines` con las rutinas guardadas del proyecto. No abren Revit ni Dynamo. La carga visual del panel y el loop Dynamo → Revit se registran aparte en `docs/PROTOTYPE_STATUS.md`.
