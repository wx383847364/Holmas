import importlib.util
import os
import subprocess
import sys
import tempfile
import textwrap
import unittest
from datetime import datetime
from pathlib import Path
from shutil import copy2


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "update_project_docs.py"
FINALIZE_SCRIPT = Path(__file__).resolve().parents[1] / "finalize_task.sh"
SYNC_SKILLS_SCRIPT = Path(__file__).resolve().parents[1] / "sync_codex_skills.sh"

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
        - Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动
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
    def test_finalize_task_help_mentions_sync_for_overview_and_index(self):
        completed = subprocess.run(
            ["bash", str(FINALIZE_SCRIPT), "--help"],
            capture_output=True,
            text=True,
            check=False,
        )

        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertIn("passed / passed-with-suggestions / failed / pending / deferred / not-required", completed.stdout)
        self.assertIn("项目总览.md", completed.stdout)
        self.assertIn("update_project_docs.py sync", completed.stdout)
        self.assertIn("文档维护", completed.stdout)
        self.assertIn("Git 提交建议", completed.stdout)

    def test_new_iteration_uses_default_agent_statuses(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            path = update_project_docs.create_iteration(doc_root, "主题", "目标", "进行中")
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 1：Shared 与骨架继续保持冻结，本轮未改", text)
            self.assertIn("- Agent 5：测试与质量保障，默认按需启动，本轮未启动", text)
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)
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
                ["Agent 6：已启动并完成本轮挑刺与问题审查"],
            )
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 6：已启动并完成本轮挑刺与问题审查", text)
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
                - Agent 6：待补充

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
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)
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
                - Agent 6：待补充
                """,
            )

            update_project_docs.append_iteration(doc_root, str(path), False, "", [], [], [], [])
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 2：已启动并完成地图配置接线", text)
            self.assertIn("- Agent 3：任务与长期进度纯逻辑作为被验证对象，本轮未改生产逻辑", text)
            self.assertIn("- Agent 5：测试与质量保障，默认按需启动，本轮未启动", text)
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)

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
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)
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
                - Agent 6：待补充
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
                - Agent 6：待补充

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
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)
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
                - Agent 6：待补充

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
                env={**os.environ, "CODEX_HOME": str(repo_root / ".codex")},
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            text = iteration_path.read_text(encoding="utf-8")
            self.assertIn("- Agent 5：已启动并完成核心脚本验证", text)
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)
            self.assertIn("- 分工状态改为长期规则源驱动", text)

    def test_finalize_task_outputs_fixed_wrapup_sections_with_chinese_commit_message(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            scripts_dir = repo_root / "scripts"
            scripts_dir.mkdir(parents=True, exist_ok=True)
            copy2(SCRIPT_PATH, scripts_dir / "update_project_docs.py")
            copy2(FINALIZE_SCRIPT, scripts_dir / "finalize_task.sh")
            create_doc_root(repo_root)

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
                - Agent 6：待补充

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
                    "统一新会话启动与收尾脚本输出",
                    "--done",
                    "为收尾流程补固定三段输出",
                    "--done",
                    "为提交建议补中文标题和内容",
                    "--agent6-review",
                    "passed",
                    "--skip-temp-cleanup",
                ],
                cwd=repo_root,
                env={**os.environ, "CODEX_HOME": str(repo_root / ".codex")},
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertIn("文档维护：已执行", completed.stdout)
            self.assertIn("Git 提交建议：适合提交", completed.stdout)
            self.assertIn("标题：流程：新会话启动与收尾脚本输出", completed.stdout)
            self.assertIn("内容：", completed.stdout)
            self.assertIn("- 为收尾流程补固定三段输出", completed.stdout)
            self.assertIn("- 为提交建议补中文标题和内容", completed.stdout)
            self.assertIn("会话建议：", completed.stdout)

    def test_finalize_task_auto_syncs_skills_when_skill_source_changes(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            scripts_dir = repo_root / "scripts"
            scripts_dir.mkdir(parents=True, exist_ok=True)
            copy2(SCRIPT_PATH, scripts_dir / "update_project_docs.py")
            copy2(FINALIZE_SCRIPT, scripts_dir / "finalize_task.sh")
            copy2(SYNC_SKILLS_SCRIPT, scripts_dir / "sync_codex_skills.sh")
            create_doc_root(repo_root)
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
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )
            write_file(
                repo_root,
                "doc/长期主文档/协作与执行/skills/unity-hotupdate-boundary/SKILL.md",
                """
                ---
                name: unity-hotupdate-boundary
                description: fixture
                ---

                # Fixture Skill
                """,
            )

            subprocess.run(["git", "init"], cwd=repo_root, check=True, capture_output=True, text=True)

            completed = subprocess.run(
                [
                    "bash",
                    str(scripts_dir / "finalize_task.sh"),
                    "--file",
                    str(iteration_path),
                    "--summary",
                    "同步 skill 真源",
                    "--skip-temp-cleanup",
                ],
                cwd=repo_root,
                env={**os.environ, "CODEX_HOME": str(repo_root / ".codex")},
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            synced_path = repo_root / ".codex" / "skills" / "unity-hotupdate-boundary" / "SKILL.md"
            self.assertTrue(synced_path.exists())
            self.assertIn("自动同步到 ~/.codex/skills", completed.stdout)

    def test_finalize_task_skips_skill_sync_when_skill_source_is_unchanged(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            scripts_dir = repo_root / "scripts"
            scripts_dir.mkdir(parents=True, exist_ok=True)
            copy2(SCRIPT_PATH, scripts_dir / "update_project_docs.py")
            copy2(FINALIZE_SCRIPT, scripts_dir / "finalize_task.sh")
            copy2(SYNC_SKILLS_SCRIPT, scripts_dir / "sync_codex_skills.sh")
            create_doc_root(repo_root)
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
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            subprocess.run(["git", "init"], cwd=repo_root, check=True, capture_output=True, text=True)

            completed = subprocess.run(
                [
                    "bash",
                    str(scripts_dir / "finalize_task.sh"),
                    "--file",
                    str(iteration_path),
                    "--summary",
                    "普通任务收尾",
                    "--skip-temp-cleanup",
                ],
                cwd=repo_root,
                env={**os.environ, "CODEX_HOME": str(repo_root / ".codex")},
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            synced_root = repo_root / ".codex" / "skills"
            self.assertFalse(synced_root.exists())
            self.assertNotIn("自动同步到 ~/.codex/skills", completed.stdout)

    def test_suggest_agent_start_does_not_recommend_agent_6(self):
        suggested = update_project_docs.suggest_agent_start(["- 先交给 Agent 6 做挑刺审查"])
        self.assertNotEqual(suggested, "Agent 6：挑刺与问题审查")
        self.assertEqual(suggested, "暂无明确建议")

    def test_suggest_handoff_pending_review_uses_review_session_when_context_degraded(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            today = datetime.now().strftime("%Y%m%d")
            write_file(
                root,
                f"doc/迭代记录/迭代记录_{today}_001.md",
                f"""
                # 迭代记录 {datetime.now().strftime('%Y-%m-%d')} 001

                ## 本轮主题

                流程与协作规则收尾

                ## 工作日志

                ### {datetime.now().strftime('%Y-%m-%d %H:%M')}

                - 完成第一轮规则收口
                """,
            )

            report = update_project_docs.suggest_handoff(
                doc_root,
                "完成第二轮规则收口",
                ["补齐脚本规则"],
                [],
                ["继续当前主线"],
                "pending",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                True,
                0,
            )

            self.assertEqual(report["session_advice"], "建议新开修复/复审会话")
            self.assertIn("审查已发起但结果尚未回传", report["session_reason"])
            self.assertIn("自动压缩背景信息", report["session_reason"])

    def test_suggest_handoff_deferred_review_after_reviewer_handoff_stays_in_current_fix_session(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            # Old reviewer timed out; a new reviewer continues the same review_chain_id.
            report = update_project_docs.suggest_handoff(
                doc_root,
                "继续收敛 Agent 6 去阻塞规则",
                ["补齐状态机定义"],
                [],
                ["继续当前修复与验证"],
                "deferred",
                "auto",
                "continue",
                "",
                "",
                [],
                False,
                False,
                0,
            )

            self.assertEqual(report["session_advice"], "继续当前修复/验证会话")
            self.assertIn("待回补复审", report["session_reason"])
            self.assertEqual(report["iteration_advice"], "继续当前迭代记录")

    def test_suggest_handoff_prefers_new_session_after_two_same_session_major_tasks(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            report = update_project_docs.suggest_handoff(
                doc_root,
                "继续补第三轮规则收口",
                ["更新收尾模板"],
                [],
                ["继续当前主线"],
                "passed",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                False,
                2,
            )

            self.assertEqual(report["session_advice"], "建议新开下一阶段会话")
            self.assertIn("当前会话内已连续完成 2 个大任务", report["session_reason"])

    def test_suggest_handoff_generates_chinese_commit_suggestion(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            report = update_project_docs.suggest_handoff(
                doc_root,
                "统一新会话启动与收尾流程",
                ["为收尾流程补固定三段输出", "为提交建议补中文标题和内容"],
                [],
                ["继续推进脚本收尾规则"],
                "passed",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                False,
                0,
            )

            self.assertEqual(report["doc_maintenance_status"], "已执行")
            self.assertTrue(report["commit_suggestion"]["suitable"])
            self.assertEqual(report["commit_suggestion"]["title"], "流程：新会话启动与收尾流程")
            self.assertEqual(
                report["commit_suggestion"]["content"][:2],
                ["为收尾流程补固定三段输出", "为提交建议补中文标题和内容"],
            )
            formatted = update_project_docs.format_handoff_report(report)
            self.assertIn("文档维护：已执行", formatted)
            self.assertIn("Git 提交建议：适合提交", formatted)
            self.assertIn("标题：流程：新会话启动与收尾流程", formatted)
            self.assertIn("会话建议：", formatted)


if __name__ == "__main__":
    unittest.main()
