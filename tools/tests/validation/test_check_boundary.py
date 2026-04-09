import importlib.util
import json
import subprocess
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = TOOLS_ROOT / "validation" / "check_boundary.py"
REPO_ROOT = Path(__file__).resolve().parents[3]

spec = importlib.util.spec_from_file_location("check_boundary", SCRIPT_PATH)
check_boundary = importlib.util.module_from_spec(spec)
assert spec.loader is not None
sys.modules[spec.name] = check_boundary
spec.loader.exec_module(check_boundary)


def write_file(root: Path, relative_path: str, content: str) -> None:
    path = root / relative_path
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(textwrap.dedent(content).lstrip("\n"), encoding="utf-8")


def analyze_fixture(root: Path, scope: str = check_boundary.SCOPE_APP_ONLY):
    return check_boundary.analyze_repo(root, scope=scope)


class CheckBoundaryTests(unittest.TestCase):
    def test_repo_baseline_has_no_failures(self):
        report = check_boundary.analyze_repo(REPO_ROOT, scope=check_boundary.SCOPE_DEFAULT)
        self.assertEqual(report["summary"]["failures"], 0, json.dumps(report, ensure_ascii=False, indent=2))

    def test_bd004_rejects_monobehaviour_in_shared(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            write_file(
                root,
                "Assets/Scripts/App.Shared/Foo.cs",
                """
                using UnityEngine;

                namespace App.Shared
                {
                    public class SharedView : MonoBehaviour
                    {
                    }
                }
                """,
            )
            report = analyze_fixture(root)
            rule_ids = [item["rule_id"] for item in report["findings"]]
            self.assertIn("BD004", rule_ids)

    def test_bd003_rejects_hotupdate_reference_in_shared(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            write_file(
                root,
                "Assets/Scripts/App.Shared/Foo.cs",
                """
                using App.HotUpdate;

                namespace App.Shared
                {
                    public class SharedRuntimeData
                    {
                    }
                }
                """,
            )
            report = analyze_fixture(root)
            rule_ids = [item["rule_id"] for item in report["findings"]]
            self.assertIn("BD003", rule_ids)

    def test_bd006_rejects_resources_load_in_hotupdate(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            write_file(
                root,
                "Assets/HotUpdateContent/Script/App.HotUpdate/Foo.cs",
                """
                using UnityEngine;

                namespace App.HotUpdate
                {
                    public class Loader
                    {
                        public Object Load()
                        {
                            return Resources.Load("foo");
                        }
                    }
                }
                """,
            )
            report = analyze_fixture(root)
            rule_ids = [item["rule_id"] for item in report["findings"]]
            self.assertIn("BD006", rule_ids)

    def test_bd007_warns_on_runtime_terrain_write(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            write_file(
                root,
                "Assets/Minesweeper/Scripts/Foo.cs",
                """
                public class TerrainMutator
                {
                    private MinesweeperTerrainData terrain;

                    public void Mutate()
                    {
                        terrain.SetColor(0, 0, default);
                    }
                }
                """,
            )
            report = analyze_fixture(root, scope=check_boundary.SCOPE_DEFAULT)
            warning_ids = [item["rule_id"] for item in report["findings"] if item["severity"] == "warning"]
            self.assertIn("BD007", warning_ids)

    def test_bd008_warns_on_suspicious_shared_service_with_method_body(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            write_file(
                root,
                "Assets/Scripts/App.Shared/FooService.cs",
                """
                namespace App.Shared
                {
                    public class FooService
                    {
                        public int Run()
                        {
                            return 1;
                        }
                    }
                }
                """,
            )
            report = analyze_fixture(root)
            warning_ids = [item["rule_id"] for item in report["findings"] if item["severity"] == "warning"]
            self.assertIn("BD008", warning_ids)

    def test_normalize_path_uses_forward_slashes(self):
        normalized = check_boundary.normalize_path("Assets\\Scripts\\App.Shared\\Foo.cs")
        self.assertEqual(normalized, "Assets/Scripts/App.Shared/Foo.cs")

    def test_shell_wrapper_forwards_arguments(self):
        completed = subprocess.run(
            ["bash", str(REPO_ROOT / "tools/validation/check_boundary.sh"), "--json"],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(completed.returncode, 0, completed.stderr)
        payload = json.loads(completed.stdout)
        self.assertIn("summary", payload)

    def test_batch_wrapper_contains_argument_passthrough(self):
        batch_path = REPO_ROOT / "tools/validation/check_boundary.bat"
        content = batch_path.read_text(encoding="utf-8")
        self.assertIn("%*", content)
        self.assertIn("check_boundary.py", content)


if __name__ == "__main__":
    unittest.main()
