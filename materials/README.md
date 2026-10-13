# Catálogo de materiales ARQ-IA

`base.csv` contiene **208 entradas de referencia**, ordenadas por nombre y con cinco campos: nombre, clase Revit, categoría, color RGB y transparencia. Este conteo describe únicamente el catálogo curado; no es un conteo del modelo abierto ni una biblioteca completa de Revit.

Una consulta distinta, documentada en [MATERIALES_REVIT.md](../docs/MATERIALES_REVIT.md), devolvió 300/300 materiales definidos en un modelo de muestra. Las cifras corresponden a alcances distintos y no se deben comparar como inventarios equivalentes. El catálogo compartido no se usa para afirmar que un material exista o esté aplicado en el RVT actual.

Se omitieron IDs, assets, nombre del modelo, usos y cantidades/takeoff. El catálogo sirve como vocabulario y para encontrar candidatos; no certifica especificaciones ni garantiza que un material exista en el RVT abierto. Antes de asignarlo, consulta el documento activo y valida sus propiedades y alcance.

Cada instalación puede agregar preferencias propias en `.arq-ia/materials/local.csv`, siguiendo `local-materials.template.csv`. Esos datos quedan locales y fuera de Git. ARQ-IA los agrega solo cuando el usuario define la convención o el resultado ya quedó verificado. La base compartida se actualiza mediante una revisión intencional del repositorio.
