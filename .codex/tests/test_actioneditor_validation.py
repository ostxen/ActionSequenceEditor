"""Audited fixed-target verification, never arbitrary code execution."""

from pathlib import Path
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from workflow_lib.core import Context, WorkflowError
from workflow_lib.unity import Unity, READ_ONLY, MUTATIONS


class ActionEditorValidationTests(unittest.TestCase):
    def test_native_save_cancel_is_fixed_mutation_and_rejects_all_parameters(self):
        command = "actioneditor.verification cancel_save_dialog"
        self.assertNotIn(command, READ_ONLY)
        self.assertIn(command, MUTATIONS)
        unity = Unity(SimpleNamespace(context=Context()))
        unity.raw = Mock()
        for parameters in ({"pid": 1}, {"handle": 1}, {"force": True}):
            with self.assertRaises(WorkflowError):
                unity.preview(command, parameters)
            with self.assertRaises(WorkflowError):
                unity.apply({"kind": "unity", "command": command, "parameters": parameters}, "unused")
        unity.raw.assert_not_called()

    def test_fixed_operations_reject_target_or_code_overrides(self):
        for name in ("document_lifecycle_probe", "persistence_probe", "reference_probe", "sync_projects", "export_probe", "tutorial_probe"):
            command = "actioneditor.verification " + name
            self.assertNotIn(command, READ_ONLY)
            self.assertEqual(MUTATIONS[command], {"dry_run": True, "confirm": True, "poll": None})
            unity = Unity(SimpleNamespace(context=Context()))
            unity.ready = Mock()
            unity.call = Mock()
            for parameters in ({"path": "Assets/Other"}, {"code": "arbitrary"}, {"force": True}):
                with self.assertRaises(WorkflowError):
                    unity.preview(command, parameters)
                with self.assertRaises(WorkflowError):
                    unity.apply({"kind": "unity", "command": command, "parameters": parameters}, "unused")
            unity.call.assert_not_called()
            unity.ready.assert_not_called()

    def test_consumed_maintenance_commands_are_unavailable_before_editor_access(self):
        for name in ("arrange_source_layout", "arrange_playback_sources", "rename_timeline_layout", "arrange_composition", "arrange_view_resources", "arrange_tests", "arrange_capabilities", "arrange_view_layout", "retire_unused_timeline", "retire_experiment_sources", "session_probe", "workbench_probe",
                     "retire_consumed_commands", "retire_view_protocols", "arrange_feature_layout",
                     "retire_imgui_timeline_sources", "retire_editing_sources", "arrange_application_layout",
                     "arrange_domain_layout", "rename_toolkit_ui", "rf17_move", "rf18_move"):
            command = "actioneditor.verification " + name
            self.assertNotIn(command, READ_ONLY)
            self.assertNotIn(command, MUTATIONS)
            unity = Unity(SimpleNamespace(context=Context()))
            unity.ready = Mock()
            unity.discover = Mock()
            unity.call = Mock()
            for parameters in ({}, {"path": "Assets/Other"}):
                with self.assertRaises(WorkflowError):
                    unity.preview(command, parameters)
                with self.assertRaises(WorkflowError):
                    unity.apply({"kind": "unity", "command": command, "parameters": parameters}, "unused")
            unity.ready.assert_not_called()
            unity.discover.assert_not_called()
            unity.call.assert_not_called()

    def test_retired_command_group_is_unavailable_without_editor_access(self):
        for name in ("shared_json_probe", "persistence_probe", "rf18_reference_probe", "rf20_export_probe", "rf22_tutorial_probe", "controlled_quit", "document_lifecycle_probe", "sync_projects"):
            command = "actioneditor.validation " + name
            self.assertNotIn(command, MUTATIONS)
            unity = Unity(SimpleNamespace(context=Context()))
            unity.ready = Mock()
            unity.discover = Mock()
            with self.assertRaises(WorkflowError):
                unity.preview(command, {})
            unity.ready.assert_not_called()
            unity.discover.assert_not_called()

    def test_cancelled_repair_is_unavailable_without_contacting_editor(self):
        command = "actioneditor.verification repair_new_action_copy"
        self.assertNotIn(command, READ_ONLY)
        self.assertNotIn(command, MUTATIONS)
        unity = Unity(SimpleNamespace(context=Context()))
        unity.ready = Mock()
        unity.discover = Mock()
        unity.call = Mock()
        for parameters in ({}, {"path": "Assets/Other"}):
            with self.assertRaises(WorkflowError):
                unity.preview(command, parameters)
            with self.assertRaises(WorkflowError):
                unity.apply({"kind": "unity", "command": command, "parameters": parameters}, "unused")
        unity.ready.assert_not_called()
        unity.discover.assert_not_called()
        unity.call.assert_not_called()

    def test_controlled_quit_requires_mutation_and_rejects_all_parameters(self):
        command = "actioneditor.verification controlled_quit"
        self.assertNotIn(command, READ_ONLY)
        self.assertEqual(MUTATIONS[command], {"dry_run": True, "confirm": True, "poll": None})
        unity = Unity(SimpleNamespace(context=Context()))
        unity.ready = Mock()
        unity.call = Mock()
        for parameters in ({"save": True}, {"discard": True}, {"target": "other"}):
            with self.assertRaises(WorkflowError):
                unity.preview(command, parameters)
        unity.ready.assert_not_called()
        unity.call.assert_not_called()

    def test_json_status_is_fixed_read_only_query(self):
        self.assertIn("actioneditor.verification json_status", READ_ONLY)
        self.assertNotIn("actioneditor.verification json_status", MUTATIONS)

    def test_persistence_probe_is_fixed_mutation_not_read_only(self):
        command = "actioneditor.verification persistence_probe"
        self.assertNotIn(command, READ_ONLY)
        self.assertEqual(MUTATIONS[command], {"dry_run": True, "confirm": True, "poll": None})

    def test_window_status_is_fixed_read_only_query(self):
        self.assertIn("actioneditor.verification window_status", READ_ONLY)
        self.assertNotIn("actioneditor.verification window_status", MUTATIONS)

    def test_probe_is_mutating_and_not_a_query(self):
        command = "actioneditor.verification document_lifecycle_probe"
        self.assertNotIn(command, READ_ONLY)
        self.assertEqual(MUTATIONS[command], {"dry_run": True, "confirm": True, "poll": None})
        unity = Unity(SimpleNamespace(context=Context()))
        unity.ready = Mock()
        unity.call = Mock()
        with self.assertRaises(WorkflowError):
            unity.query(command, {})
        unity.call.assert_not_called()

    def test_apply_rechecks_preview_then_consumes_confirmation_once(self):
        command = "actioneditor.verification document_lifecycle_probe"
        unity = Unity(SimpleNamespace(context=Context()))
        unity.ready = Mock()
        unity.discover = Mock()
        unity.capabilities = {command}
        preview = {"root": "Assets/ActionEditorLifecycleVerification", "schema": 1}
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
        command = "actioneditor.verification document_lifecycle_probe"
        unity.capabilities = {command}
        unity.call = Mock(return_value={"schema": 2})
        plan = {"kind": "unity", "command": command, "parameters": {}, "preview": {"schema": 1}}
        with patch("workflow_lib.unity.consume_approval") as consume:
            with self.assertRaises(WorkflowError):
                unity.apply(plan, "approval.json")
            consume.assert_not_called()
        self.assertEqual(unity.call.call_count, 1)
