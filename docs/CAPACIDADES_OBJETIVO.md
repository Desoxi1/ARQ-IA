# Capacidades objetivo y límites de ARQ-IA

**13-10-2026.** Este documento separa lo diseñado de lo comprobado. La evidencia vigente está en [PROTOTYPE_STATUS.md](PROTOTYPE_STATUS.md).

## Resultado buscado

Un arquitecto describe en español habitual o técnico lo que necesita; ARQ-IA consulta el modelo activo, identifica categorías, niveles, vistas, tipos, parámetros y unidades reales, pregunta solo lo que cambia el resultado y después comprueba el resultado. El panel ofrece acceso controlado y acceso completo por sesión; completo evita confirmaciones repetidas de Codex cuando el usuario pide la acción. Las rutinas Dynamo/PythonNet3 quedan guardadas para reutilizarse.

El asistente no “absorbe” automáticamente todos los MCP ni aprende de cualquier conversación. Un MCP expone herramientas; Codex las descubre y decide cuáles usar. El conocimiento persistente proviene de este glosario, guías técnicas y rutinas verificadas en Git. Añadir otro MCP exige revisar compatibilidad, licencia, seguridad y autoridad de escritura.

## Disponible en el paquete

| Capacidad | Implementación | Evidencia |
|---|---|---|
| Chat y adjuntos en Revit y Dynamo | Paneles propios con Codex app-server; paquete Dynamo 0.5.1 | El cliente procesó texto e imagen en un smoke; hay registro de carga de una versión anterior del panel. La instalación limpia de 0.5.1 no está validada en otro PC. |
| Vista activa y selección | `rvt-mcp` de bimwright | Consulta de solo lectura pasada en Revit. |
| Herramientas tipadas Revit | 105 herramientas expuestas: 58 de lectura y 47 de escritura para creación/edición arquitectónica, estructura, MEP, parámetros, materiales, vistas, láminas y tablas | Catálogo estático contrastado con `rvt-mcp` v1.1.0; las escrituras no se han probado todas en vivo. No equivale a toda la API Revit. |
| Color de presentación por elemento | `revit_override_element_graphics` de `rvt-mcp` | Esquema disponible en el binario local; habilitado en el perfil ARQ-IA, sin prueba viva de color. |
| Guardar `.py` + `.dyn` + manifiesto | MCP propio de rutinas | Creación/listado y hashes comprobados fuera de Revit. |
| Dejar un borrador visible en Dynamo sobre el documento activo | `prepare_saved_routine_in_dynamo` + bridge | Bridge compilado, empaquetado y preparado para la próxima sesión Revit: solicita apertura en Manual, no llama al ejecutor y se detiene si el workspace tiene nodos/notas. Pendiente validar la apertura sin ejecución con Dynamo 4.0.2 instalado. |
| Ejecutar rutina fija PythonNet3 de lectura | MCP runner + bridge Revit/Dynamo | Pasó en RVT descartable el 05-10-2026. |
| Ejecutar una rutina genérica | Runner de prueba y operación separada para el RVT activo | La rutina fija de lectura pasó históricamente en un modelo descartable. La ejecución genérica y las escrituras PythonNet3 aún requieren verificación viva por flujo. |

## Reglas de ingeniería

1. Preferir herramienta Revit tipada para una operación cubierta; usar Dynamo cuando el flujo de nodos o un script pequeño aporten una capacidad que falta.
2. No inventar nombres de niveles, IDs, unidades ni anfitriones. `ElementId` solo vale en el documento actual; para referencias entre sesiones usar identidad de documento y `UniqueId` si corresponde.
3. “Pintar de rojo” debe distinguir [override de vista y material](COLOR_MUROS_PRIMER_PISO.md). “Primer piso” debe resolverse contra niveles/elementos reales.
4. Para cambios masivos, mostrar cantidad y alcance antes de ejecutar. Si una consulta está truncada, no afirmar que cubre todos los elementos.
5. PythonNet3 corre con los permisos de Revit; la restricción de RVT no aísla archivos/red. Revisar scripts completos, usar copia descartable y comprobar deltas tras ejecutar.
6. Reducir tokens con consultas acotadas, respuestas resumidas y reutilización de rutinas; no volver a generar el mismo grafo ni adjuntar el modelo entero sin motivo.

### Límites de cobertura

El perfil tipado actual permite crear y editar las familias de operaciones enumeradas en `config/rvt-mcp-tools.json`; no habilita borrado de elementos, C# arbitrario ni toda la API de Revit. Si falta una operación concreta —por ejemplo dividir un muro o mover geometría sin una herramienta tipada adecuada— ARQ-IA debe reconocer las herramientas Revit que ya respondieron y resolver el cambio con una rutina Dynamo revisada sobre el modelo abierto cuando el runner lo permita. La capacidad queda pendiente si el runner o sus precondiciones no están disponibles.

## Evolución

Validar la ruta gráfica en una copia descartable; cerrar la ejecución genérica al cambiar de grafo; comprobar una escritura acotada y reversible; después ampliar casos de documentación, geometría o MEP según necesidades reales. Comparar con [repositorios externos y herramientas oficiales](ARCHITECTURE_DECISION.md) antes de sumar otro bridge. No llamar “producto final” a una capacidad sin evidencia viva.
