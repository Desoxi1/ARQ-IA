# Taxonomía del catálogo

`catalog_group` facilita filtrar y leer el catálogo. Es una clasificación editorial basada en el nombre visible; no se inspeccionaron los assets del RVT para asignar estos grupos. Los campos originales `revit_class` y `category` se conservan para trazabilidad. No se tradujeron ni corrigieron los nombres porque deben seguir permitiendo reconocer el material de origen.

## Lectura rápida

- Filtra `materials/base.csv` por `catalog_group` para explorar una familia.
- Usa `material_name` para buscar el nombre de origen; `revit_class` y `category` son etiquetas copiadas del origen y pueden ser genéricas, estar en inglés o vacías.
- `color_rgb` y `transparency_percent` describen valores visuales del registro; no son propiedades físicas ni prueban que el material esté aplicado.
- Para consultar un proyecto, usa el MCP sobre el RVT activo. Esta tabla solo ayuda a encontrar vocabulario y candidatos.

## Grupos y cantidad de registros

Las cantidades son filas del catálogo compartido, no cantidades de obra ni de elementos del modelo.

| Grupo (`catalog_group`) | Registros | Incluye | Precaución |
|---|---:|---|---|
| `Metales` | 14 | Aceros, aluminio, cobre y otros metales | Perfiles y piezas pueden estar descritos como componentes |
| `Hormigón y morteros` | 5 | Hormigón, morteros y sobrelosa | No implica resistencia ni clase estructural |
| `Maderas` | 5 | Maderas y tablones | No infiere especie o tratamiento si no aparece en el nombre |
| `Vidrios` | 5 | Vidrio, cristal y vidrio templado | No infiere espesor ni prestación |
| `Albañilería y áridos` | 7 | Ladrillo, piedra, ripio, tierra, gravilla y asfalto | Revisar terreno o capas compuestas |
| `Cerámicas y revestimientos` | 5 | Cerámica, porcelanato, fachaleta y trasdosados | No prueba formato, adherencia o uso real |
| `Aislación, impermeabilización y plásticos` | 12 | Aislantes, membranas, PVC, poliurea y polímeros | No prueba desempeño térmico, fuego o estanqueidad |
| `Yesos y placas` | 3 | Yeso y placas | Confirmar si describe material, capa o producto |
| `Adhesivos` | 3 | Pegamentos y adhesivos | “Estructural” no equivale a certificación |
| `Componentes y elementos constructivos` | 45 | Puertas, ventanas, perfiles, bandejas, ductos, accesorios y capas nombradas | Confirma qué representa cada registro en Revit |
| `Equipos y artefactos` | 11 | Ascensor, iluminación, medidores, extractor y equipos | No reemplaza familias BIM ni fichas técnicas |
| `Apariencia y colores` | 21 | Colores, materiales de render y poche | Referencia visual; no equivale a material físico |
| `Datos analíticos de Revit` | 35 | Superficies, espacios, zonas y cargas | No son materiales constructivos |
| `Valores predeterminados de Revit` | 22 | Nombres `Default`, “por defecto” y equivalentes | Separar de materiales de proyecto |
| `Fases y estados` | 11 | Fases en español o inglés | Metadatos/estados, no materiales |
| `Gases` | 1 | Aire | Grupo semántico por nombre |
| `Paisajismo y terreno` | 1 | Vegetación | Grupo semántico por nombre |
| `Revisar origen (no parece material)` | 2 | `Dynamo`, `DynamoError` | Se conservaron del origen; no reutilizar sin aclarar qué representan |

## Calidad de datos

- Total conservado: **208 filas**; el recuento agrupado también suma 208. No hay duplicados en `material_name`.
- Los nombres, colores, transparencias, clase/categoría Revit originales no se reinterpretaron; 12 categorías originales están vacías y las clases contienen mezclas de español, inglés y etiquetas genéricas.
- La nueva taxonomía clasifica las 208 filas y mantiene `Dynamo` y `DynamoError` marcados para revisión. La revisión de sintaxis CSV no valida los materiales ni sus propiedades técnicas.
- En caso de duda, conserva el registro original y consulta el documento activo. No deduzcas prestaciones, cumplimiento normativo o aplicación del material por la etiqueta.
