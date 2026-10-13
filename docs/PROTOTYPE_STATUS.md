# Estado de ARQ-IA

**Actualizado:** 13 de octubre de 2026
**Estado:** prototipo funcional con límites pendientes de validación; no es una herramienta de producción aprobada.

## Qué incluye

- Add-in para Revit 2027 y extensión lateral para Dynamo 4.0.2.
- Chat local de Codex app-server, adjuntos, selector de modelos e historial por carpeta de proyecto.
- Configuración local de `rvt-mcp`, un MCP para crear y listar rutinas, y otro para invocar el bridge Revit/Dynamo.
- Plantilla Dynamo con un nodo PythonNet3 en modo Manual y catálogo inicial de materiales sin IDs ni datos de modelos.
- Instalador para Windows que configura las rutas del clon y verifica hashes de los binarios distribuidos.

## Evidencia y límites

- Hay evidencia histórica de lectura de vista/selección mediante `rvt-mcp`, lectura de materiales en un modelo de muestra y ejecución de una rutina fija de solo lectura en un RVT descartable. Esto no valida todas las operaciones ni la versión `rvt-mcp` 1.1.0 de una instalación nueva.
- La ejecución genérica de rutinas, las escrituras complejas y la experiencia completa en otro PC siguen pendientes de validación. El nombre de herramienta, los hashes o una compilación no equivalen a prueba de comportamiento.
- No se ejecutaron Revit, Dynamo, el instalador ni pruebas de producto durante esta revisión de seguridad. La próxima prueba debe usar un RVT descartable.

## Seguridad y datos

- **Acceso controlado** es el modo inicial. **Acceso completo** requiere confirmación al activarse y aplica `approvalPolicy=never` con `danger-full-access` solo a esa conversación; abrir o crear otra la devuelve a controlado. No cambia la configuración global de Codex.
- Las rutinas PythonNet3 ejecutan dentro del proceso Revit con los permisos de Windows del usuario. La validación de ruta del RVT no es un sandbox del sistema: el código podría acceder a otros archivos o a la red. Inspecciona el código generado y ejecuta solo sobre una copia descartable.
- `.arq-ia/`, el historial local y `logs/attachments/` están excluidos de Git y del ZIP. El historial no está cifrado; adjuntos y conversación pueden contener información de proyecto que Codex procese. Un cierre inesperado puede dejar archivos temporales locales.
- Los manifiestos muestran **Branko Plaza**, pero los DLL no están firmados con un certificado confiable. El nombre de proveedor del manifiesto no autentica al editor; Revit puede seguir mostrando un aviso de editor desconocido.

## Repositorio público

- La rama publicada es `main`. La carpeta versionada contiene código, documentación, plantillas, materiales de referencia y rutinas de ejemplo; el historial Git revisado no incluye modelos Revit ni configuración local.
- No hay archivo `LICENSE`, política `SECURITY.md` ni flujo CI. Los rulesets de GitHub consultados no existen; el estado de protección clásica de `main` no se pudo leer. La licencia y los controles del repositorio requieren decisión/configuración del propietario.
- La comprobación local de secretos es dirigida a formatos conocidos y rutas locales; no es un escáner exhaustivo. No se ejecutó un escáner automatizado en esta revisión.

Consulta [la auditoría de seguridad](SECURITY_AUDIT.md), [la arquitectura](ARCHITECTURE_DECISION.md) y [las instrucciones de instalación](INSTRUCCIONES_OTRO_PC.md) antes de distribuir el prototipo.
