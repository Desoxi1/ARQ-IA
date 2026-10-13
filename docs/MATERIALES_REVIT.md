# Materiales en ARQ-IA

## Qué se consulta

ARQ-IA consulta **el documento Revit activo**. `revit_list_materials` devuelve los materiales definidos en ese RVT; `revit_get_material_properties` obtiene detalles de uno por ID o nombre. El instalador habilita estas dos herramientas de lectura en Codex. No habilita las herramientas MCP que crean, asignan o editan materiales.

Un material definido no demuestra que esté aplicado a un elemento. Las bibliotecas `.adsklib` instaladas en el PC son otra fuente distinta y no se importan automáticamente al proyecto. Tampoco existe un catálogo universal de materiales arquitectónicos válido para toda oficina o proyecto.

## Base de referencia y extensión local

`materials/base.csv` contiene 208 entradas curadas para búsqueda y lenguaje arquitectónico. Se conservaron solo nombre, clase, categoría, color y transparencia; se excluyeron IDs de Revit/asset, nombre del modelo, cantidades, recuentos de uso y takeoffs. Es una base común, no una norma de oficina ni una ficha técnica. Su cantidad no representa el inventario de un RVT. Verifica siempre que el material exista en el documento activo y confirma sus propiedades antes de asignarlo.

Cada PC puede guardar convenciones locales en `.arq-ia/materials/local.csv`, usando las columnas de `materials/local-materials.template.csv`. Añade solo una preferencia que el usuario haya definido o un resultado reutilizable ya verificado. No copies el inventario completo de un RVT a la base común; `.arq-ia/` está ignorada por Git, así cada equipo amplía su propia colección sin compartir el modelo.

Según Autodesk, un material puede tener datos de **identidad**, **gráficos** para vistas no renderizadas, **apariencia** de render, **propiedades físicas** y **propiedades térmicas**. Estas facetas no son intercambiables. La clase/categoría escrita en el material es metadata del modelo, no una especificación técnica verificada. [Materiales en Revit 2027](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-Customize/files/GUID-8E88F590-4864-4C60-9433-4A70AC066605.htm), [API de Material](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/2ec33007-7a2a-f86a-009b-d4c5d235a307.htm).

## Lectura comprobada en este PC

El 13-10-2026 se abrió **Snowdon Towers Sample Architectural.rvt**, copia local del ejemplo Autodesk, en Revit 2027. El conector local `rvt-mcp` v0.6.2 devolvió `total=300`, `returned=300`, `limit_hit=false`, `skipped=0` con `limit=1000`. Por tanto, el listado de **materiales definidos en ese modelo** quedó completo. Esta lectura de 300 es independiente del catálogo curado de 208 entradas de `materials/base.csv`; no se espera que las cantidades coincidan. La respuesta avisó que el resultado era grande (77.924 bytes); en trabajo normal conviene filtrar por nombre o clase y pedir propiedades solo de los candidatos.

Distribución por clase declarada en el ejemplo: Generic 71, Unassigned 60, Metal 49, Wood 24, Plastic 20, Miscellaneous 16, Masonry 11, Paint 9, Glass 8, Concrete 7, System 6, Stone 6, Ceramic 3 y otras clases minoritarias. Los 300 registran un asset de apariencia; 124 tienen asset estructural y 128 asset térmico. Se consultaron detalladamente seis candidatos de hormigón, vidrio, ladrillo, madera, yeso y aislación; el conector devolvió gráficos, patrones, identidad, assets y parámetros. El ejemplo muestra que un nombre comercial o genérico puede coexistir con assets físicos/térmicos de nombres distintos. **No tomar sus valores como ficha técnica del proyecto.**

Los resultados completos de esa consulta se conservan solo en almacenamiento local, fuera de Git y del ZIP. El repositorio mantiene únicamente la base común saneada de 208 entradas. El escaneo no activó `includeUseCount`; no hay evidencia de cuáles de los 300 se usan en elementos ni de sus cantidades. Se identificaron 14 archivos `.adsklib` Autodesk instalados, sin afirmar que sus contenidos estén cargados en el RVT. Cada PC generará su propio perfil al abrir su modelo; la base común se usa solo como vocabulario de referencia.

## Cómo resolver una petición de un arquitecto

1. Identificar **documento y alcance**: vista, selección, nivel, categoría, elementos y si se cambia una instancia, un tipo o un material compartido.
2. Para "pintar/colorear", distinguir: override gráfico **solo en una vista**; pintura de **caras**; asignación de **material** a parámetro/capa; edición de **apariencia o gráficos del material**. La pintura de caras no cambia la estructura del elemento. [API de pintura de caras](https://help.autodesk.com/cloudhelp/2018/ENU/Revit-API/Revit_API_Developers_Guide/Revit_Geometric_Elements/Material/Painting_the_Face_of_an_Element.html).
3. Buscar primero en el RVT activo con filtros. Validar `total`, `returned`, `limit_hit` y `skipped`; si hay truncamiento, acotar la consulta y no afirmar "todos".
4. Consultar las propiedades de los candidatos por ID; verificar nombre, clase, color, transparencia, patrón, asset, parámetros y posible uso. Para un cómputo, confirmar elementos/categorías, fase y unidades; `revit_get_material_quantities` no sustituye una especificación.
5. Antes de editar, explicar el efecto en lenguaje natural y confirmar el alcance. Reutilizar un material existente solo si sus propiedades cumplen el pedido. Verificar el resultado en Revit y, si procede, con una segunda consulta.

### Ejemplo

"Pon los muros del primer piso rojos" puede significar un override en una vista, pintar sus caras con un material o cambiar el material de una capa de sus tipos. ARQ-IA debe localizar los muros del nivel real y preguntar cuál efecto se desea si el contexto no lo resuelve. **No debe crear un material rojo ni modificar un tipo compartido por inferencia.** Véase [el caso de muros rojos](COLOR_MUROS_PRIMER_PISO.md).

## Repetir la lectura en otro PC

Instala ARQ-IA, abre Revit 2027 y el RVT que corresponda, y desde el panel solicita: "Lista los materiales del modelo activo, comprueba que el resultado sea completo y resume clases, gráficos y assets; guarda los nombres solo localmente. Luego examina en detalle los candidatos para esta tarea". El MCP es el puente de consulta; Codex traduce el pedido y no necesita copiar los 300 registros al chat. En un modelo con más resultados que el límite del conector, usa filtros por nombre/clase y reconoce explícitamente cualquier parte no cubierta.
