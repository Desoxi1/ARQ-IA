# Fichas técnicas, materiales y familias

**Entrada:** ficha/PDF/imagen del producto y objetivo del modelo. **Salida:** tabla de requisitos con fuente y propuesta de parámetros o familia, sin valores inventados.

1. Extraer fabricante, producto exacto, revisión/fecha, unidades, alcance del ensayo, propiedades declaradas y páginas de origen. Separar dato medido, rango, condición de ensayo, recomendación comercial e inferencia. Si hay discrepancias entre fichas, marcarla.
2. Consultar materiales, familias y tipos del RVT activo; comparar funciones: apariencia, identidad, parámetros físicos/térmicos y capas de un tipo. El catálogo común `materials/base.csv` ayuda con nombres, no prueba equivalencia técnica ni cumplimiento normativo.
3. Mapear cada dato a un parámetro Revit existente con unidad y tipo de almacenamiento. Para datos sin parámetro adecuado, proponer campo/familia compartida y alcance de aplicación; no escribir un número con unidad incorrecta ni usar una propiedad visual como estructural.
4. Para modelar, definir geometría, categoría, anfitrión, niveles/restricciones, conectores si aplican, representación 2D/3D y nivel de detalle pedido. En una familia, comprobar tipos y parámetros antes de insertar instancias.
5. Antes de aplicar, señalar datos ausentes y decisiones del proyectista. Después comparar valores y cantidades en el RVT y conservar trazabilidad a la página/revisión de la ficha. No declarar certificación o cumplimiento solo por transcribir un PDF.

Fuentes: [materiales y assets en Revit API](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API/files/Revit_API_Developers_Guide/Revit_Geometric_Elements/Material/Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Material_General_Material_Information_html.html), [editor de familias Revit 2027](https://help.autodesk.com/view/RVT/2027/ENU/?guid=GUID-253B2300-35C2-4024-AB70-43E576CEA49C).
