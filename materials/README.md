# Materiales ARQ-IA

## Archivos

- `base.csv`: vocabulario compartido de **208 entradas curadas**, conservando los nombres y campos de origen de Revit y añadiendo `catalog_group` para navegar por familias.
- `TAXONOMIA.md`: definición de grupos, límites y notas para revisar clasificaciones dudosas.
- `local-materials.template.csv`: esquema para preferencias que cada PC guarda en `.arq-ia/materials/local.csv`.

## Cómo recorrer el catálogo

1. Filtra `base.csv` por `catalog_group` para navegar por familia.
2. Busca por `material_name`; conserva `revit_class` y `category` como etiquetas de origen. Pueden ser genéricas, estar en inglés o vacías.
3. Usa color y transparencia solo como referencias visuales.
4. Antes de asignar, consulta materiales y propiedades en el RVT activo. La presencia en esta tabla no demuestra disponibilidad, uso ni equivalencia.

Los 208 registros se distribuyen en 18 grupos. [La taxonomía](TAXONOMIA.md) lista cada grupo y su cantidad para orientarse sin abrir un listado de materiales del modelo.

La base mezcla materiales de construcción, elementos nombrados como materiales, registros analíticos, colores, estados de fase y valores predeterminados. Por eso la nueva columna clasifica **el tipo de registro**, no afirma que cada fila sea material físico. Se conservaron sin normalizar nombres y valores originales; la categoría Revit y la clase Revit pueden venir vacías, en otro idioma o con valores genéricos.

La base es referencial y no es una especificación. No es un inventario completo de Revit ni demuestra que esos materiales estén disponibles o usados en el modelo activo. Consulta el RVT con `revit_list_materials` y revisa propiedades antes de proponer una asignación. Véase [Materiales en Revit](../docs/MATERIALES_REVIT.md).

## Preferencias por PC

Las convenciones propias se guardan localmente bajo `.arq-ia/materials/local.csv`. Registra solo una preferencia explícita del usuario o un uso previamente comprobado. No copies IDs, cantidades, assets privados, nombres de proyecto o fichas no autorizadas a `base.csv`.
