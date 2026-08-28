#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import os
import shutil

from playwright.sync_api import sync_playwright


def browser_executable() -> str | None:
    windows_chrome = Path(os.environ.get("PROGRAMFILES", "")) / "Google" / "Chrome" / "Application" / "chrome.exe"
    return (
        shutil.which("chromium")
        or shutil.which("chromium-browser")
        or shutil.which("google-chrome")
        or (str(windows_chrome) if windows_chrome.is_file() else None)
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="Smoke-test the published AgentScreen Devices UI.")
    parser.add_argument("--url", default="http://127.0.0.1:5277")
    args = parser.parse_args()
    executable = browser_executable()

    with sync_playwright() as playwright:
        launch_options = {"headless": True}
        if executable:
            launch_options["executable_path"] = executable
        browser = playwright.chromium.launch(**launch_options)
        try:
            for width, height in ((1280, 900), (390, 844)):
                page = browser.new_page(viewport={"width": width, "height": height})
                page.goto(f"{args.url}/devices", wait_until="networkidle")
                selector = page.locator(".interval-field select")
                selector.wait_for(state="visible")
                assert selector.locator("option").all_text_contents() == [
                    "30 seconds", "1 minute", "5 minutes", "15 minutes"
                ]
                selector.select_option("300")
                page.reload(wait_until="networkidle")
                assert page.locator(".interval-field select").input_value() == "300"
                assert page.locator(".sync-control-card").evaluate("el => el.scrollWidth <= el.clientWidth")
                page.close()
            print("Devices auto-sync interval smoke test passed at desktop and mobile widths.")
        finally:
            browser.close()


if __name__ == "__main__":
    main()
