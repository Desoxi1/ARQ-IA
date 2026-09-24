# Casos de lenguaje BIM/Revit

Estos casos evalúan que el asistente traduzca pedidos coloquiales a conceptos Revit, consulte el modelo y pregunte antes de una acción ambigua. No son comandos para ejecutar en un modelo productivo.

| Pedido cotidiano | Forma técnica equivalente | Respuesta esperada antes de escribir |
|---|---|---|
| “Haz una planta para el segundo piso.” | Crear un `Level` y/o una vista `ViewPlan`. | Preguntar si se necesita crear un nivel, una vista de planta asociada a un nivel existente, o ambos; consultar niveles y vistas. |
| “Sube estos muros a tres metros.” | Cambiar `Unconnected Height` o `Top Constraint/Top Offset`. | Consultar IDs, tipos, niveles, restricción superior y unidades. Preguntar si son 3 m sobre el nivel base o elevación absoluta; distinguir instancia de tipo. |
| “Alarga el tabique hasta el eje B.” | Cambiar geometría/curva de ubicación de un `Wall`. | Consultar muro y eje objetivo; resolver si se extiende el extremo cercano, ambos extremos o se mueve el muro completo. Informar coordenadas/unidades. |
| “Pon puertas de 90 por 210 en estos recintos.” | Colocar instancias `FamilyInstance` hospedadas en muros. | Consultar familias/tipos disponibles, muros anfitriones, niveles y puntos. Preguntar por ubicación y orientación si no quedan determinadas; no inferir anfitrión desde el recinto. |
| “Cambia el tipo a todos estos muros.” | Aplicar `WallType` a instancias seleccionadas. | Confirmar los `ElementId` y el tipo destino exacto del modelo. Explicar que el cambio afecta las instancias seleccionadas; un parámetro de tipo puede tener un alcance más amplio. |
| “Marca estos muros como RF-60.” | Posible parámetro de instancia/tipo, comentario, clasificación o etiqueta. | Consultar parámetros disponibles y preguntar cuál campo debe recibir `RF-60`; no escribirlo automáticamente en Comentarios. |
| “Pon cotas entre los ejes.” | Crear `Dimension` con referencias geométricas en una vista compatible. | Consultar vista y ejes, comprobar que existen referencias válidas y confirmar orientación/ubicación de la línea de cota. |
| “Haz lo mismo en todos los pisos.” | Repetir una acción por cada `Level` o vista de planta. | Consultar la lista exacta de niveles, excluir los no solicitados y presentar el número de llamadas/elementos antes de ampliar alcance. |

## Aprobación de un caso

Para dar un caso por aprobado, registrar petición, interpretación técnica, consultas usadas, herramienta elegida, preguntas aclaratorias, valores/unidades, resultado de Revit y comprobación posterior. Si el modelo o versión no está disponible, registrar el caso como pendiente; no convertir la respuesta supuesta en regla.
