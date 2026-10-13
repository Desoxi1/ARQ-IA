# Estado de ARQ-IA

- **Actualizado:** 13 de octubre de 2026
- **Versión declarada del paquete Dynamo:** 0.5.1
- **Estado:** prototipo funcional con capacidades que aún requieren validación en Revit/Dynamo; no es una herramienta aprobada para producción.

Este archivo es la fuente canónica del estado. Las pruebas antiguas se interpretan según su fecha y versión; no demuestran por sí solas el comportamiento de la versión 0.5.1 ni de una instalación nueva.

## Qué incluye el repositorio

- Add-in de chat para Revit 2027 y extensión lateral para Dynamo 4.0.2.
- Cliente Codex app-server, adjuntos, selector de modelos e historial local por clon.
- Configuración local de `rvt-mcp`, MCP para crear/listar rutinas y MCP Runner para hablar con el bridge Revit/Dynamo.
- Rutinas de ejemplo, plantilla PythonNet3 en modo Manual, catálogo común de 208 nombres de materiales y un instalador por usuario.

## Evidencia registrada y límites

- **Lectura de Revit:** consultas de vista y selección pasaron históricamente con `rvt-mcp` v0.6.2. Los clones nuevos fijan `rvt-mcp` v1.1.0; esa integración aún no tiene una validación completa en instalación limpia.
- **Panel y Codex:** los registros históricos muestran una versión anterior de la extensión cargada y un smoke del cliente que procesó texto e imagen. La versión de paquete actual es 0.5.1; no hay una prueba documentada de instalación limpia de esa versión en otro PC.
- **Dynamo:** la rutina fija `read_active_view_selection` pasó en Revit real con un RVT descartable el 5 de octubre de 2026. La ejecución genérica al cambiar de grafo agotó el tiempo en un intento anterior; el ajuste posterior sigue sin validación viva.
- **Preparar rutina en el documento abierto:** el bridge y MCP ahora incluyen una operación separada que valida hashes y documento activo, solicita abrir el `.dyn` en Manual sin solicitar ejecución, y rechaza reemplazar un workspace con nodos/notas. El bridge compiló y se empaquetó (48.128 bytes; SHA-256 `ec2f814a1f9e600cf17a13d49031e92a615a9335b41849fc37505444cd8dad08`) y quedó instalado en modo preparado para la siguiente sesión de Revit. La sesión abierta conserva el DLL anterior. Aún no se comprobó en Dynamo 4.0.2 que abrir el grafo no ejecute un nodo. No se modificó un RVT.
- **Panel de chat (13 de octubre de 2026):** los mensajes del chat e historial se muestran en controles de texto de solo lectura que permiten seleccionar y copiar; cada mensaje tiene un botón **Copiar**. Las respuestas aceptan encabezados, listas, citas y bloques de código con formato. El selector muestra modelo y nivel de razonamiento disponibles en el catálogo local de Codex; el panel guarda esos metadatos por respuesta y los recupera al abrir historial. Si el servidor rechaza modelo/esfuerzo, el panel informa el fallback a los valores predeterminados en vez de atribuir la opción solicitada. Los detalles técnicos de llamadas MCP también son seleccionables.
- **Materiales:** el 13 de octubre una consulta en un modelo de muestra devolvió 300 de 300 materiales definidos. La base compartida contiene 208 nombres curados; no guarda el inventario completo de ese modelo ni sus IDs, cantidades o assets.
- **Escrituras:** el cambio de color de muros y las escrituras complejas no tienen una ejecución viva verificada desde ARQ-IA. Una compilación, un hash válido o una respuesta de plan no prueban que el modelo haya cambiado correctamente.
- **Verificación del panel (13 de octubre de 2026):** smoke del historial local `PASS` (incluye persistencia de modelo/esfuerzo); 9 comprobaciones dirigidas del paquete Dynamo `PASS`; builds Release de Revit y Dynamo `PASS`, ambos con 0 errores y 0 advertencias. Las extensiones compiladas quedaron instaladas para Revit 2027 y Dynamo Revit 27.0; la instalación de Dynamo confirmó el SHA-256 `783bf5d0578a3449fce86e7d88c5971fbebfd8c6f4cccb537dba12e07fa8fdf6`. La interfaz nueva aún requiere comprobarse visualmente y usar una respuesta real al reabrir Revit; esta sesión no ejecutó rutinas ni modificó el RVT.

## Seguridad y datos locales

- **Acceso controlado** es el modo inicial. **Acceso completo** requiere confirmación y aplica permisos amplios solo a esa conversación; no cambia la configuración global de Codex.
- PythonNet3 corre dentro del proceso Revit con los permisos de Windows del usuario. La restricción del runner sobre el RVT no aísla archivos ni red. Revisa el script y usa una copia descartable.
- `.codex/` contiene configuración MCP local; `.arq-ia/` contiene perfil, historial y preferencias locales. El contenido operativo de `logs/`, `artifacts/` y `vendor/` se excluye de Git; los README de esas carpetas explican su propósito.
- Los manifiestos indican **Branko Plaza**, pero los DLL no tienen firma de editor confiable. Revit puede seguir mostrando «Fabricante desconocido» hasta que se use un certificado de firma confiable.

## Repositorio y uso previsto

- La rama publicada es `main`. El repositorio está público; el uso previsto es entre colegas del equipo. La visibilidad pública significa que el contenido puede ser visto fuera del equipo.
- No hay archivo `LICENSE`, `SECURITY.md` ni CI versionado. No se ha elegido una licencia de código abierto. Si el repositorio debe limitarse realmente al equipo, hay que cambiar su visibilidad a privado.
- La búsqueda de secretos documentada fue dirigida a formatos conocidos; no equivale a un escáner exhaustivo. Consulta la [auditoría de seguridad](SECURITY_AUDIT.md).

## Siguiente validación

En una sesión controlada de Dynamo 4.0.2, confirmar una sola vez que la preparación abre el grafo en Manual sin ejecutar nodos, que se niega a reemplazar un workspace con contenido y que el RVT no se guarda automáticamente. Después, la rutina fija de lectura, una escritura acotada y el caso de color siguen siendo validaciones separadas. El [registro de evidencia](../tests/MCP_SMOKE_TEST.md) explica qué ya se probó y con qué límites.
