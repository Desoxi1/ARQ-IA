# Estado de ARQ-IA

<!-- PRODUCT-STATE-20261013:BEGIN -->

## Estado reconciliado — 2026-10-13

- **Corrección de interpretación y portabilidad preparada:** el panel ya no intercepta “pintar el piso” antes de consultar Revit; Codex recibe la petición, distingue override de material y puede usar `revit_override_element_graphics`. El inventario MCP del binario local v0.6.2 confirmó esa herramienta y sus argumentos. El clon nuevo apunta a `rvt-mcp` v1.1.0 con SHA-256 fijo; esta combinación aún no se ha instalado en otro PC. Se retiraron tres scripts de migración/registro obsoletos; los instaladores de componentes necesarios permanecen.
- **Evidencia de esta sesión:** ambos paneles compilaron sin errores y quedaron instalados por usuario; hashes Dynamo `bf4653bb04cb141010020eb9b5591c740c0f28cee4e623611d1ecd3fcc91f94e` y Revit `45eb84c24c4157517c0ab78ef6bc4d82d7574dd882758864e0c218ed6cb76061`. La configuración MCP local ya expone `revit_override_element_graphics`. `doctor.ps1`: 14 PASS, 3 WAIT, 0 WARN, 0 FAIL con Revit cerrado. **Pendiente:** comprobar desde el chat visible una solicitud de color sobre RVT descartable. El ejemplo de muros rojos sigue siendo una ruta diseñada, no un éxito en Revit.
- **Estado: BLOCKED para aceptación de ejecución genérica.** La rutina fija de lectura tiene evidencia histórica aprobada; la ejecución genérica y escrituras complejas no se declaran validadas.
- **Versión anterior documentada:** HEAD 26626f4, 08-10, sin origin. Dynamo 0.4.0 agregó selector Económico/Estándar/Potente y una aclaración local que ahora se retiró porque bloqueaba la consulta del modelo y perdía contexto. No hay ahorro de cuota medido.
- **Instalación comprobada por archivos:** bridge distribuido e instalación referenciada coinciden, SHA-256 43ac7838fd55dfb7f67ad5a7ee102f72861c70d29c1350a1c86956c9780ced4f. Es el ajuste preparado el 05-10. Add-in distribuido/instalado y manifiesto Dynamo también contrastados por hashes.
- **Bloqueo vigente en evidencia:** PROTOTYPE_STATUS del 05-10 mantiene revit_execution_timeout_unknown al cambiar de grafo; no se encontró validación documental posterior que lo resuelva. Conservar causas probables como hipótesis.
- **Validación histórica:** build, render y 56 pruebas offline constan en la documentación anterior; no se reejecutaron. El panel visible, mensaje/adjunto y lectura genérica requieren comprobación viva aparte.
- **Siguiente acción:** en la próxima sesión normal sobre copia descartable, verificar modelo tras timeout, cargar el bridge preparado y comprobar lectura genérica con executed_and_verified_read_only; después probar mensaje/adjunto desde el panel. Una escritura requiere su alcance y aprobación específica.
- **Diseño/publicación:** arquitectura y bucle de ejecución documentados; kit manual ChatGPT→Dynamo es otro proyecto. ZIP ARQ-IA-26626f4.zip existe con SHA-256 22e93998c6887f1fc2b1a790c7ee06a0c5ca1c46be8261819b6af7edb4e485c6. Repositorio sin remoto; no se publicó en esta tarea.
- **Método:** revisión independiente Challenger de AGENTS, documentos, código puntual, Git, manifiestos y hashes. Sin ejecutar Revit/Dynamo, pruebas, scripts, modelos, logs o credenciales. Confianza alta sobre artefactos; aceptación viva no comprobada.

### Registro de avance

13-10-2026: se habilitó la capacidad gráfica tipada y se corrigió el bloqueo local de lenguaje natural; se actualizó el pin de instalación para clones nuevos y se simplificó la carpeta. Se conserva el bloqueo de ejecución genérica y la historia previa. Documentar preparación no cierra el timeout.

## Evidencia histórica previa (05-10-2026)

<!-- PRODUCT-STATE-20261013:END -->


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
