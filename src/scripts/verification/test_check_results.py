import json
from pathlib import Path
import tempfile
import unittest

from check_results import check_playwright, check_trx


class ResultGateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)

    def trx(self, total=1, passed=1, outcome="Passed", assembly="Tests.dll"):
        (self.directory / "run.trx").write_text(f'''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results><UnitTestResult testId="1" outcome="{outcome}" /></Results>
          <TestDefinitions><UnitTest id="1"><TestMethod codeBase="/tests/{assembly}" /></UnitTest></TestDefinitions>
          <ResultSummary outcome="Completed"><Counters total="{total}" executed="{passed}" passed="{passed}" /></ResultSummary>
        </TestRun>''', encoding="utf-8")

    def test_success(self):
        self.trx()
        self.assertIn("1 passed", check_trx(self.directory, ["Tests.dll"]))

    def test_missing_report(self):
        with self.assertRaises(ValueError):
            check_trx(self.directory, ["Tests.dll"])

    def test_empty_skipped_failed_or_incomplete(self):
        for total, passed, outcome in [(0, 0, "Passed"), (1, 0, "NotExecuted"),
                                       (1, 0, "Failed"), (2, 2, "Passed")]:
            with self.subTest(total=total, passed=passed, outcome=outcome):
                self.trx(total, passed, outcome)
                with self.assertRaises(ValueError):
                    check_trx(self.directory, ["Tests.dll"])

    def test_missing_assembly(self):
        self.trx()
        with self.assertRaises(ValueError):
            check_trx(self.directory, ["Tests.dll", "Other.dll"])

    def test_browser_gates(self):
        report = self.directory / "browser.json"
        for expected, unexpected, skipped, valid in [(1, 0, 0, True), (0, 0, 0, False),
                                                     (1, 0, 1, False), (0, 1, 0, False)]:
            report.write_text(json.dumps({"stats": {"expected": expected, "unexpected": unexpected,
                                                  "skipped": skipped, "flaky": 0}}), encoding="utf-8")
            if valid:
                check_playwright(report)
            else:
                with self.assertRaises(ValueError):
                    check_playwright(report)


if __name__ == "__main__":
    unittest.main()
