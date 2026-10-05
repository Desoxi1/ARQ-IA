# Estado de ARQ-IA

**Fecha:** 5 de octubre de 2026. **Equipo comprobado:** Revit 2027.0, Dynamo 4.0.2, PythonNet3 Engine 1.4.8, Codex Desktop/CLI y `rvt-mcp` 0.6.2.

## Lo que funciona

- El instalador local colocó el add-in de Revit, el paquete de Dynamo por usuario y tres MCP del proyecto. El menú de Dynamo **Extensiones → Abrir ARQ-IA** y la ficha lateral aparecen en el log vivo: `Loaded Package ARQ-IA 0.3.0` y `ARQ-IA ... view extension is added`.
- El diagnóstico llegó a **18 PASS, 0 WAIT, 0 WARN, 0 FAIL** con Revit y Dynamo abiertos sobre una copia descartable. El bridge respondió al ping y confirmó Dynamo asociado al documento activo.
- El cliente del chat abrió Codex app-server, creó/listó una rutina mediante MCP y procesó adjuntos de imagen y texto. La consulta de vista/selección por `rvt-mcp` pasó en pruebas previas. Esto aún no equivale a una solicitud enviada por una persona desde la ficha visible.
- Compilan los tres DLL entregados y la suite offline pasó 56 pruebas. La compilación del bridge preparada para el próximo inicio tiene SHA-256 `43ac7838fd55dfb7f67ad5a7ee102f72861c70d29c1350a1c86956c9780ced4f`.
- La rutina fija de solo lectura **sí se ejecutó en vivo**: `REVIT_BRIDGE_PING=PASS` y `DYNAMO_READONLY_RUN=PASS` sobre el RVT descartable. PythonNet3 produjo el resultado y el bridge verificó cero cambios de elementos.
- El registro antiguo de Dynamo en Program Files se retiró. El paquete activo está bajo `%APPDATA%\Dynamo\Dynamo Revit\27.0\packages\ARQ-IA`.

## Bloqueo actual

La prueba posterior de una rutina genérica de solo lectura agotó el tiempo con `revit_execution_timeout_unknown` al cambiar de grafo. El log de Dynamo registró que intentó guardar el grafo temporal anterior; es una causa probable de la espera, aunque la traza no demuestra exactamente qué diálogo la produjo. **La ejecución genérica y las escrituras complejas no están validadas.** No se usó un proyecto productivo.

El [código oficial de DynamoRevit](https://github.com/DynamoDS/DynamoRevit/blob/master/src/DynamoRevit/DynamoRevit.cs) distingue ejecución con UI ya abierta de modo de automatización. El bridge devuelve el control a Revit, espera el archivo de resultado sin bloquear la interfaz y vuelve a verificar dentro de un evento de Revit. Esto hizo pasar la rutina fija. También se eliminó una segunda invocación de `Run()` que podía duplicar una escritura. Para la próxima sesión, el bridge conservará el archivo del grafo temporal mientras siga abierto en Dynamo, evitando que Dynamo lo trate como inexistente al cambiar de grafo. Este último ajuste está instalado pero no se ha probado en vivo; no se pedirá otro reinicio durante esta ronda.

## Próxima comprobación

1. En la siguiente sesión normal de Revit, el manifiesto cargará el bridge preparado. Abre solo el RVT descartable y Dynamo.
2. Ejecuta primero una rutina genérica de solo lectura y confirma que retorna `executed_and_verified_read_only` sin esperar un cuadro de guardado del grafo anterior.
3. Prueba un mensaje y un adjunto directamente desde el panel visible. Una escritura se probará únicamente en la copia descartable, con revisión del script y aprobación.

Los modelos, adjuntos temporales, credenciales y resultados locales están excluidos de Git y del ZIP portable. La metadata de fabricante es **Branko Plaza**; los DLL aún no tienen firma de editor confiable para Windows.
