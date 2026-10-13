# Catálogo de referencia de ARQ-IA

`base.csv` es la referencia compartida inicial. Tiene 208 filas con nombre, clase y categoría Revit, color y transparencia. No contiene IDs de elemento/material, assets, cantidades ni nombre del modelo de origen.

La base ayuda a reconocer nombres y encontrar candidatos; no certifica especificaciones ni implica que un material exista en el RVT activo. Antes de asignarlo, consulta el documento abierto y valida sus propiedades y alcance.

Cada instalación puede agregar preferencias propias en `.arq-ia/materials/local.csv`, siguiendo `local-materials.template.csv`. Esos datos quedan locales y fuera de Git. ARQ-IA los agrega solo cuando el usuario define la convención o el resultado ya quedó verificado. La base compartida se actualiza mediante una revisión intencional del repositorio.
