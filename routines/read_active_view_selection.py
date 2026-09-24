import clr
clr.AddReference("RevitServices")
from RevitServices.Persistence import DocumentManager

uiapp = DocumentManager.Instance.CurrentUIApplication
uidoc = uiapp.ActiveUIDocument if uiapp is not None else None
if uidoc is None:
    OUT = {
        "status": "no_active_document",
        "view_name": None,
        "view_type": None,
        "selection_count": 0,
        "selected_elements": [],
    }
else:
    doc = uidoc.Document
    view = uidoc.ActiveView
    elements = []
    for element_id in uidoc.Selection.GetElementIds():
        element = doc.GetElement(element_id)
        if element is not None:
            category = element.Category.Name if element.Category is not None else None
            elements.append({"id": str(element.Id.Value), "category": category})
    OUT = {
        "status": "ok",
        "view_name": view.Name,
        "view_type": str(view.ViewType),
        "selection_count": len(elements),
        "selected_elements": elements,
    }
