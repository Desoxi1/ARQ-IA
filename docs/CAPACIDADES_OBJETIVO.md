# Capacidad objetivo del asistente BIM

**Actualizado:** 1 de octubre de 2026
**Estado:** arquitectura y ruta de desarrollo; todavía no es la capacidad instalada completa.

## Objetivo

Que un arquitecto describa una tarea en español cotidiano o lenguaje BIM/Revit, reciba preguntas cuando falten datos, vea un plan de cambios en términos comprensibles, apruebe su ejecución y obtenga evidencia de lo que Revit creó o modificó. Las rutinas ya validadas deben quedar disponibles para volver a ejecutarlas.

El sistema no “absorbe” código de todos los MCP ni aprende automáticamente cada respuesta. Cada MCP aporta herramientas y datos; Codex decide cuáles usar. El conocimiento persistente se construye con glosarios, documentación consultable, estándares de oficina y rutinas verificadas, guardados y versionados.

## Flujo previsto

```text
Petición del arquitecto
        ↓
Interpretar intención, términos y restricciones
        ↓
Consultar Revit: vista, selección, niveles, tipos y parámetros disponibles
        ↓
Resolver ambigüedades y preparar un plan de cambios
        ↓
Mostrar objetivos, unidades, cambios y riesgos esperados
        ↓
Confirmación explícita del usuario
        ↓
Ejecutar herramientas Revit tipadas en transacciones
        ↓
Volver a consultar el modelo y comparar con el plan
        ↓
Informar resultados y guardar el procedimiento reutilizable
```

Para tareas habituales se prefieren herramientas Revit tipadas: crear niveles, ejes, muros, pisos, recintos, instancias de familias; editar parámetros/tipos; documentar vistas, cotas y etiquetas. Para un caso que no tenga una herramienta tipada, Codex puede guardar una rutina PythonNet3/Dynamo y solicitar su ejecución. El runner genérico está compilado e instalado por usuario. En la última comprobación (1-10-2026), Revit estaba en Inicio sin modelo y respondía por el bridge anterior; el nuevo bridge, staged después del inicio de esa sesión, exige Dynamo UI abierta y ligada al RVT activo. Aún falta ejecutar la rutina en vivo con esa condición.

## Conocimiento técnico reutilizable

El paquete debe crecer con:

- Glosario bidireccional español ↔ inglés Revit API/Dynamo: nombre común, término técnico, categoría/clase, sinónimos, unidades y ejemplo.
- Catálogo del modelo activo: niveles, tipos de muro/piso, familias, parámetros, vistas y plantillas disponibles. El asistente debe consultar el proyecto antes de inventar nombres o IDs.
- Guías por tarea: intención, datos necesarios, precondiciones, herramienta compatible, efectos y comprobación posterior.
- Biblioteca de rutinas aprobadas con versión de Revit/Dynamo, código, hash, alcance y ejemplos de entrada/salida.
- Casos de evaluación con solicitudes coloquiales y técnicas equivalentes, incluidas solicitudes ambiguas que deben producir una pregunta antes de ejecutar.

Una rutina entra a la biblioteca común después de revisión y prueba en un modelo descartable. No se convierte automáticamente en estándar por el solo hecho de haberse generado.

## Capacidad disponible hoy

- La consulta directa Revit 2027 ↔ rvt-mcp pasó en solo lectura: se obtuvo la vista activa y la selección de un muro. Los IDs y nombres del modelo se omitieron de la versión pública.
- La instalación v0.6.2 expone un inventario local de **51 herramientas** al seleccionar los grupos `query,create,modify,view,annotation`. El perfil de proyecto prepara una lista curada de consultas, creación de objetos arquitectónicos y edición de parámetros/tipos; Codex solicita aprobación por llamada. No se habilitan borrado ni ejecución de código arbitrario. `codex mcp list` muestra habilitados los tres MCP del proyecto y una prueba app-server llamó al MCP de rutinas; la consulta desde el panel hacia Revit todavía no está validada.
- El MCP de rutinas genera el fuente `.py`, un `.dyn` de un nodo PythonNet3 Manual con wrapper de resultado y un manifiesto con hashes. El runner genérico vuelve a validar ambos y se limita a RVT de `revit/test-models/`.
- El borrador `set_comments_on_selection` escribe `PRUEBA_IA` en Comments de los elementos seleccionados. El propio script también comprueba que el documento sea un RVT local guardado bajo el directorio desechable. Aún no se ha ejecutado en Revit.
- La consulta real de rvt-mcp a Revit pasó por separado desde la CLI. Codex app-server desde esta carpeta listó rutinas mediante el MCP; aún falta probar desde la ventana Asistente BIM una consulta al modelo Revit y la rutina PythonNet3.

El inventario local es una lectura del esquema de herramientas, no una prueba de cada comando. Antes de aprobar una escritura, Codex debe explicar el objetivo y consultar los datos relevantes del proyecto. Cada herramienta aprobada puede aplicar el cambio inmediatamente; el MCP no proporciona una vista previa o rollback general. Las tareas compuestas se ejecutan en pasos pequeños sobre un modelo descartable y se verifican con consultas posteriores. Borrado, guardado, exportación y ejecución libre requieren controles separados.

## Diseño de permisos y ejecución

1. **Lectura:** consultar el documento, elementos, selección y catálogos sin modificarlo.
2. **Creación tipada:** enseñar plan y objetivos; ejecutar solo después de confirmar la llamada concreta en un modelo descartable.
3. **Edición:** identificar IDs, valores anteriores/nuevos, unidades y alcance de instancia/tipo; confirmar antes de aplicar.
4. **Borrado, archivos, guardado y exportación:** permiso por acción, previa y efecto claramente visibles.
5. **Rutina Dynamo:** guardar fuente, wrapper, manifiesto y hashes; revisar cada rutina antes de correrla. El bridge limita el modelo y exige aprobación explícita de posible escritura en toda ejecución genérica, porque la etiqueta del manifiesto no prueba que el código sea de solo lectura. PythonNet3 sigue ejecutándose con permisos del proceso Revit; la validación de ruta no sandboxea archivos ni red.
6. **Errores:** cancelar o revertir el grupo transaccional cuando sea posible; reportar explícitamente resultados parciales cuando Revit no permita revertirlos.

La configuración de Codex debe exponer herramientas por perfil y mantener `default_tools_approval_mode = "prompt"`. No se habilita el escape `revit_send_code_to_revit` como herramienta cotidiana. Un `dry-run` documentado debe probarse en el binario exacto antes de depender de él; la lista de herramientas por sí sola no demuestra que una acción sea reversible.

## MCP de GitHub revisados para la lista corta

| Proyecto | Información publicada por el repositorio | Uso considerado |
|---|---|---|
| [bimwright/rvt-mcp](https://github.com/bimwright/rvt-mcp) | Ya instalado y probado para consultas en este PC. El inventario local de v0.6.2 incluye herramientas tipadas de creación, edición, vistas y anotación al pedir esos grupos. | Mantener como base de este POC mientras se prueba una lista explícita de herramientas de escritura. |
| [KenLP/RevitMCPServer](https://github.com/KenLP/RevitMCPServer) | El README anuncia Revit 2025–2027, herramientas tipadas, transacciones, `dryRun`, diferencias estructuradas, riesgo por herramienta y licencia MIT. | Comparar sus patrones de vista previa/transacción. La página consultada no mostraba un instalador release listo, así que requeriría auditar y compilar su fuente. |
| [HorizunGroup/horizun-revit-mcp](https://github.com/HorizunGroup/horizun-revit-mcp) | El README anuncia Revit 2023–2027, 98 herramientas/262 suboperaciones y licencia Apache-2.0; su documento de seguridad describe perfiles de solo lectura/escritura y una ruta Python privilegiada apagada por defecto. Los releases públicos son unsigned según su propia política. | Candidato de mayor cobertura para evaluar arquitectura, familias, MEP y documentación. Auditar fuente, dependencias, instalador y perfiles antes de instalarlo. |

No se instalarán varias implementaciones de puente Revit simultáneamente hasta revisar conflictos y elegir una autoridad de escritura. Se pueden combinar servidores MCP especializados como herramientas separadas, manteniendo un único puente responsable de las transacciones del modelo.

## Ruta propuesta

1. Abrir esta carpeta como proyecto confiable en Codex y validar las consultas de vista/selección; confirmar qué herramientas del perfil se cargaron.
2. Iniciar Revit con el RVT desechable y probar `read_test_model_summary` mediante el runner genérico; verificar que el documento no cambie.
3. En modelo desechable, probar una edición acotada de Comments con aprobación y lectura posterior del parámetro. Luego probar una acción tipada y comparar ambas rutas.
4. Evaluar un lote de operaciones en una transacción y manejo de rollback antes de probar flujos compuestos.
5. Revisar y seleccionar qué capacidades externas se adoptan tras inspeccionar código, licencia, dependencias, releases y evidencia de pruebas.
6. Ampliar las evaluaciones de tareas y el manejo de fallos parciales; ejecutar rutinas PythonNet3 solo con revisión de código y dentro del RVT descartable.
7. Añadir glosario bilingüe, manuales de tareas y evaluaciones con resultados esperados; ampliar arquitectura hacia estructura/MEP según los casos de uso reales.

## Interfaz recomendada

El prototipo ya incluye el panel **Asistente BIM** dentro de Dynamo 4.0.2. El panel conversa con Codex app-server local y sus MCP del proyecto; no es una página web de ChatGPT incrustada ni usa una API key. Codex recibe las reglas del `AGENTS.md`, consulta este glosario y las capacidades instaladas, elige entre herramientas Revit tipadas y rutinas Dynamo/PythonNet3, y muestra el progreso en el panel. Las ejecuciones que puedan modificar el modelo siguen sujetas a aprobación explícita.

El smoke app-server → MCP y la generación de rutinas ya pasaron. Falta validar el panel en la instalación actualizada y cerrar el ciclo con Dynamo UI abierto sobre un RVT descartable: petición en el chat, consulta o cambio, lectura posterior y rutina reutilizable. Un panel nativo de Revit puede evaluarse después como una segunda superficie; no reemplaza el panel Dynamo que forma parte del objetivo actual.

## Fuentes consultadas

- [Repositorio bimwright/rvt-mcp](https://github.com/bimwright/rvt-mcp)
- [Repositorio KenLP/RevitMCPServer](https://github.com/KenLP/RevitMCPServer)
- [Repositorio HorizunGroup/horizun-revit-mcp](https://github.com/HorizunGroup/horizun-revit-mcp)
- [Modelo de seguridad de Horizun](https://github.com/HorizunGroup/horizun-revit-mcp/blob/main/docs/security-model.md)
