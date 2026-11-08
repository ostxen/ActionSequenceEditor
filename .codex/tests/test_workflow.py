"""No-Git project migration, authorization, profile and evidence contracts."""

import json
from pathlib import Path
import sys
import tempfile
import time
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
import workflow
from workflow_lib import checks
from workflow_lib.core import (Context, Run, WorkflowError, aggregate, approve, consume_approval,
                               make_plan, read_json, redact, validate_plan, workspace_fingerprint)
from workflow_lib.unity import validate_test_result


class WorkflowTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="workflow project ")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        for name in ("Assets", "Packages", "ProjectSettings", ".codex", "Tools", "Docs"):
            (self.root / name).mkdir()
        self.context = Context(self.root)

    def test_context_is_project_root_and_solution_tracks_name(self):
        preferred = self.root / (self.root.name + ".sln")
        preferred.touch()
        context = Context(self.root)
        self.assertEqual(context.repo, self.root)
        self.assertEqual(context.solution, preferred)
        self.assertEqual(context.cli, self.root / "Tools/UnityCLI/unity.exe")

    def test_single_solution_fallback_and_ambiguous_solution_not_guessed(self):
        single = self.root / "Renamed.sln"
        single.touch()
        self.assertEqual(Context(self.root).solution, single)
        (self.root / "Other.sln").touch()
        self.assertFalse(Context(self.root).solution.exists())

    def test_fingerprint_works_without_git_and_ignores_only_generated_state(self):
        source = self.root / "Assets" / "Example.cs"
        source.write_text("original", encoding="utf-8")
        before = workspace_fingerprint(self.context)
        for directory in ("Library/Pipeline", "Temp", ".codex/runs/example", ".codex/scripts/__pycache__"):
            path = self.root / directory
            path.mkdir(parents=True)
            (path / "generated").write_text("ignored", encoding="utf-8")
        self.assertEqual(before, workspace_fingerprint(self.context))
        source.write_text("changed", encoding="utf-8")
        self.assertNotEqual(before, workspace_fingerprint(self.context))
        before = workspace_fingerprint(self.context)
        nested = self.root / "Assets/runs"
        nested.mkdir()
        (nested / "business.asset").write_text("must not be ignored", encoding="utf-8")
        self.assertNotEqual(before, workspace_fingerprint(self.context))

    def test_preview_approval_and_consumption_are_one_attempt_without_git(self):
        run = Run(self.context, "preview-test")
        result = make_plan(run, "unity", "asset delete", {"asset": "Assets/Example.asset"}, high_risk=True)
        plan = read_json(result["plan"])
        with self.assertRaises(WorkflowError):
            approve(Run(self.context, "approve-test"), result["plan"], "tester", "explicit test approval")
        approval = approve(Run(self.context, "approve-test"), result["plan"], "tester", "explicit test approval", True)
        consume_approval(Run(self.context, "apply-test"), plan, approval["approval"])
        with self.assertRaises(WorkflowError):
            consume_approval(Run(self.context, "repeat-test"), plan, approval["approval"])
        second = approve(Run(self.context, "approve-twice"), result["plan"], "tester", "test confirmation", True)
        with self.assertRaises(WorkflowError):
            consume_approval(Run(self.context, "repeat-twice"), plan, second["approval"])

    def test_changed_expired_wrong_project_or_tampered_plan_is_rejected(self):
        run = Run(self.context, "preview-test")
        result = make_plan(run, "unity", "asset delete", {}, high_risk=True)
        plan = read_json(result["plan"])
        approval = approve(Run(self.context, "approve"), result["plan"], "tester", "test approval", True)
        changed = {**plan, "parameters": {"asset": "Assets/Other.asset"}}
        with self.assertRaises(WorkflowError):
            consume_approval(Run(self.context, "apply"), changed, approval["approval"])
        with self.assertRaises(WorkflowError):
            validate_plan(self.context, {**plan, "expiresAt": time.time() - 1})
        with self.assertRaises(WorkflowError):
            validate_plan(self.context, {**plan, "project": str(self.root / "Other")})
        (self.root / "Assets/New.cs").touch()
        with self.assertRaises(WorkflowError):
            validate_plan(self.context, plan)

    def test_deleted_inputs_and_workflow_changes_invalidate_plan(self):
        path = self.root / "Assets/Input.asset"
        path.touch()
        run = Run(self.context, "preview")
        plan = read_json(make_plan(run, "unity", "asset delete", {}, inputs=[path])["plan"])
        path.unlink()
        with self.assertRaises(WorkflowError):
            validate_plan(self.context, plan)

    def test_partial_skips_cannot_pass(self):
        with self.assertRaises(WorkflowError):
            validate_test_result({"status": "completed", "summary": dict(total=2, passed=1, failed=0, skipped=1, inconclusive=0),
                                  "results": [{"FullName": "A", "Status": "Passed"}, {"FullName": "B", "Status": "Skipped"}]}, {"A", "B"})

    def test_unity_verification_requires_explicit_selection_before_any_operation(self):
        for args in (("--profile", "unity"), ("--profile", "full", "--test-filter", "Example")):
            run = Run(self.context, "verify-test")
            with patch.object(checks, "verify") as verify:
                with self.assertRaises(WorkflowError) as failure:
                    workflow.dispatch(run, workflow.parser().parse_args(["verify", *args]))
                self.assertEqual(failure.exception.status, "blocked")
                verify.assert_not_called()

    def test_docs_profile_does_not_require_unity_or_domain_modules(self):
        run = Run(self.context, "docs-test")
        with patch.object(checks, "structure", return_value={}), patch.object(checks, "unit_tests", return_value={}), patch.object(checks, "Unity") as unity:
            checks.verify(run, "docs", None, "assembly", 1, [], None)
            unity.assert_not_called()
        self.assertEqual(run.finish(), 0)

    def test_optional_asset_skip_is_not_asset_pass_and_interruption_stays_blocked(self):
        run = Run(self.context, "evidence-test")
        run.step("verified", lambda: {"answer": 1})
        run.skip("asset-validation", "No targets; no claim.")
        from workflow_lib.core import render_report
        self.assertEqual(render_report(run.path), "blocked")
        self.assertEqual(run.finish(), 0)
        self.assertEqual(run.data["steps"][1]["status"], "skipped")
        self.assertEqual(aggregate([]), "blocked")
        self.assertEqual(aggregate([{"status": "skipped", "required": True}]), "blocked")

    def test_asset_validator_never_substitutes_a_query_for_real_validation(self):
        unity = Mock()
        with self.assertRaises(WorkflowError) as failure:
            checks.validate_assets(unity, ["Assets/Example.asset"])
        self.assertEqual(failure.exception.status, "blocked")
        unity.query.assert_not_called()

    def test_logs_redact_secret_fields_and_nested_json(self):
        value = redact({"password": "sensitive", "output": json.dumps({"access_token": "sensitive"})})
        self.assertNotIn("sensitive", json.dumps(value))


if __name__ == "__main__":
    unittest.main()
