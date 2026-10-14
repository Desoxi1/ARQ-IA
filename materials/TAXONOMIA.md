# Taxonomía del catálogo

`catalog_group` facilita filtrar y leer el catálogo; es una clasificación editorial basada en el nombre visible, no en inspección de assets del RVT. Los campos originales `revit_class` y `category` se conservan para trazabilidad. No se tradujeron ni corrigieron los nombres porque deben seguir permitiendo reconocer el material de origen.

| Grupo | Incluye | Precaución |
|---|---|---|
| Metales | Aceros, aluminio, cobre y otros metales nombrados como materiales | Perfiles y piezas quedan como componentes |
| Hormigón y morteros | Hormigón, morteros y sobrelosa | No implica resistencia ni clase estructural |
| Maderas | Maderas y tablones | No infiere especie o tratamiento cuando no está en el nombre |
| Vidrios | Vidrio, cristal y vidrio templado | No infiere espesor/prestación a partir del nombre |
| Albañilería y áridos | Ladrillo, piedra, ripio, tierra, gravilla y asfalto | Revisar si una fila describe terreno o una capa compuesta |
| Cerámicas y revestimientos | Cerámica, porcelanato, fachaleta y trasdosados | No prueba formato, adherencia o uso real |
| Aislación, impermeabilización y plásticos | Aislantes, membranas, PVC, poliurea y polímeros | No prueba desempeño térmico, fuego o estanqueidad |
| Yesos y placas | Yeso, placas y paneles de yeso | Confirmar si se trata de material, capa o producto |
| Adhesivos | Pegamentos y adhesivos | “Estructural” en el nombre no es certificación |
| Componentes y elementos constructivos | Puertas, ventanas, perfiles, bandejas, ductos, accesorios y capas nombradas | Las filas son registros de material con nombres de componentes; confirmar qué representa cada una en Revit |
| Equipos y artefactos | Ascensor, iluminación, medidores, extractor y equipos identificables | El listado no reemplaza una familia BIM ni ficha técnica |
| Apariencia y colores | Colores RAL/RGB, materiales de render y poche | Solo referencia visual; no equivale a material físico |
| Datos analíticos de Revit | Superficies, espacios, zonas y cargas de análisis | No son una lista de materiales constructivos |
| Valores predeterminados de Revit | Nombres `Default`, “por defecto” y equivalentes | Mantener separados de materiales de proyecto |
| Fases y estados | Fases nombradas en español o inglés | Metadatos/estados, no materiales |
| Gases / Paisajismo y terreno | Aire y vegetación | Agrupación semántica por nombre; validar el uso en contexto |
| Revisar origen | `Dynamo`, `DynamoError` | Se conservaron porque existen en la fuente, pero su nombre no describe un material; confirmar antes de reutilizar |

## Calidad de datos

- Total conservado: **208 filas**, sin duplicados en `material_name`.
- Los nombres, colores, transparencias, clase/categoría Revit originales no se reinterpretaron; 12 categorías originales están vacías y las clases contienen mezclas de español, inglés y etiquetas genéricas.
- La nueva taxonomía clasifica las 208 filas y mantiene `Dynamo` y `DynamoError` marcados para revisión. La revisión de sintaxis CSV no valida los materiales ni sus propiedades técnicas.
- En caso de duda, conserva el registro original y consulta el documento activo. No deduzcas prestaciones, cumplimiento normativo o aplicación del material por la etiqueta.
