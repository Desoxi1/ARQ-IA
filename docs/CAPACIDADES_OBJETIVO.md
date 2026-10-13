# Capacidades objetivo y límites de ARQ-IA

**13-10-2026.** Este documento separa lo diseñado de lo comprobado. La evidencia vigente está en [PROTOTYPE_STATUS.md](PROTOTYPE_STATUS.md).

## Resultado buscado

Un arquitecto describe en español habitual o técnico lo que necesita; ARQ-IA consulta el modelo activo, identifica categorías, niveles, vistas, tipos, parámetros y unidades reales, pregunta solo lo que cambia el resultado y después comprueba el resultado. El panel ofrece acceso controlado y acceso completo por sesión; completo evita confirmaciones repetidas de Codex cuando el usuario pide la acción. Las rutinas Dynamo/PythonNet3 quedan guardadas para reutilizarse.

El asistente no “absorbe” automáticamente todos los MCP ni aprende de cualquier conversación. Un MCP expone herramientas; Codex las descubre y decide cuáles usar. El conocimiento persistente proviene de este glosario, guías técnicas y rutinas verificadas en Git. Añadir otro MCP exige revisar compatibilidad, licencia, seguridad y autoridad de escritura.

## Disponible en el paquete

| Capacidad | Implementación | Evidencia |
|---|---|---|
| Chat y adjuntos en Revit y Dynamo | Paneles propios con Codex app-server | Cliente conectado y procesamiento de adjuntos comprobados fuera del panel visible; carga histórica del panel de Dynamo. |
| Vista activa y selección | `rvt-mcp` de bimwright | Consulta de solo lectura pasada en Revit. |
| Operaciones tipadas de creación/edición | Lista acotada de `rvt-mcp`; control de Codex según modo del chat | Inventario de herramientas; cada escritura requiere prueba individual. |
| Color de presentación por elemento | `revit_override_element_graphics` de `rvt-mcp` | Esquema disponible en el binario local; habilitado en el perfil ARQ-IA, sin prueba viva de color. |
| Guardar `.py` + `.dyn` + manifiesto | MCP propio de rutinas | Creación/listado y hashes comprobados fuera de Revit. |
| Ejecutar rutina fija PythonNet3 de lectura | MCP runner + bridge Revit/Dynamo | Pasó en RVT descartable el 05-10-2026. |
| Ejecutar una rutina genérica | Runner restringido a `revit/test-models/` | Pendiente: el cambio de grafo agotó tiempo en la última prueba viva. |

## Reglas de ingeniería

1. Preferir herramienta Revit tipada para una operación cubierta; usar Dynamo cuando el flujo de nodos o un script pequeño aporten una capacidad que falta.
2. No inventar nombres de niveles, IDs, unidades ni anfitriones. `ElementId` solo vale en el documento actual; para referencias entre sesiones usar identidad de documento y `UniqueId` si corresponde.
3. “Pintar de rojo” debe distinguir [override de vista y material](COLOR_MUROS_PRIMER_PISO.md). “Primer piso” debe resolverse contra niveles/elementos reales.
4. Para cambios masivos, mostrar cantidad y alcance antes de ejecutar. Si una consulta está truncada, no afirmar que cubre todos los elementos.
5. PythonNet3 corre con los permisos de Revit; la restricción de RVT no aísla archivos/red. Revisar scripts completos, usar copia descartable y comprobar deltas tras ejecutar.
6. Reducir tokens con consultas acotadas, respuestas resumidas y reutilización de rutinas; no volver a generar el mismo grafo ni adjuntar el modelo entero sin motivo.

## Evolución

Validar la ruta gráfica en una copia descartable; cerrar la ejecución genérica al cambiar de grafo; comprobar una escritura acotada y reversible; después ampliar casos de documentación, geometría o MEP según necesidades reales. Comparar con [repositorios externos y herramientas oficiales](ARCHITECTURE_DECISION.md) antes de sumar otro bridge. No llamar “producto final” a una capacidad sin evidencia viva.
