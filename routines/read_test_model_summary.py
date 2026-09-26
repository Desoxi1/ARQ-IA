import clr
clr.AddReference("RevitServices")
clr.AddReference("RevitAPI")
from RevitServices.Persistence import DocumentManager
from Autodesk.Revit.DB import FilteredElementCollector

uiapp = DocumentManager.Instance.CurrentUIApplication
uidoc = uiapp.ActiveUIDocument if uiapp is not None else None
if uidoc is None or uidoc.Document is None:
    OUT = {"status": "no_active_document", "affected_ids": []}
else:
    doc = uidoc.Document
    elements = FilteredElementCollector(doc).WhereElementIsNotElementType()
    OUT = {
        "status": "ok",
        "model_title": doc.Title,
        "active_view": uidoc.ActiveView.Name,
        "selection_count": len(list(uidoc.Selection.GetElementIds())),
        "element_count": elements.GetElementCount(),
        "affected_ids": [],
    }
