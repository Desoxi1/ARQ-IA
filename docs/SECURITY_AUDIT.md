# Auditoría de seguridad y preparación del repositorio

**Fecha:** 13 de octubre de 2026
**Alcance:** archivos versionados, historial de nombres/objetos Git, instaladores, configuración MCP, bridge Revit/Dynamo, cliente Codex, adjuntos, historial local y paquete portable.

## Resultado

No encontré credenciales reconocibles, rutas personales, modelos Revit ni configuraciones locales en el árbol versionado o en el historial revisado. La búsqueda cubrió formatos comunes de GitHub, OpenAI, AWS, Slack y Google, encabezados de llaves privadas y rutas de perfil de Windows. El empaquetador bloquea rutas Windows bajo perfiles de usuario. Es una comprobación dirigida, no una garantía de ausencia de secretos.

No se ejecutaron el instalador, Revit, Dynamo ni rutinas. No se modificó un modelo. No se ejecutó un escáner automatizado en esta revisión; la herramienta de análisis automatizado no estuvo disponible, por lo que el resultado es una revisión estática manual y acotada.

## Correcciones aplicadas

- Acceso completo requiere una confirmación única con una explicación de sus permisos. El permiso se restablece al crear o abrir otra conversación, y el estado inicial sigue siendo Acceso controlado.
- El empaquetador bloquea más rutas y extensiones locales sensibles y reconoce patrones comunes de tokens y llaves. Su salida se llama `TARGETED_SECRET_PATTERNS_AND_LOCAL_ARTIFACTS`; no pretende reemplazar un escáner de secretos.
- Se retiraron de la documentación pública registros históricos contradictorios y detalles de estado local que ya no servían como instrucciones. Los documentos de entorno y estado ahora distinguen evidencia histórica de capacidades pendientes.
- README aclara el uso previsto entre colegas, la visibilidad pública y que no se eligió una licencia de código abierto.

## Hallazgos que siguen abiertos

### Alto — las rutinas PythonNet3 no están aisladas del sistema operativo

El ejecutor MCP de prueba limita el RVT a `revit/test-models/`, valida hashes y exige que la llamada declare posibles escrituras. La operación separada para el RVT abierto exige Dynamo abierto en un workspace vacío, revalida documento y hash, y muestra un diálogo Sí/No de Revit antes de cargar y ejecutar por cada solicitud, incluso en Acceso completo. No guarda ni sincroniza el RVT. La preparación carga el borrador sin solicitar ejecución; que la apertura no ejecute automáticamente el nodo aún debe comprobarse en Dynamo 4.0.2. Si el usuario pulsa Ejecutar manualmente, esa ejecución queda fuera de la confirmación y de la verificación del bridge. PythonNet3 corre dentro de Revit con los permisos de Windows del usuario y puede acceder a otros archivos o a la red. El modo Acceso completo también entrega a Codex `danger-full-access` para la conversación confirmada. Revisa el Python y el alcance antes de ejecutar. El diálogo regula el momento de ejecución, pero no aísla el script del sistema operativo.

### Medio — los binarios no autentican al editor

Los manifiestos Revit identifican `Branko Plaza` y los scripts verifican SHA-256 de los DLL distribuidos. Los hashes detectan diferencias respecto del archivo acompañado, pero no prueban quién lo publicó. Los DLL no tienen firma de código de un certificado de editor confiable; Revit puede continuar mostrando “Fabricante desconocido”. Para distribución profesional hace falta una firma verificable y proteger la clave privada fuera del repositorio.

### Medio — faltan controles de publicación

- No existe `LICENSE`. El uso previsto declarado es entre colegas del equipo, pero el repositorio está público y ese propósito no restringe quién puede ver su contenido. No se ha elegido una licencia de código abierto; si se requiere acceso solo interno, el propietario debe cambiar la visibilidad a privada.
- No existe una política `SECURITY.md` ni un canal privado de reporte documentado.
- No hay CI versionado. La API de GitHub mostró cero rulesets; la lectura de protección clásica de `main` no estuvo disponible, así que ese ajuste queda sin confirmar.

### Bajo — datos locales de conversaciones y adjuntos

El historial de `.arq-ia/` se guarda como texto local sin cifrar. Los adjuntos se copian temporalmente a `logs/attachments/`; se eliminan al cerrar normalmente el panel, pero un cierre inesperado puede dejar archivos. Estas rutas están ignoradas por Git y bloqueadas por el empaquetador. Adjuntos e historial pueden contener información de proyectos; compártelos con Codex solo si tienes autorización para hacerlo.

## Controles observados

- El release de `rvt-mcp` se descarga desde su URL oficial con versión y SHA-256 fijados.
- La configuración MCP y la confianza de Codex se crean para la ruta exacta del clon; no se guarda una API key en el repositorio.
- El bridge usa una tubería local limitada al usuario actual, solicitudes acotadas y un conjunto cerrado de operaciones.
- Las rutinas Dynamo generadas se guardan como grafos Manuales. La preparación pide abrir el borrador en el documento activo sin invocar el ejecutor y se detiene si el workspace tiene nodos/notas; la ausencia de ejecución automática está pendiente de validación viva. La ejecución genérica mediante el MCP requiere un RVT bajo `revit/test-models/` y pasa por validación de integridad.
- `.gitignore` y el empaquetador excluyen `.codex/`, `.arq-ia/`, modelos, adjuntos, contenido operativo de `logs/`, resultados y cachés de `vendor/`; los README de esas carpetas sí se versionan para explicar su propósito.

## Cierre

El repositorio está ordenado para clonar y revisar, pero no debe describirse como seguro o listo para producción. Antes de distribuirlo a arquitectos externos, define la licencia, publica un canal de reporte privado, activa controles de rama y CI, y decide cómo firmar los binarios. La ejecución genérica requiere una evaluación de riesgo aparte porque el proceso actual no puede contener código PythonNet3 dentro de un límite del sistema operativo.
