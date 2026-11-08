"""Audited fixed-target A01 probe, never arbitrary code execution."""

from pathlib import Path
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from workflow_lib.core import Context, WorkflowError
from workflow_lib.unity import Unity, READ_ONLY, MUTATIONS


class ActionEditorValidationTests(unittest.TestCase):
    def test_probe_is_mutating_and_not_a_query(self):
        command = "actioneditor.validation persistence_probe"
        self.assertNotIn(command, READ_ONLY)
        self.assertEqual(MUTATIONS[command], {"dry_run": True, "confirm": True, "poll": None})
        unity = Unity(SimpleNamespace(context=Context()))
        unity.ready = Mock()
        unity.call = Mock()
        with self.assertRaises(WorkflowError):
            unity.query(command, {})
        unity.call.assert_not_called()

    def test_apply_rechecks_preview_then_consumes_confirmation_once(self):
        command = "actioneditor.validation persistence_probe"
        unity = Unity(SimpleNamespace(context=Context()))
        unity.ready = Mock()
        unity.discover = Mock()
        unity.capabilities = {command}
        preview = {"root": "Assets/ActionEditorValidationA01", "schema": 1}
        unity.call = Mock(side_effect=[preview, {"status": "passed"}])
        plan = {"kind": "unity", "command": command, "parameters": {}, "preview": preview}
        with patch("workflow_lib.unity.consume_approval") as consume:
            self.assertEqual(unity.apply(plan, "approval.json"), {"status": "passed"})
            consume.assert_called_once()
        self.assertEqual(unity.call.call_args_list[0].args[1], {"dry-run": True})
        self.assertEqual(unity.call.call_args_list[1].args[1], {"confirm": True, "dry-run": False})

    def test_changed_preview_does_not_consume_or_apply(self):
        unity = Unity(SimpleNamespace(context=Context()))
        unity.ready = Mock()
        unity.discover = Mock()
        command = "actioneditor.validation persistence_probe"
        unity.capabilities = {command}
        unity.call = Mock(return_value={"schema": 2})
        plan = {"kind": "unity", "command": command, "parameters": {}, "preview": {"schema": 1}}
        with patch("workflow_lib.unity.consume_approval") as consume:
            with self.assertRaises(WorkflowError):
                unity.apply(plan, "approval.json")
            consume.assert_not_called()
        self.assertEqual(unity.call.call_count, 1)
