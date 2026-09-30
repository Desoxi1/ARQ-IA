# Modelos locales de prueba

Guarda aquí únicamente archivos RVT desechables creados para el POC. El bridge
rechaza la ejecución de rutinas genéricas sobre otros directorios, modelos
workshared, proyectos sin guardar o documentos con cambios pendientes.

`prepare-test-model.ps1` copia el sample HVAC de Revit 2027 y, cuando se usa
ese sample predeterminado, sus cinco RVT vinculados al mismo directorio para
que Revit pueda resolver las rutas relativas. Verifica los SHA-256, no
sobrescribe archivos locales distintos y quita el atributo de solo lectura
únicamente a la copia anfitriona de prueba.

Los archivos `.rvt` y sus respaldos se excluyen de Git. No copies modelos de
proyectos productivos a esta carpeta.
