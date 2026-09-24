import json
import os
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

# The Revit-hosted runner sets these for a single approved smoke execution.
# The side-channel contains no view name, element id, or parameter value.
run_id = os.environ.get("BIM_AI_RUN_ID")
result_dir = os.environ.get("BIM_AI_RESULT_DIR")
if run_id and result_dir and len(run_id) == 32 and all(c in "0123456789abcdef" for c in run_id):
    from pathlib import Path

    result_root = Path(result_dir).resolve()
    result_path = (result_root / (run_id + ".json")).resolve()
    if result_path.parent == result_root:
        summary = {
            "status": OUT.get("status"),
            "view_type": OUT.get("view_type"),
            "selection_count": OUT.get("selection_count", 0),
        }
        result_path.write_text(json.dumps(summary), encoding="utf-8")
