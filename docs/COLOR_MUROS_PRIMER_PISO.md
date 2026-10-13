# Caso: “pinta de rojo los muros del primer piso”

## Traducción de lenguaje natural a Revit

| Frase | Lectura técnica | Dato que hay que comprobar |
|---|---|---|
| “muros” | Instancias de la categoría `Walls` | Muros del documento activo; excluir otros tipos de elemento. |
| “primer piso” | Posible nivel base o vista de planta | Nombre/ID del nivel y propiedad de nivel de cada muro. No basta el nombre de la vista. |
| “rojo” | `#FF0000` | Si se busca un override de vista, pintura de caras, asignación de material o cambio de sus propiedades gráficas. |

**Aclaración mínima si el usuario no especificó el efecto:** “¿Quieres que esos muros se vean rojos solo en esta vista, pintar sus caras o cambiar el material de sus capas?” El asistente puede consultar Revit antes de preguntar. Si pide “en esta vista”, no se cambia ningún material ni tipo de muro. [Guía de materiales](MATERIALES_REVIT.md).

## Ruta técnica para el color en una vista

1. `revit_get_current_view_info`: confirmar documento y vista que permite sobrescrituras.
2. `revit_ai_element_filter` con categoría `Walls`: obtener candidatos. Si el resultado está limitado o truncado, ampliar o consultar por partes; nunca llamar “todos” a un subconjunto.
3. `revit_get_element_details` para los IDs candidatos: confirmar el nivel base de cada muro. Comparar IDs de nivel, no únicamente textos localizados.
4. Presentar vista, nivel, cantidad e IDs/ejemplos al arquitecto. Con su aprobación, llamar `revit_override_element_graphics` con `elementIds` exactos, `viewId` de la vista objetivo, `surfaceForegroundColor="#FF0000"`, y `cutLineColor="#FF0000"` si corresponde a una planta con muros cortados. La herramienta instalada busca un patrón de relleno sólido para la superficie.
5. Leer el resultado `succeeded` y `failed`; comprobar visualmente y, cuando exista una consulta suficiente, contrastar los overrides de la vista. Si falla parcialmente, informar los IDs fallidos. No repetir la escritura a ciegas.

Una vista 3D o alzado puede requerir `projectionLineColor` y `surfaceForegroundColor`; una planta que corta muros puede requerir además color de corte. El color es **específico de esa vista**. Para que aparezca en varias vistas, planificar cada vista o un filtro de vista con reglas revisadas. Para color de material, consultar primero el material y su alcance: modificar un material compartido puede afectar elementos que el usuario no pidió.

Esta ruta usa herramientas tipadas de `rvt-mcp`; no crea un script Dynamo. En la versión local v0.6.2 se confirmó por inventario MCP que existe `revit_override_element_graphics` con `elementIds`, `viewId`, `surfaceForegroundColor` y `cutLineColor`. El código fuente del manejador contiene una transacción y busca un patrón sólido. **No hay todavía una ejecución viva de esta solicitud desde el panel ARQ-IA.**

## Criterio de aceptación en un RVT descartable

- La vista y el nivel son los solicitados; los IDs objetivo pertenecen a `Walls` y al nivel confirmado.
- La herramienta devuelve éxito para el conjunto completo, o informa exactamente los fallos parciales.
- Los muros se ven rojos en la vista elegida; un elemento fuera del alcance y otra vista no cambian.
- El material/tipo de muro queda intacto cuando se pidió solo presentación.

Fuentes: [manejador `rvt-mcp` v0.6.2](https://github.com/bimwright/rvt-mcp/blob/v0.6.2/src/shared/Handlers/OverrideElementGraphicsHandler.cs), [Autodesk `View.SetElementOverrides`](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/a6f1ced3-1f1c-dd42-c0ca-f15f301d1cad.htm).
