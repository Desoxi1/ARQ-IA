# Catálogo de materiales ARQ-IA

`base.csv` contiene la lista común inicial de **208 materiales distintos**, ordenada por nombre y con cinco campos: nombre, clase Revit, categoría, color RGB y transparencia. La lista se contrastó con una consulta completa de Revit (`total=208`, `returned=208`, sin límite ni omisiones).

Se omitieron IDs, assets, nombre del modelo, usos y cantidades/takeoff. El catálogo sirve como vocabulario y para encontrar candidatos; no certifica especificaciones ni garantiza que un material exista en el RVT abierto. Antes de asignarlo, consulta el documento activo y valida sus propiedades y alcance.

Cada instalación puede agregar preferencias propias en `.arq-ia/materials/local.csv`, siguiendo `local-materials.template.csv`. Esos datos quedan locales y fuera de Git. ARQ-IA los agrega solo cuando el usuario define la convención o el resultado ya quedó verificado. La base compartida se actualiza mediante una revisión intencional del repositorio.
