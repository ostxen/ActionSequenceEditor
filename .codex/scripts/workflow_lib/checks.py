"""Project-local structure checks and explicit verification profiles."""

import importlib.util
import os
from pathlib import Path
import re
import shutil
import sys
import xml.etree.ElementTree as ET
from urllib.parse import unquote, urlsplit

from .core import WorkflowError, blocked, project_lock, require_success, read_json
from .unity import Unity

SKILLS = ("unity-cli",)


def structure(context):
    missing = [name for name in ("yaml", "markdown_it") if importlib.util.find_spec(name) is None]
    if missing:
        blocked("Missing document parsers: " + ", ".join(missing) +
                ". Install requirements.txt only with explicit authorization.")
    import yaml
    from markdown_it import MarkdownIt

    root = context.project / ".codex"
    discovered = sorted(path.parent.name for path in (root / "skills").glob("*/SKILL.md"))
    errors = []
    if not set(SKILLS).issubset(discovered):
        errors.append("Required Unity CLI skill is missing.")
    for name in SKILLS:
        directory = root / "skills" / name
        try:
            text = (directory / "SKILL.md").read_text(encoding="utf-8-sig")
            sections = text.split("---", 2)
            metadata = yaml.safe_load(sections[1]) if len(sections) == 3 and not sections[0].strip() else None
            if not isinstance(metadata, dict) or metadata.get("name") != name or not metadata.get("description"):
                errors.append(f"{name}: invalid frontmatter")
            ui = yaml.safe_load((directory / "agents" / "openai.yaml").read_text(encoding="utf-8"))
            interface = ui.get("interface", {})
            if "$" + name not in interface.get("default_prompt", ""):
                errors.append(f"{name}: default_prompt must name the skill")
            if not 25 <= len(interface.get("short_description", "")) <= 64:
                errors.append(f"{name}: short_description must be 25-64 characters")
        except (ValueError, OSError, yaml.YAMLError, AttributeError) as exc:
            errors.append(f"{name}: {exc}")
    docs = [context.project / "AGENTS.md", context.project / "Docs" / "AI-Development-Workflow.md",
            root / "templates" / "task.md", *sorted((root / "skills" / "unity-cli").rglob("*.md"))]
    parser = MarkdownIt()
    for path in docs:
        if not path.is_file():
            errors.append(f"Missing document: {path}")
            continue
        text = path.read_text(encoding="utf-8-sig")
        for token in parser.parse(text):
            for child in token.children or []:
                if child.type not in ("link_open", "image"):
                    continue
                target = child.attrGet("href" if child.type == "link_open" else "src") or ""
                parts = urlsplit(target)
                if parts.scheme or target.startswith("#") or not parts.path:
                    continue
                if not (path.parent / unquote(parts.path)).resolve().exists():
                    errors.append(f"Broken reference: {path} -> {target}")
    if errors:
        raise WorkflowError("\n".join(errors))
    return {"skills": discovered, "documents": len(docs)}


def solution_inputs(context):
    projects = re.findall(r'Project\("[^"]+"\)\s*=\s*"[^"]+",\s*"([^"]+\.csproj)"',
                          context.solution.read_text(encoding="utf-8-sig"))
    if not projects:
        blocked("Solution contains no C# projects.")
    sources = set()
    for name in projects:
        path = context.solution.parent / name.replace("\\", "/")
        if not path.is_file():
            blocked(f"Generated project missing: {name}")
        for item in ET.parse(path).getroot().iter():
            if item.tag.rsplit("}", 1)[-1] == "Compile" and item.get("Include"):
                sources.add((path.parent / item.get("Include").replace("\\", "/")).resolve())
    missing = [str(path.relative_to(context.project)) for path in (context.project / "Assets").rglob("*.cs")
               if path.resolve() not in sources]
    if missing:
        blocked("Sources absent from generated solution; regenerate in Unity or explicitly validate excluded "
                "platform sources: " + ", ".join(missing[:30]))
    return {"projects": len(projects), "sources": len(sources)}


def unit_tests(run):
    result = run.execute([sys.executable, "-m", "unittest", "discover", "-s",
                          run.context.project / ".codex" / "tests", "-p", "test_*.py", "-v"], timeout=180)
    require_success(result, "Workflow unit tests")
    output = result.stdout + result.stderr
    match = re.search(r"Ran (\d+) tests?", output)
    if not match or int(match[1]) == 0 or re.search(r"OK \(skipped=", output):
        raise WorkflowError("Unit suite ran no tests or skipped coverage.")
    return {"tests": int(match[1]), "runner": "unittest"}


def build(run):
    if not shutil.which("dotnet"):
        blocked(".NET SDK is missing.")
    if not run.context.solution.is_file():
        blocked(f"Generated solution missing: {run.context.solution}. Regenerate in Unity, do not synthesize it.")
    inputs = solution_inputs(run.context)
    result = run.execute(["dotnet", "build", run.context.solution, "--nologo", "-v:q", "-clp:ErrorsOnly"], timeout=300)
    require_success(result, "Full solution build")
    return {"solution": str(run.context.solution), "inputs": inputs, "output": result.stdout}


def doctor(run):
    context = run.context

    def tools():
        package = read_json(context.project / "Packages" / "com.unity.pipeline" / "package.json")
        version = (context.project / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8")
        if not context.cli.is_file():
            blocked("Project-local Unity CLI missing.")
        result = run.execute([context.cli, "--version"], timeout=15,
                             env=dict(os.environ, UNITY_NO_UPDATE_CHECK="1"))
        require_success(result, "CLI version")
        return {"python": sys.version, "dotnet": shutil.which("dotnet"), "git": shutil.which("git"),
                "unity": version.strip(), "pipeline": package["version"], "cli": result.stdout.strip(),
                "solution": str(context.solution), "solutionExists": context.solution.is_file(),
                "optionalDependencies": {name: importlib.util.find_spec(name) is not None
                                         for name in ("yaml", "markdown_it")}}

    run.step("environment", tools)
    with project_lock(context):
        run.step("editor-connection", Unity(run).ready)


def validate_assets(unity, assets):
    # Importer queries are not full serialized-reference or business validation.
    blocked("No full asset validator is registered for these targets. Use audited read-only "
            "queries and explicit project tests. --asset cannot claim validation: " + ", ".join(assets))


def verify(run, profile, query, filter_type, timeout, assets, modes=None):
    if profile in ("docs", "code", "full"):
        run.step("skill-and-document-structure", lambda: structure(run.context))
        run.step("workflow-unit-tests", lambda: unit_tests(run))
    if profile in ("code", "full"):
        run.step("solution-build", lambda: build(run))
    if profile in ("unity", "full"):
        if not query or not query.strip() or not modes:
            blocked("Unity verification requires explicit test filter and modes.")
        modes = tuple(dict.fromkeys(modes))
        with project_lock(run.context):
            unity = Unity(run)
            connection = run.step("editor-capabilities", unity.discover)
            if connection["status"] != "passed":
                for name in ("editor-compilation", *(mode + "-tests" for mode in modes)):
                    run.skip(name, "Requires the project Editor connection.", required=True)
                if assets:
                    run.skip("asset-validation", "Requires Editor connection.", required=True)
                return
            compilation = run.step("editor-compilation", lambda: unity.recompile(timeout))
            if compilation["status"] != "passed":
                for mode in modes:
                    run.skip(mode + "-tests", "Editor compilation did not pass.", required=True)
                if assets:
                    run.skip("asset-validation", "Editor compilation did not pass.", required=True)
                return
            if assets:
                run.step("asset-validation", lambda: validate_assets(unity, assets))
            else:
                run.skip("asset-validation", "No asset targets supplied; no claim about uninspected assets.")
            for mode in modes:
                run.step(mode + "-tests", lambda mode=mode: unity.tests(mode, query, filter_type, timeout))
