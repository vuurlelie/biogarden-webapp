"""Combine Cobertura line hits and enforce reviewed critical-file coverage floors."""

import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def collect(directory):
    reports = list(Path(directory).rglob("coverage.cobertura.xml"))
    if not reports:
        raise ValueError("No Cobertura coverage reports found")
    files = {}
    for report in reports:
        root = ET.parse(report).getroot()
        for cls in root.findall("./packages/package/classes/class"):
            filename = cls.attrib["filename"].replace("\\", "/")
            # Coverlet paths may be absolute or relative on different runners.
            parts = filename.split("/")
            start = next((i for i, part in enumerate(parts)
                          if part in ("Biogarden.BusinessLogic", "Biogarden.DataAccess", "Biogarden.WebApi")), None)
            if start is None:
                continue
            filename = "/".join(parts[start:])
            lines = files.setdefault(filename, {})
            for line in cls.findall("./lines/line"):
                number = int(line.attrib["number"])
                hit = int(line.attrib["hits"]) > 0
                condition = line.get("condition-coverage")
                branch = float(condition.split("%", 1)[0]) if condition else None
                previous_hit, previous_branch = lines.get(number, (False, None))
                observations = [value for value in (branch, previous_branch) if value is not None]
                lines[number] = (hit or previous_hit, max(observations) if observations else None)
    if not files:
        raise ValueError("Reports contain no application source coverage")
    return files


def summarize(files, targets):
    if not targets:
        raise ValueError("Critical coverage targets are empty")
    output = ["# Critical backend coverage", "",
              "Combined unit/HTTP/SQL line hits; generated migrations are excluded. "
              "Real-browser and frontend JavaScript coverage are not included.", "",
              "| Source | Covered lines | Line coverage | Minimum |",
              "| --- | ---: | ---: | ---: |"]
    gaps = []
    errors = []
    for filename, minimum in targets.items():
        if not isinstance(minimum, (int, float)) or not 0 <= minimum <= 100:
            raise ValueError(f"Invalid coverage floor for {filename}")
        lines = files.get(filename, {})
        covered = sum(hit for hit, _ in lines.values())
        percent = 100 * covered / len(lines) if lines else 0
        output.append(f"| `{filename}` | {covered}/{len(lines)} | {percent:.1f}% | {minimum}% |")
        if not lines or percent < minimum:
            errors.append(f"{filename}: {percent:.1f}% < {minimum}% or source missing")
        uncovered = [str(n) for n, (hit, _) in sorted(lines.items()) if not hit]
        partial = [str(n) for n, (_, branch) in sorted(lines.items()) if branch is not None and branch < 100]
        gaps.extend([f"### {filename}", "",
                     "Uncovered lines: " + (", ".join(uncovered) or "none") + ".",
                     "", "Branch lines without a fully covered observation: "
                     + (", ".join(partial) or "none") + ".", ""])
    output.extend(["", "## Review locations", "",
                   "Line hits are unioned across reports, including async/nested classes. "
                   "Duplicate reports do not inflate counts. Branch percentages are not added: "
                   "the best per-line observation is used only to identify review locations. "
                   "A covered line is not proof that every condition or race was tested.", "", *gaps])
    if errors:
        output.extend(["## Gate failures", "", *[f"- {error}" for error in errors]])
    return "\n".join(output) + "\n", errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory")
    parser.add_argument("targets")
    parser.add_argument("output")
    args = parser.parse_args()
    try:
        targets = json.loads(Path(args.targets).read_text(encoding="utf-8-sig"))
        report, errors = summarize(collect(args.directory), targets)
        destination = Path(args.output)
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(report, encoding="utf-8")
        print(report.split("## Review locations")[0])
        if errors:
            parser.exit(1, "Critical coverage gate failed:\n" + "\n".join(errors) + "\n")
    except (ValueError, OSError, ET.ParseError, KeyError, TypeError) as error:
        parser.exit(1, f"Coverage verification failed: {error}\n")


if __name__ == "__main__":
    main()
