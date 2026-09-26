# Revit

El POC apunta a Revit 2027 con RvtMcp v0.6.2 instalado en el perfil del usuario. El instalador upstream es self-contained y se obtiene desde su release oficial con verificación SHA-256.

El 24-09-2026, las consultas de vista activa y selección respondieron en una sesión interactiva; no se modificó el modelo productivo. La configuración local de Codex ofrece herramientas Revit tipadas con aprobación por llamada, el generador de rutinas y el runner Dynamo. El bridge genérico solo admite RVT locales de `revit/test-models/`; las escrituras requieren confirmación. El add-in quedó instalado por usuario para la próxima sesión, sin cargarlo en la sesión productiva que ya estaba abierta. Falta validar la ejecución viva desde Codex y Dynamo.
