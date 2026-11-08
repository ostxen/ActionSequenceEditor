#!/usr/bin/env python3
"""Project-local development/CI entrypoint. No automatic setup or publication."""

import argparse
from pathlib import Path
import sys

from workflow_lib import checks
from workflow_lib.core import (
    Context, Run, WorkflowError, approve, blocked, project_lock, read_json, render_report,
)
from workflow_lib.unity import Unity


def parser():
    root = argparse.ArgumentParser(description=__doc__)
    root.add_argument("--project", type=Path, help="Explicit Unity project root (default: this script's project).")
    commands = root.add_subparsers(dest="command", required=True)
    commands.add_parser("doctor", help="Read-only environment and project connection preflight.")
    commands.add_parser("check", help="Validate instructions, links, and deterministic unit tests.")
    verify = commands.add_parser("verify", help="Run the selected validation profile.")
    verify.add_argument("--profile", choices=("docs", "code", "unity", "full"), required=True)
    verify.add_argument("--test-filter", help="Explicit test selection, never all third-party tests.")
    verify.add_argument("--test-mode", action="append", choices=("editor", "playmode"), help="Explicit modes; repeat to run both.")
    verify.add_argument("--filter-type", choices=("assembly", "testName", "category"), default="assembly")
    verify.add_argument("--timeout", type=int, default=300)
    verify.add_argument("--asset", action="append", default=[], help="Explicit asset path to validate; repeat as needed.")
    approval = commands.add_parser("approve", help="Record a human confirmation after reviewing a preview.")
    approval.add_argument("--plan", required=True, type=Path)
    approval.add_argument("--by", required=True)
    approval.add_argument("--reason", required=True, help="Reference to the actual user/CI approval.")
    approval.add_argument("--high-risk", action="store_true")
    report = commands.add_parser("report", help="Render only already-recorded evidence.")
    report.add_argument("--run", required=True, type=Path)
    unity = commands.add_parser("unity", help="Audited project-bound Unity operations.")
    sub = unity.add_subparsers(dest="action", required=True)
    sub.add_parser("list")
    compile_command = sub.add_parser("compile", help="Recompile this Editor and verify the terminal result.")
    compile_command.add_argument("--timeout", type=int, default=300)
    for action in ("query", "preview"):
        command = sub.add_parser(action)
        command.add_argument("name")
        command.add_argument("--params-file", type=Path)
    apply = sub.add_parser("apply")
    apply.add_argument("--plan", type=Path, required=True)
    apply.add_argument("--approval", type=Path, required=True)
    runtime = sub.add_parser("runtime-status", help="Read a specifically located Development Player.")
    runtime.add_argument("--runtime-path", type=Path, required=True)
    return root


def dispatch(run, args):
    if args.command == "doctor":
        checks.doctor(run)
    elif args.command == "check":
        run.step("skill-and-document-structure", lambda: checks.structure(run.context))
        run.step("workflow-unit-tests", lambda: checks.unit_tests(run))
    elif args.command == "verify":
        if args.timeout < 1:
            raise WorkflowError("Timeout must be positive.")
        if args.profile in ("unity", "full") and (not args.test_filter or not args.test_filter.strip() or not args.test_mode):
            blocked("Unity verification requires --test-filter and at least one --test-mode; no implicit test suite.")
        checks.verify(run, args.profile, args.test_filter, args.filter_type, args.timeout, args.asset, args.test_mode)
    elif args.command == "approve":
        run.step("approval-record", lambda: approve(run, args.plan, args.by, args.reason, args.high_risk))
    elif args.command == "unity":
        unity = Unity(run)
        with project_lock(run.context):
            if args.action == "list":
                run.step("registered-commands", unity.discover)
            elif args.action == "compile":
                if args.timeout < 1:
                    raise WorkflowError("Timeout must be positive.")
                run.step("editor-compilation", lambda: unity.recompile(args.timeout))
            elif args.action == "runtime-status":
                run.step("runtime-status", lambda: runtime_status(unity, args.runtime_path))
            elif args.action == "apply":
                run.step("unity-mutation", lambda: unity.apply(read_json(args.plan), args.approval))
            else:
                params = read_json(args.params_file) if args.params_file else {}
                method = unity.query if args.action == "query" else unity.preview
                run.step("unity-" + args.action, lambda: method(args.name, params))


def runtime_status(unity, path):
    from workflow_lib.unity import decode_response
    path = path.resolve()
    value = decode_response(unity.raw(["command", "--runtime-path", str(path), "runtime", "status"]))
    if not isinstance(value, dict):
        blocked("Runtime status has no verifiable working directory.")
    actual = value.get("workingDirectory") or value.get("WorkingDirectory")
    if not actual or Path(actual).resolve() != path:
        blocked("Runtime working directory differs from the explicitly requested Player.")
    return value


def main(argv=None):
    args = parser().parse_args(argv)
    if sys.version_info < (3, 11):
        print("Python 3.11 or newer is required.", file=sys.stderr)
        return 2
    if args.command == "report":
        try:
            status = render_report(args.run)
            print(f"{status}: {args.run / 'report.md'}")
            return {"passed": 0, "failed": 1, "blocked": 2}[status]
        except (OSError, WorkflowError, KeyError) as exc:
            print(str(exc), file=sys.stderr)
            return 2
    try:
        context = Context(args.project)
    except WorkflowError as exc:
        print(str(exc), file=sys.stderr)
        return 2
    run = Run(context, " ".join(argv if argv is not None else sys.argv[1:]))
    try:
        dispatch(run, args)
    except WorkflowError as exc:
        run.data["steps"].append({"name": "dispatch", "status": exc.status, "message": str(exc), "required": True})
    except KeyboardInterrupt:
        run.data["steps"].append({"name": "interrupted", "status": "blocked", "message": "Inspect in-flight operation before retrying.", "required": True})
    except Exception as exc:
        run.data["steps"].append({"name": "unexpected-error", "status": "failed", "message": f"{type(exc).__name__}: {exc}", "required": True})
    code = run.finish()
    print(f"{run.data['status']}: {run.path / 'report.md'}")
    for step in run.data["steps"]:
        print(f"  {step['name']}: {step['status']}")
        if step.get("message"):
            print("    " + step["message"])
        elif step.get("result") and args.command not in ("verify", "check", "doctor"):
            print("    " + str(step["result"]))
    return code


if __name__ == "__main__":
    raise SystemExit(main())
