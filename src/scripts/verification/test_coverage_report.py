from pathlib import Path
import tempfile
import unittest

from coverage_report import collect, summarize


class CoverageGateTests(unittest.TestCase):
    def test_union_deduplicates_reports_and_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            for name, filename, hit in [("unit", "Biogarden.BusinessLogic/Test.cs", 1),
                                         ("sql", "C:\\repo\\Biogarden.BusinessLogic\\Test.cs", 0)]:
                path = Path(directory) / name
                path.mkdir()
                (path / "coverage.cobertura.xml").write_text(f'''<coverage><packages><package><classes>
                  <class filename="{filename}"><lines><line number="1" hits="{hit}" /></lines></class>
                </classes></package></packages></coverage>''', encoding="utf-8")
            files = collect(directory)
            self.assertEqual({"Biogarden.BusinessLogic/Test.cs": {1: (True, None)}}, files)

    def test_missing_reports_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ValueError):
                collect(directory)

    def test_missing_source_and_regression_fail(self):
        for files in ({}, {"Test.cs": {1: (True, 100), 2: (False, 0)}}):
            report, errors = summarize(files, {"Test.cs": 75})
            self.assertTrue(errors)
            self.assertIn("Gate failures", report)

    def test_threshold_boundary_passes(self):
        _, errors = summarize({"Test.cs": {1: (True, 50), 2: (False, 0)}}, {"Test.cs": 50})
        self.assertFalse(errors)


if __name__ == "__main__":
    unittest.main()
