# Capacidad objetivo del asistente BIM

**Fecha:** 24 de septiembre de 2026
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

Para las tareas habituales se prefieren herramientas Revit tipadas: crear niveles, ejes, muros, pisos, recintos, instancias de familias; editar parámetros/tipos; documentar vistas, cotas y etiquetas. Para un caso que no tenga una herramienta tipada, Codex puede preparar PythonNet3/Dynamo. El runner compilado solo admite una rutina de lectura; aún no se probó dentro de Revit.

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
- La instalación v0.6.2 expone un inventario local de **51 herramientas** al seleccionar los grupos `query,create,modify,view,annotation`. El perfil de proyecto prepara una lista curada de consultas, creación de objetos arquitectónicos y edición de parámetros/tipos; Codex solicita aprobación por llamada. No se habilitan borrado ni ejecución de código arbitrario. La integración de ese perfil desde Codex aún no se ha validado.
- El MCP de rutinas genera un `.py`, un `.dyn` de un nodo PythonNet3 y un manifiesto. El runner MCP presenta una sola rutina read-only con hashes fijos, pero el smoke vivo no encontró el add-in en Revit; su ejecución aún no está validada.
- Hay un borrador de escritura `set_comments_on_selection` que asigna `PRUEBA_IA` a Comments en los elementos seleccionados. La lógica pasa pruebas con dobles de Revit; no se ha ejecutado en un RVT y el runner actual no permite ejecutarla.
- La app Codex todavía debe abrir esta carpeta como proyecto confiable y confirmar que cargó los MCP. La consulta viva de Revit aún no se ha hecho desde esa configuración de Codex.

El inventario local es una lectura del esquema de herramientas, no una prueba de cada comando. Antes de aprobar una escritura, Codex debe explicar el objetivo y consultar los datos relevantes del proyecto. Cada herramienta aprobada puede aplicar el cambio inmediatamente; el MCP no proporciona una vista previa o rollback general. Las tareas compuestas se ejecutan en pasos pequeños sobre un modelo descartable y se verifican con consultas posteriores. Borrado, guardado, exportación y ejecución libre requieren controles separados.

## Diseño de permisos y ejecución

1. **Lectura:** consultar el documento, elementos, selección y catálogos sin modificarlo.
2. **Creación tipada:** enseñar plan y objetivos; ejecutar solo después de confirmar la llamada concreta en un modelo descartable.
3. **Edición:** identificar IDs, valores anteriores/nuevos, unidades y alcance de instancia/tipo; confirmar antes de aplicar.
4. **Borrado, archivos, guardado y exportación:** permiso por acción, previa y efecto claramente visibles.
5. **Rutina Dynamo:** almacenar código y huellas; revisar y probar en modelo descartable. El runner prototipo acepta solo el ID/hash fijado de una rutina read-only. Antes de habilitar escrituras debe añadirse una lista permitida explícita, aprobación, comprobación de documento/sesión y verificación posterior.
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
2. Instalar el add-in con Revit cerrado y ejecutar la única rutina Dynamo de lectura en una plantilla descartable.
3. En modelo desechable, probar una única creación tipada y una edición de parámetro: plan, aprobación de la llamada, operación y verificación.
4. Evaluar un lote de operaciones en una transacción y manejo de rollback antes de probar flujos compuestos.
5. Revisar y seleccionar qué capacidades externas se adoptan tras inspeccionar código, licencia, dependencias, releases y evidencia de pruebas.
6. Ampliar el runner por IDs y hashes aprobados, empezando con una escritura muy acotada en archivo descartable; mantener la ejecución arbitraria deshabilitada.
7. Añadir glosario bilingüe, manuales de tareas y evaluaciones con resultados esperados; ampliar arquitectura hacia estructura/MEP según los casos de uso reales.

## Interfaz recomendada

Para esta prueba, el arquitecto conversa con Codex en una ventana separada y el agente opera Revit mediante los MCP del proyecto. La única ejecución presentada por el runner es de solo lectura y todavía no ha pasado la prueba viva. Las rutinas nuevas se guardan para revisión y ejecución manual. Esta ruta permite medir los pedidos frecuentes antes de financiar y desplegar otra interfaz.

Si la adopción justifica un panel dentro del flujo de trabajo, la opción a evaluar primero es un panel acoplable en Revit. El panel puede enviar solicitudes al mismo agente/orquestador y conservar un solo puente autorizado para operar el modelo. Un paquete de extensión de Dynamo queda como opción para controles propios del editor de grafos. La interfaz no reemplaza el runner, el despacho de Revit, las aprobaciones ni la verificación posterior.

## Fuentes consultadas

- [Repositorio bimwright/rvt-mcp](https://github.com/bimwright/rvt-mcp)
- [Repositorio KenLP/RevitMCPServer](https://github.com/KenLP/RevitMCPServer)
- [Repositorio HorizunGroup/horizun-revit-mcp](https://github.com/HorizunGroup/horizun-revit-mcp)
- [Modelo de seguridad de Horizun](https://github.com/HorizunGroup/horizun-revit-mcp/blob/main/docs/security-model.md)
