import clr
import json
import os
clr.AddReference("RevitServices")
clr.AddReference("RevitAPI")
from RevitServices.Persistence import DocumentManager
from RevitServices.Transactions import TransactionManager
from Autodesk.Revit.DB import BuiltInParameter, StorageType

TARGET_VALUE = "PRUEBA_IA"


def is_disposable_test_document(doc):
    try:
        app_data = os.environ.get("APPDATA", "")
        settings_path = os.path.join(app_data, "BIM-AI-POC", "bridge.json")
        with open(settings_path, "r", encoding="utf-8") as settings_file:
            test_models_root = os.path.realpath(json.load(settings_file)["testModelsRoot"])
        model_path = os.path.realpath(doc.PathName)
        root = os.path.normcase(test_models_root)
        model = os.path.normcase(model_path)
        return (
            bool(model_path)
            and os.path.isfile(model_path)
            and os.path.splitext(model_path)[1].lower() == ".rvt"
            and os.path.commonpath([root, model]) == root
            and not doc.IsWorkshared
            and not doc.IsModified
            and not doc.IsModifiable
        )
    except (OSError, ValueError, KeyError, TypeError):
        return False


uiapp = DocumentManager.Instance.CurrentUIApplication
uidoc = uiapp.ActiveUIDocument if uiapp is not None else None

if uidoc is None or uidoc.Document is None:
    OUT = {"status": "no_active_document", "updated_ids": [], "unchanged_ids": [], "skipped": []}
else:
    doc = uidoc.Document
    if not is_disposable_test_document(doc):
        OUT = {"status": "test_model_required", "updated_ids": [], "unchanged_ids": [], "skipped": [], "affected_ids": []}
    elif doc.IsReadOnly:
        OUT = {"status": "document_read_only", "updated_ids": [], "unchanged_ids": [], "skipped": []}
    else:
        selected_ids = list(uidoc.Selection.GetElementIds())
        updated_ids = []
        unchanged_ids = []
        skipped = []
        writable = []

        for element_id in selected_ids:
            element = doc.GetElement(element_id)
            element_id_text = str(element_id.Value)
            if element is None:
                skipped.append({"id": element_id_text, "reason": "element_missing"})
                continue
            parameter = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
            if parameter is None:
                skipped.append({"id": element_id_text, "reason": "comments_unavailable"})
                continue
            if parameter.IsReadOnly or parameter.StorageType != StorageType.String:
                skipped.append({"id": element_id_text, "reason": "comments_not_writable_string"})
                continue
            previous_value = parameter.AsString()
            if previous_value == TARGET_VALUE:
                unchanged_ids.append(element_id_text)
            else:
                writable.append((element_id_text, parameter))

        if writable:
            TransactionManager.Instance.EnsureInTransaction(doc)
            try:
                for element_id_text, parameter in writable:
                    try:
                        if parameter.Set(TARGET_VALUE):
                            updated_ids.append(element_id_text)
                        else:
                            skipped.append({"id": element_id_text, "reason": "revit_rejected_value"})
                    except Exception:
                        skipped.append({"id": element_id_text, "reason": "parameter_set_failed"})
            finally:
                TransactionManager.Instance.TransactionTaskDone()

        OUT = {
            "status": "ok",
            "requested_value": TARGET_VALUE,
            "selected_count": len(selected_ids),
            "updated_ids": updated_ids,
            "unchanged_ids": unchanged_ids,
            "affected_ids": updated_ids,
            "skipped": skipped,
        }
