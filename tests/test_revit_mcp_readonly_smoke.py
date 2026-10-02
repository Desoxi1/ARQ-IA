"""Ensure live read-only smoke tests don't treat MCP error text as model data."""

from __future__ import annotations

import unittest

from run_revit_mcp_readonly_smoke import read_tool_status


class ReadOnlySmokeResponseTests(unittest.TestCase):
    def test_no_open_revit_document_is_not_a_successful_read(self):
        response = {
            "result": {
                "content": [{"type": "text", "text": "Error: No document is open."}]
            }
        }
        self.assertEqual(read_tool_status(response), "no_active_document")

    def test_empty_selection_payload_is_a_valid_read(self):
        response = {"result": {"content": [{"type": "text", "text": "[]"}]}}
        self.assertEqual(read_tool_status(response), "ok")

    def test_structured_failure_is_not_a_successful_read(self):
        response = {
            "result": {
                "content": [{"type": "text", "text": '{"status":"failed"}'}]
            }
        }
        self.assertEqual(read_tool_status(response), "tool_error")

    def test_mcp_error_flag_is_not_a_successful_read(self):
        response = {"result": {"isError": True, "content": []}}
        self.assertEqual(read_tool_status(response), "tool_error")


if __name__ == "__main__":
    unittest.main()
