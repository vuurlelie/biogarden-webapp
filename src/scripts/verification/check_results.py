"""Fail CI when a required suite did not actually execute successfully."""

import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def check_trx(directory, expected_assemblies):
    reports = list(Path(directory).rglob("*.trx"))
    if not reports:
        raise ValueError("No TRX reports found")
    assemblies = set()
    total = 0
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    for report in reports:
        root = ET.parse(report).getroot()
        summary = root.find("t:ResultSummary", ns)
        counters = root.find("t:ResultSummary/t:Counters", ns)
        results = root.findall("t:Results/t:UnitTestResult", ns)
        if summary is None or counters is None or summary.get("outcome") != "Completed":
            raise ValueError(f"{report}: missing or unsuccessful run summary")
        count = int(counters.get("total", "0"))
        if count <= 0 or len(results) != count:
            raise ValueError(f"{report}: empty/incomplete test results")
        if any(int(counters.get(key, "-1")) != count for key in ("executed", "passed")):
            raise ValueError(f"{report}: tests failed, were skipped, or did not execute")
        if any(result.get("outcome") != "Passed" for result in results):
            raise ValueError(f"{report}: non-passing result")
        definitions = {
            test.get("id"): test.find("t:TestMethod", ns)
            for test in root.findall("t:TestDefinitions/t:UnitTest", ns)
        }
        for result in results:
            method = definitions.get(result.get("testId"))
            if method is None or not method.get("codeBase"):
                raise ValueError(f"{report}: result has no assembly identity")
            assemblies.add(method.get("codeBase").replace("\\", "/").rsplit("/", 1)[-1])
        total += count
    if assemblies != set(expected_assemblies):
        raise ValueError(f"Expected assemblies {expected_assemblies}, got {sorted(assemblies)}")
    return f"Verified {total} passed tests in {len(reports)} report(s); no skipped tests."


def check_playwright(report):
    data = json.loads(Path(report).read_text(encoding="utf-8-sig"))
    stats = data["stats"]
    if data.get("errors") or stats["unexpected"] != 0 or stats["skipped"] != 0:
        raise ValueError("Playwright has errors, failures, or skipped tests")
    if stats["expected"] + stats["flaky"] <= 0:
        raise ValueError("Playwright executed no tests")
    # Existing policy allows two CI retries. Report flakiness explicitly.
    return f"Playwright: {stats['expected']} passed, {stats['flaky']} passed on retry, zero skipped."


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="kind", required=True)
    trx = sub.add_parser("trx")
    trx.add_argument("directory")
    trx.add_argument("assemblies", nargs="+")
    browser = sub.add_parser("playwright")
    browser.add_argument("report")
    args = parser.parse_args()
    try:
        print(check_trx(args.directory, args.assemblies) if args.kind == "trx"
              else check_playwright(args.report))
    except (ValueError, OSError, ET.ParseError, KeyError, TypeError) as error:
        parser.exit(1, f"Verification failed: {error}\n")


if __name__ == "__main__":
    main()
