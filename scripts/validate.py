#!/usr/bin/env python3
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import py_compile
import xml.etree.ElementTree as ET

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "validation-report.json"


def result(name: str, status: str, detail: str, **extra: object) -> dict[str, object]:
    return {"name": name, "status": status, "detail": detail, **extra}


def run(command: list[str], timeout: int = 900) -> tuple[int, str]:
    completed = subprocess.run(command, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout)
    return completed.returncode, completed.stdout[-8000:]


def check_required() -> dict[str, object]:
    required = [
        "AgentDisplay.slnx", "README.md", "LICENSE", "SECURITY.md", "global.json",
        "src/AgentDisplay.Host/Program.cs", "src/AgentDisplay.Web/Pages/Dashboard.razor",
        "src/AgentDisplay.Web/Pages/Pair.razor", "src/AgentDisplay.Web/wwwroot/icons/icon-192.png",
        "src/AgentDisplay.Web/wwwroot/icons/icon-512.png",
        "integrations/hooks/install.mjs", "integrations/hooks/relay.mjs",
        "firmware/e32r40t/platformio.ini", ".github/workflows/ci.yml",
        ".github/dependabot.yml", ".github/CODEOWNERS",
        ".github/ISSUE_TEMPLATE/bug_report.yml", ".github/workflows/codeql.yml",
    ]
    missing = [path for path in required if not (ROOT / path).is_file()]
    return result("required-files", "fail" if missing else "pass", "missing: " + ", ".join(missing) if missing else f"{len(required)} required paths present")


def check_structured_files() -> dict[str, object]:
    errors: list[str] = []
    json_files = list(ROOT.rglob("*.json")) + list(ROOT.rglob("*.webmanifest"))
    for path in json_files:
        if any(part in {"bin", "obj", ".pio", ".git"} for part in path.parts):
            continue
        try:
            json.loads(path.read_text(encoding="utf-8"))
        except Exception as exc:
            errors.append(f"{path.relative_to(ROOT)}: {exc}")
    xml_files = [ROOT / "Directory.Build.props", ROOT / "AgentDisplay.slnx", *ROOT.rglob("*.csproj")]
    for path in xml_files:
        try:
            ET.parse(path)
        except Exception as exc:
            errors.append(f"{path.relative_to(ROOT)}: {exc}")
    return result("structured-files", "fail" if errors else "pass", "; ".join(errors) if errors else f"parsed {len(json_files)} JSON and {len(xml_files)} XML files")


def check_python() -> dict[str, object]:
    scripts = sorted((ROOT / "scripts").glob("*.py"))
    errors: list[str] = []
    for script in scripts:
        try:
            py_compile.compile(str(script), doraise=True)
        except py_compile.PyCompileError as exc:
            errors.append(f"{script.relative_to(ROOT)}: {exc.msg}")
    return result(
        "python-syntax",
        "fail" if errors else "pass",
        "; ".join(errors) if errors else f"compiled {len(scripts)} Python scripts",
    )


def repository_digest() -> str:
    ignored_names = {"validation-report.json", "manifest.sha256"}
    ignored_parts = {".git", "bin", "obj", ".pio", "node_modules", "artifacts", "TestResults"}
    digest = hashlib.sha256()
    for path in sorted(ROOT.rglob("*"), key=lambda candidate: candidate.as_posix()):
        if not path.is_file() or path.name in ignored_names or any(part in ignored_parts for part in path.relative_to(ROOT).parts):
            continue
        relative = path.relative_to(ROOT).as_posix().encode("utf-8")
        digest.update(len(relative).to_bytes(4, "big"))
        digest.update(relative)
        contents = path.read_bytes()
        digest.update(len(contents).to_bytes(8, "big"))
        digest.update(contents)
    return digest.hexdigest()


def check_javascript() -> list[dict[str, object]]:
    if not shutil.which("node"):
        return [result("javascript-syntax", "skip", "Node.js is not installed"), result("javascript-tests", "skip", "Node.js is not installed")]
    scripts = [
        "integrations/hooks/install.mjs",
        "integrations/hooks/relay.mjs",
        "src/AgentDisplay.Web/wwwroot/js/agentdisplay.js",
        "src/AgentDisplay.Web/wwwroot/service-worker.js",
        "src/AgentDisplay.Web/wwwroot/service-worker.published.js",
        "scripts/capture-screenshots.mjs",
    ]
    errors = []
    for script in scripts:
        code, output = run(["node", "--check", script], timeout=60)
        if code: errors.append(f"{script}: {output.strip()}")
    syntax = result("javascript-syntax", "fail" if errors else "pass", "\n".join(errors) if errors else f"checked {len(scripts)} scripts")
    test_files = [str(path.relative_to(ROOT)) for path in sorted((ROOT / "tests" / "js").glob("*.test.mjs"))]
    code, output = run(["node", "--test", *test_files], timeout=120)
    tests = result("javascript-tests", "pass" if code == 0 else "fail", output.strip())
    return [syntax, tests]


def check_native() -> dict[str, object]:
    if not shutil.which("g++"):
        return result("firmware-model-native", "skip", "g++ is not installed")
    code, output = run(["bash", "scripts/test-firmware-model.sh"], timeout=120)
    return result("firmware-model-native", "pass" if code == 0 else "fail", output.strip())


def check_screenshots() -> dict[str, object]:
    expected = {
        "dashboard-desktop.png": (1440, 1000),
        "dashboard-mobile.png": (390, 844),
        "session-detail.png": (1280, 900),
        "approval-gate.png": (1280, 900),
        "device-320x480.png": (320, 480),
    }
    errors = []
    for name, dimensions in expected.items():
        path = ROOT / "docs" / "screenshots" / name
        if not path.exists():
            errors.append(f"missing {name}")
            continue
        with Image.open(path) as image:
            if image.size != dimensions: errors.append(f"{name}: {image.size}, expected {dimensions}")
    return result("screenshots", "fail" if errors else "pass", "; ".join(errors) if errors else "five deterministic captures have expected dimensions")


def check_icons() -> dict[str, object]:
    errors = []
    for size in (192, 512):
        path = ROOT / "src" / "AgentDisplay.Web" / "wwwroot" / "icons" / f"icon-{size}.png"
        if not path.exists():
            errors.append(f"missing {path.name}")
            continue
        with Image.open(path) as image:
            if image.size != (size, size): errors.append(f"{path.name}: {image.size}")
    return result("pwa-icons", "fail" if errors else "pass", "; ".join(errors) if errors else "192px and 512px icons validated")


def check_board() -> dict[str, object]:
    text = (ROOT / "firmware" / "e32r40t" / "platformio.ini").read_text(encoding="utf-8")
    required = [
        "ST7796_DRIVER=1", "TFT_WIDTH=320", "TFT_HEIGHT=480", "TFT_MISO=12",
        "TFT_MOSI=13", "TFT_SCLK=14", "TFT_CS=15", "TFT_DC=2", "TFT_BL=27", "TOUCH_CS=33",
    ]
    missing = [item for item in required if item not in text]
    board = (ROOT / "firmware" / "e32r40t" / "include" / "BoardConfig.h").read_text(encoding="utf-8")
    if "TouchIrq = 36" not in board: missing.append("TouchIrq = 36")
    return result("e32r40t-pin-map", "fail" if missing else "pass", "missing: " + ", ".join(missing) if missing else "ST7796S/XPT2046 dimensions and pins are pinned")


def check_secrets() -> dict[str, object]:
    patterns = [
        re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
        re.compile(r"\bAKIA[0-9A-Z]{16}\b"),
        re.compile(r"\b(?:sk-ant-|sk-proj-|ghp_|github_pat_)[A-Za-z0-9._-]{24,}\b"),
    ]
    findings: list[str] = []
    ignore_names = {"validation-report.json", "manifest.sha256"}
    allowed_token_files = {
        Path("src/AgentDisplay.Core/Redactor.cs"), Path("integrations/hooks/relay.mjs"),
        Path("tests/AgentDisplay.Core.Tests/CoreTests.cs"), Path("tests/js/relay.test.mjs"),
        Path("scripts/validate.py"),
    }
    for path in ROOT.rglob("*"):
        if not path.is_file() or path.name in ignore_names or any(part in {".git", "bin", "obj", ".pio"} for part in path.parts):
            continue
        if path.suffix.lower() in {".png", ".jpg", ".jpeg", ".zip", ".bin", ".elf"}:
            continue
        try: text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError: continue
        rel = path.relative_to(ROOT)
        for index, pattern in enumerate(patterns):
            if index == 2 and rel in allowed_token_files: continue
            if pattern.search(text): findings.append(str(rel))
    return result("secret-scan", "fail" if findings else "pass", "possible secrets: " + ", ".join(sorted(set(findings))) if findings else "no private keys or credential-shaped values found")


def check_dotnet(skip: bool) -> dict[str, object]:
    if skip: return result("dotnet-tests", "skip", "explicitly skipped")
    if not shutil.which("dotnet"): return result("dotnet-tests", "skip", "dotnet SDK is not installed in this environment")
    code, output = run(["dotnet", "test", "AgentDisplay.slnx", "--configuration", "Release", "--nologo"], timeout=1200)
    return result("dotnet-tests", "pass" if code == 0 else "fail", output.strip())


def check_pio(skip: bool) -> dict[str, object]:
    if skip: return result("platformio", "skip", "explicitly skipped")
    if not shutil.which("pio"): return result("platformio", "skip", "PlatformIO is not installed in this environment")
    code1, out1 = run(["pio", "test", "-d", "firmware/e32r40t", "-e", "native"], timeout=1200)
    code2, out2 = run(["pio", "run", "-d", "firmware/e32r40t", "-e", "e32r40t"], timeout=1800)
    ok = code1 == 0 and code2 == 0
    return result("platformio", "pass" if ok else "fail", (out1 + "\n" + out2).strip())


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--skip-dotnet", action="store_true")
    parser.add_argument("--skip-pio", action="store_true")
    args = parser.parse_args()

    checks: list[dict[str, object]] = [check_required(), check_structured_files(), check_python()]
    checks.extend(check_javascript())
    checks.extend([
        check_native(), check_screenshots(), check_icons(), check_board(), check_secrets(),
        check_dotnet(args.skip_dotnet), check_pio(args.skip_pio),
    ])
    failed = [item for item in checks if item["status"] == "fail"]
    skipped = [item for item in checks if item["status"] == "skip"]
    status = "fail" if failed else "pass_with_skips" if skipped else "pass"
    report = {
        "product": "AgentScreen",
        "version": "0.1.0-alpha.1",
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "status": status,
        "checks": checks,
        "repositorySha256": repository_digest(),
    }
    REPORT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
