# Estructura y reproducción de elementos

**Entrada mínima:** origen, destino, documento, niveles/ejes, coordenadas, categorías y propósito. **Salida:** propuesta o conjunto creado con dependencias y deltas identificados.

1. Identificar qué es modelo físico (columnas, vigas, losas, muros estructurales, fundaciones) y qué es modelo analítico. Consultar tipos, materiales, niveles, restricciones, anfitriones, uniones y vínculos. No deducir capacidad portante ni normativa a partir de una vista.
2. «Recrear en otro lado» exige origen y destino inequívocos, unidades y marco de coordenadas. Calcular transformación de traslación/giro y comprobar tres referencias geométricas. Distinguir copiar instancias de reconstruir familias/tipos, y elementos del modelo de anotaciones propias de una vista.
3. Hacer inventario acotado de dependencias: tipos, grupos, familias, niveles, ejes, anfitriones, huecos, parámetros y vínculos. Una copia puede generar elementos dependientes; comparar el conjunto devuelto con el solicitado y detectar duplicados/conflictos de tipos.
4. Antes de cambiar estructura, expresar impacto y autoridad de diseño requerida. Preparar una alternativa si faltan cargas, criterios o coordinación; no presentar una propuesta geométrica como cálculo estructural aprobado.
5. Ejecutar solo con herramienta compatible y alcance autorizado. Después verificar ubicación, orientación, niveles, conexiones, dependencias, parámetros, advertencias y cantidad; si el destino es otro documento, comprobar allí.

Fuentes: [elementos estructurales Revit 2027](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-StructEng/files/GUID-8DBD5510-BB44-48E8-A37F-6D370234E23A.htm), [copia y dependencias en Revit API](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Basic_Interaction_with_Revit_Elements/Editing_Elements/Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Editing_Elements_Copying_Elements_html.html), [coordenadas compartidas](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-Model/files/GUID-68611F67-ED48-4659-9C3B-59C5024CE5F2.htm).
