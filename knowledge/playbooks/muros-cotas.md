# Muros y cotas

**Entrada mínima:** documento, nivel, ubicación, tipo y alcance. **Salida:** muros o cotas con referencias y unidades comprobadas.

1. Consultar niveles, unidades, ejes, tipos de muro y familias existentes. Distinguir **tipo** de **instancia** y comprobar la composición/capas del tipo antes de usarlo. No crear un tipo nuevo si el estándar del modelo ya resuelve el pedido.
2. Para cada muro, definir línea de ubicación, trayectoria y sentido; restricción base/superior, offsets, altura, unión de extremos y aberturas/anfitriones. Convertir unidades con la API, sin tratar milímetros mostrados como unidades internas. Comprobar intersecciones y duplicados antes de crear.
3. Si el pedido es «pintar», separar override de vista, pintura de cara y material/capa del tipo. Un cambio de tipo puede afectar muchas instancias: leer usos y alcance primero.
4. Las cotas permanentes pertenecen a una **vista** y requieren referencias geométricas válidas, línea de cota y estilo; IDs de elementos por sí solos no bastan. Leer las referencias y comprobar que son estables y visibles en la vista objetivo.
5. Usar herramienta tipada solo si el catálogo conectado ofrece la operación exacta; de otro modo preparar una rutina revisable. Tras escribir, consultar IDs creados, parámetros, posición, vista y referencias. Registrar elementos omitidos y advertencias.

Fuentes: [muros en Revit 2027](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-ArchDesign/files/GUID-550F4B3E-7D49-4A4F-BEC7-4077D5B9FC85.htm), [restricciones y ubicación](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-ArchDesign/files/GUID-05BAFEAA-5186-484E-80F4-8D900C454748.htm), [cotas por vista](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-DocumentPresent/files/GUID-A7C5D9BD-BA91-4D94-9073-7C1C158A3910.htm), [API de cotas](https://help.autodesk.com/cloudhelp/2025/DEU/Revit-API/files/Revit_API_Developers_Guide/Revit_Geometric_Elements/Annotation_Elements/Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Annotation_Elements_Dimensions_and_Constraints_html.html).

Vídeos de apoyo: [Autodesk: crear/modificar muros](https://help.autodesk.com/cloudhelp/2027/ENU/RevitLT-ArchDes/files/GUID-33A5C214-C71F-46E9-BF96-A5AC77AF1EBF.htm) y [ejemplo comunitario en YouTube](https://www.youtube.com/watch?v=jHP1gC8o2YU). Las reglas de implementación provienen de la documentación Autodesk; el vídeo sirve solo para observar el flujo visual.
