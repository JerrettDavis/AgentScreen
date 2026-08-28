#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path
import os
import shutil

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "docs" / "screenshots"
PREVIEW = ROOT / "tools" / "design-preview" / "index.html"
APP_CSS = ROOT / "src" / "AgentDisplay.Web" / "wwwroot" / "css" / "app.css"

SPECS = [
    ("dashboard-desktop.png", 1440, 1000, "dashboard"),
    ("dashboard-mobile.png", 390, 844, "dashboard"),
    ("session-detail.png", 1280, 900, "session"),
    ("approval-gate.png", 1280, 900, "approval"),
    ("device-320x480.png", 320, 480, "device"),
]


def build_html(view: str) -> str:
    html = PREVIEW.read_text(encoding="utf-8")
    css = APP_CSS.read_text(encoding="utf-8")
    html = html.replace(
        '<link rel="stylesheet" href="/src/AgentDisplay.Web/wwwroot/css/app.css">',
        f"<style>\n{css}\n</style>",
    )
    html = html.replace(
        "const view = qs.get('view') || 'dashboard';",
        f"const view = {view!r};",
    )
    return html


def main() -> None:
    windows_chrome = Path(os.environ.get("PROGRAMFILES", "")) / "Google" / "Chrome" / "Application" / "chrome.exe"
    executable = (
        shutil.which("chromium")
        or shutil.which("chromium-browser")
        or shutil.which("google-chrome")
        or (str(windows_chrome) if windows_chrome.is_file() else None)
    )
    if not executable:
        raise RuntimeError("A Chromium browser is required to regenerate screenshots.")

    OUTPUT.mkdir(parents=True, exist_ok=True)
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            executable_path=executable,
            headless=True,
            args=["--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu"],
        )
        try:
            for name, width, height, view in SPECS:
                page = browser.new_page(
                    viewport={"width": width, "height": height},
                    device_scale_factor=1,
                )
                page.set_content(build_html(view), wait_until="load")
                page.wait_for_timeout(120)
                page.screenshot(path=str(OUTPUT / name), full_page=False)
                page.close()
                print(f"{name}: {width}x{height}")
        finally:
            browser.close()


if __name__ == "__main__":
    main()
