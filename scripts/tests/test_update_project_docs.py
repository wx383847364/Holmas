import importlib.util
import subprocess
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path
from shutil import copy2


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "update_project_docs.py"
FINALIZE_SCRIPT = Path(__file__).resolve().parents[1] / "finalize_task.sh"

spec = importlib.util.spec_from_file_location("update_project_docs", SCRIPT_PATH)
update_project_docs = importlib.util.module_from_spec(spec)
assert spec.loader is not None
sys.modules[spec.name] = update_project_docs
spec.loader.exec_module(update_project_docs)


def write_file(root: Path, relative_path: str, content: str) -> Path:
    path = root / relative_path
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(textwrap.dedent(content).lstrip("\n"), encoding="utf-8")
    return path


def create_doc_root(root: Path) -> Path:
    doc_root = root / "doc"
    write_file(
        root,
        "doc/长期主文档/协作与执行/Agent 启动与验收规范.md",
        """
        # Agent 启动与验收规范

        ## 迭代文档默认分工状态

        - Agent 1：Shared 与骨架继续保持冻结，本轮未改
        - Agent 2：地图与棋盘纯逻辑作为被验证对象，本轮未改生产逻辑
        - Agent 3：任务与长期进度纯逻辑作为被验证对象，本轮未改生产逻辑
        - Agent 4：UI 与验证，暂未启动
        - Agent 5：测试与质量保障，默认按需启动，本轮未启动
        """,
    )
    write_file(
        root,
        "doc/长期主文档/项目总览.md",
        """
        # 项目总览

        ## 当前阶段

        - 当前阶段：暂无
        - 当前主线：暂无
        - 当前建议起点：暂无明确建议
        """,
    )
    return doc_root


class UpdateProjectDocsTests(unittest.TestCase):
    def test_new_iteration_uses_default_agent_statuses(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            path = update_project_docs.create_iteration(doc_root, "主题", "目标", "进行中")
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 1：Shared 与骨架继续保持冻结，本轮未改", text)
            self.assertIn("- Agent 5：测试与质量保障，默认按需启动，本轮未启动", text)
            self.assertNotIn("Agent 1：待补充", text)

    def test_append_iteration_overrides_only_target_agent(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            path = update_project_docs.create_iteration(doc_root, "主题", "目标", "进行中")

            update_project_docs.append_iteration(
                doc_root,
                str(path),
                False,
                "完成边界测试补强",
                [],
                [],
                [],
                ["Agent 5：已启动并完成第 1 组与第 2 组核心边界测试补强"],
            )
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 5：已启动并完成第 1 组与第 2 组核心边界测试补强", text)
            self.assertIn("- Agent 1：Shared 与骨架继续保持冻结，本轮未改", text)
            self.assertIn("- Agent 4：UI 与验证，暂未启动", text)

    def test_append_iteration_backfills_placeholder_agent_block(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            path = write_file(
                root,
                "doc/迭代记录/迭代记录_20260330_001.md",
                """
                # 迭代记录 2026-03-30 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            update_project_docs.append_iteration(
                doc_root,
                str(path),
                False,
                "补录测试结果",
                ["新增脚本测试"],
                [],
                ["继续补充分工状态规则"],
                [],
            )
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 2：地图与棋盘纯逻辑作为被验证对象，本轮未改生产逻辑", text)
            self.assertIn("- 新增脚本测试", text)
            self.assertNotIn("## 完成项\n\n- 暂无", text)
            self.assertNotIn("Agent 3：待补充", text)

    def test_append_iteration_preserves_manual_agent_lines(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            path = write_file(
                root,
                "doc/迭代记录/迭代记录_20260331_001.md",
                """
                # 迭代记录 2026-03-31 001

                ## 分工状态

                - Agent 1：Shared 与骨架继续保持冻结，本轮未改
                - Agent 2：已启动并完成地图配置接线
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                """,
            )

            update_project_docs.append_iteration(doc_root, str(path), False, "", [], [], [], [])
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 2：已启动并完成地图配置接线", text)
            self.assertIn("- Agent 3：任务与长期进度纯逻辑作为被验证对象，本轮未改生产逻辑", text)
            self.assertIn("- Agent 5：测试与质量保障，默认按需启动，本轮未启动", text)

    def test_summary_does_not_infer_agent_status(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            path = update_project_docs.create_iteration(doc_root, "主题", "目标", "进行中")

            update_project_docs.append_iteration(
                doc_root,
                str(path),
                False,
                "验证地图线并补 Agent 5 测试",
                [],
                [],
                [],
                [],
            )
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 5：测试与质量保障，默认按需启动，本轮未启动", text)
            self.assertNotIn("已启动并完成", text)

    def test_backfill_agent_status_updates_only_placeholders(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            old_path = write_file(
                root,
                "doc/迭代记录/迭代记录_20260329_001.md",
                """
                # 旧记录

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                """,
            )
            new_path = write_file(
                root,
                "doc/迭代记录/迭代记录_20260330_001.md",
                """
                # 新记录

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：已启动并完成地图配置接线
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充

                ## 完成项

                - 新增地图配置入口
                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 继续补齐配置校验
                - 待补充
                """,
            )

            updated = update_project_docs.backfill_agent_status(doc_root, "20260330")
            self.assertEqual([path.name for path in updated], [new_path.name])

            self.assertIn("Agent 1：待补充", old_path.read_text(encoding="utf-8"))
            text = new_path.read_text(encoding="utf-8")
            self.assertIn("- Agent 2：已启动并完成地图配置接线", text)
            self.assertIn("- Agent 3：任务与长期进度纯逻辑作为被验证对象，本轮未改生产逻辑", text)
            self.assertNotIn("## 完成项\n\n- 暂无", text)
            self.assertNotIn("## 下一步\n\n- 待补充", text)

    def test_finalize_task_forwards_agent_status(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            scripts_dir = repo_root / "scripts"
            scripts_dir.mkdir(parents=True, exist_ok=True)
            copy2(SCRIPT_PATH, scripts_dir / "update_project_docs.py")
            copy2(FINALIZE_SCRIPT, scripts_dir / "finalize_task.sh")
            doc_root = create_doc_root(repo_root)

            iteration_path = write_file(
                repo_root,
                "doc/迭代记录/迭代记录_20260402_001.md",
                """
                # 迭代记录 2026-04-02 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            completed = subprocess.run(
                [
                    "bash",
                    str(scripts_dir / "finalize_task.sh"),
                    "--file",
                    str(iteration_path),
                    "--summary",
                    "完成边界检查脚本规则收敛",
                    "--done",
                    "分工状态改为长期规则源驱动",
                    "--agent-status",
                    "Agent 5：已启动并完成核心脚本验证",
                    "--skip-temp-cleanup",
                ],
                cwd=repo_root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            text = iteration_path.read_text(encoding="utf-8")
            self.assertIn("- Agent 5：已启动并完成核心脚本验证", text)
            self.assertIn("- 分工状态改为长期规则源驱动", text)


if __name__ == "__main__":
    unittest.main()
