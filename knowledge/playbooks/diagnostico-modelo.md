# Detectar y resolver problemas del modelo

**Entrada:** síntoma y documento/vista afectada. **Salida:** hallazgos reproducibles, causa probable, reparación acotada y verificación.

1. Consultar documento, fase, vista, categoría y elementos afectados. Obtener advertencias vigentes y filtros/estado de vínculos si la herramienta conectada lo permite. Separar error real, elemento oculto, problema de vista y fallo del puente MCP.
2. Reducir el problema a IDs/categorías y una condición observable: duplicado, desconexión, anfitrión perdido, unión incorrecta, interferencia, restricción incompatible, material ausente o parámetro vacío. No afirmar «sin problemas» a partir de una consulta parcial o truncada.
3. Contrastar con geometría, parámetros, niveles y coordinación de disciplinas. Para interferencias, comprobar si el contacto es intencional y quién es dueño de cada elemento. Ordenar por impacto, frecuencia y posibilidad de reversión.
4. Proponer una reparación mínima con elementos y valores concretos; conservar un estado recuperable si cambia geometría. Aplicar solo el alcance autorizado y volver a leer advertencias, parámetros y vista; si el fallo persiste, registrar la excepción.
5. Aprender localmente solo la regla o corrección que se confirmó en este proyecto, con fuente/fecha; no generalizar un workaround de un modelo a todos.

Fuentes: [advertencias del documento en Revit API](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/4774613d-600a-e1b5-b5aa-f1ee3b14394c.htm), [coordinación de modelos Revit 2027](https://help.autodesk.com/cloudhelp/2027/ENU/Revit-Collaborate/files/GUID-6324F0AF-48A7-4669-B1F7-D0B37440A29C.htm).
