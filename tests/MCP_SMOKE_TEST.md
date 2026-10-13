# Registro de evidencia de pruebas

**Reconciliado:** 13 de octubre de 2026.

Este archivo resume resultados anteriores y el alcance de cada uno. No es una afirmación de que las pruebas se hayan repetido sobre el commit actual. El [estado vigente](../docs/PROTOTYPE_STATUS.md) es la fuente canónica; las instrucciones para repetir validaciones están en [README de pruebas](README.md).

## Resultados registrados

| Área | Resultado registrado | Alcance y límite |
|---|---|---|
| Panel Dynamo | Carga observada en una sesión de desarrollo | Un log de la versión 0.3.0 registró `Loaded Package ARQ-IA` y la adición de la View Extension. El paquete actual declara 0.5.1; no hay evidencia de una instalación limpia de esa versión en otro PC. |
| Codex app-server y adjuntos | PASS histórico, 2 de octubre | El cliente C# procesó texto e imagen y creó/listó una rutina de prueba. No ejecutó Revit ni código adjunto. |
| Lectura de vista y selección | PASS histórico con `rvt-mcp` v0.6.2 | Consultas de solo lectura en Revit 2027. No demuestra la integración de v1.1.0 fijada para clones nuevos. |
| Rutina fija `read_active_view_selection` | PASS histórico, 5 de octubre | PythonNet3 se ejecutó en un RVT descartable; el bridge comprobó que el documento no cambió. Dynamo ya estaba abierto y vinculado al documento. |
| Inventario de materiales de muestra | PASS histórico, 13 de octubre | Revit devolvió 300/300 materiales definidos, sin límite ni omisiones, en un modelo de muestra. No representa todos los modelos ni identifica por sí solo los materiales usados por elementos. `materials/base.csv` contiene 208 nombres curados. |
| Suite offline | Último registro: PASS, 44 pruebas, 2 de octubre | El código del repositorio cambió después; este conteo no se atribuye al commit actual. La limpieza documental no ejecutó pruebas. |
| Runner genérico al cambiar de grafo | PENDIENTE | Un intento anterior agotó el tiempo; el ajuste posterior no tiene una ejecución viva registrada. Un smoke contra bridge falso valida contratos, no Revit/Dynamo. |
| Escrituras y caso de muros rojos | PENDIENTE | No hay resultado vivo verificado desde ARQ-IA para la escritura de Comments, color gráfico u otras ediciones. |
| Instalación limpia en otro PC | PENDIENTE | No se ha validado el flujo completo desde un clon nuevo con la versión actual. |

## Lectura segura de estos resultados

- Un build, hash coincidente, diagnóstico de archivos instalados o llamada contra un bridge falso no demuestran que Revit haya realizado una operación.
- Si una ejecución devuelve `revit_execution_timeout_unknown`, primero consulta el estado del modelo; no repitas una escritura a ciegas.
- Las rutinas genéricas deben usarse solo con un RVT local guardado y descartable bajo `revit/test-models/`. Revisa el script antes de ejecutarlo: PythonNet3 tiene los permisos del proceso Revit.
- Las pruebas en vivo y offline se describen en [tests/README.md](README.md); este registro no sustituye la revisión visual ni el resultado del modelo.
