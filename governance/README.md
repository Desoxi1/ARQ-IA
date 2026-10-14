# Reglas y restricciones

`assistant-policy.md` es la fuente completa de comportamiento BIM, límites de herramientas, alcance de escritura, protección del modelo y memoria local. La raíz conserva un `AGENTS.md` corto para que Codex encuentre estas reglas al abrir el proyecto.

La restricción que menciona **borrado** prohíbe habilitar herramientas que borren elementos dentro del modelo Revit. No impide ordenar archivos del repositorio con cambios revisables y recuperables. Las rutinas PythonNet3 tienen acceso del usuario a Windows y no son un sandbox.
