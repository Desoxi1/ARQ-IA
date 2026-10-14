# Estándar de operación y validación BIM

Este estándar define cómo ARQ-IA convierte una solicitud en un cambio comprobable. Es una guía de trabajo, no una certificación ni una garantía de ausencia de errores.

## Secuencia mínima

1. **Interpretar:** conservar el lenguaje natural del arquitecto y mapearlo a la categoría, tipo/instancia, vista/nivel, parámetro, unidad y efecto Revit que correspondan. Consultar el glosario si hay sinónimos.
2. **Leer:** identificar documento y vista activos. Consultar solo los elementos y parámetros necesarios. En una imagen, localizar referencias geométricas; no convertir píxeles, colores ni conteos por sí solos en IDs.
3. **Delimitar:** declarar el conjunto objetivo, cantidad consultada, cambios esperados y exclusiones. Aclarar solo las decisiones que cambian el resultado. Si la consulta está truncada, reducir alcance o informar cobertura parcial.
4. **Elegir herramienta:** operación Revit tipada existente primero; Dynamo Manual cuando haga falta lógica o repetición; API/add-in persistente solo si hay una brecha funcional real. No inventar herramientas ni dependencias.
5. **Prevalidar:** revisar tipos, parámetros, unidades, coordenadas, nivel/vista, permisos, elementos duplicados, enlaces/anfitriones y estado de trabajo compartido. Mostrar resumen/dry-run antes de un cambio masivo.
6. **Autorizar y ejecutar:** respetar el modo de acceso de ARQ-IA y la confirmación propia de Revit para el runner PythonNet3 en el modelo abierto. Un permiso amplio no convierte una petición ambigua en autorización. Cambios geométricos o de estructura requieren fuentes y decisiones técnicas suficientes.
7. **Verificar:** volver a leer el documento y comparar categoría, cantidad, IDs, valores, unidades, vista y advertencias con lo esperado. Si el resultado es parcial, falló la lectura o hubo timeout, decirlo; no reintentar escrituras a ciegas.
8. **Reutilizar:** conservar rutina con versión de Revit/Dynamo, entradas, alcance, hash y evidencia de prueba. Marcarla como borrador hasta verificarla en modelo descartable. Registrar aprendizaje local solo después de confirmar hechos; los fallos quedan como fallos.

## Pruebas de una rutina de escritura

Probar primero en RVT descartable. Cubrir selección vacía, elementos que no cumplen el filtro, parámetros ausentes/de solo lectura, tipos o unidades inválidos, duplicados, cancelación, error a mitad del lote, rollback, reejecución y resultado posterior. Comparar estado antes/después y verificar desde una lectura independiente de Revit. No usar capturas, respuesta textual, JSON `completed` ni compilación como sustituto de esa comprobación.

## Criterios para detenerse

Detener la escritura si no se puede identificar modelo/objetivo, hay varias interpretaciones con efectos distintos, la unidad o coordenada no es conocida, los IDs no corresponden a la vista/nivel, la evidencia no cubre el conjunto pedido, o la comprobación posterior no coincide. Informar el bloqueo concreto y una única acción siguiente.

## Eficiencia

Usar la guía pertinente, una consulta por vez al hilo de Revit y resúmenes con excepciones. Reutilizar una rutina ya verificada; no enviar el RVT entero, todo el catálogo, la conversación completa ni repetir referencias estables en cada turno. Más contexto no sustituye evidencia.
