from __future__ import annotations

import os
import tempfile
import unittest
from pathlib import Path

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PySide6.QtCore import QSettings
from PySide6.QtWidgets import QApplication, QPushButton, QScrollArea

from core.models import CompressionPreset, SplitMode, TaskResult, VideoCodec
from ui.main_window import MainWindow
from ui.pages import CompressPage, SplitPage, WebmPage


class UiLayoutTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.app = QApplication.instance() or QApplication([])

    def setUp(self) -> None:
        self.window = MainWindow()

    def tearDown(self) -> None:
        self.window.close()
        self.app.processEvents()

    def test_starts_on_compress_without_clipping_at_default_size(self) -> None:
        self.window.resize(1280, 900)
        self.window.show()
        self.app.processEvents()
        page_scroll = self.window.findChild(QScrollArea, "pageScroll")
        self.assertEqual(self.window.stack.currentIndex(), 1)
        self.assertFalse(page_scroll.verticalScrollBar().isVisible())

    def test_small_window_scrolls_instead_of_compressing_page(self) -> None:
        self.window.resize(980, 700)
        self.window.show()
        self.app.processEvents()
        page_scroll = self.window.findChild(QScrollArea, "pageScroll")
        page = self.window.pages[1]
        self.assertTrue(page_scroll.verticalScrollBar().isVisible())
        self.assertGreaterEqual(self.window.stack.height(), page.minimumSizeHint().height())

    def test_all_action_buttons_emit_typed_tasks_without_exception(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "video.mp4"
            source.write_bytes(b"0" * 2048)
            settings = QSettings(str(root / "settings.ini"), QSettings.Format.IniFormat)

            split = SplitPage(settings)
            split.files.list.add_paths([source])
            split_tasks = []
            split.runNowRequested.connect(split_tasks.extend)
            split._start()
            self.assertEqual(split_tasks[0].options.mode, SplitMode.PARTS)

            compress = CompressPage(settings)
            compress.files.list.add_paths([source])
            compress.target.set_value(1, "KB")
            compress_tasks = []
            compress.runNowRequested.connect(compress_tasks.extend)
            compress._start()
            self.assertEqual(compress_tasks[0].options.preset, CompressionPreset.CUSTOM)
            self.assertEqual(compress_tasks[0].options.codec, VideoCodec.H264)

            webm = WebmPage(settings)
            webm.files.list.add_paths([source])
            webm_tasks = []
            webm.runNowRequested.connect(webm_tasks.extend)
            webm._start()
            self.assertEqual(webm_tasks[0].options.codec, VideoCodec.VP8)

    def test_queue_ui_and_queue_actions_are_removed(self) -> None:
        self.assertFalse(hasattr(self.window, "queue"))
        self.assertFalse(hasattr(self.window, "queue_frame"))
        for page in self.window.pages:
            self.assertFalse(hasattr(page, "queue_button"))
            labels = [button.text().lower() for button in page.findChildren(QPushButton)]
            self.assertFalse(any("fila" in label for label in labels))

    def test_multiple_files_are_emitted_as_one_direct_batch(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            sources = [Path(directory) / "video-1.mp4", Path(directory) / "video-2.mp4"]
            for source in sources:
                source.write_bytes(b"0" * 2048)
            settings = QSettings(str(Path(directory) / "settings.ini"), QSettings.Format.IniFormat)
            page = CompressPage(settings)
            page.files.list.add_paths(sources)
            page.target.set_value(1, "KB")
            batches = []
            page.runNowRequested.connect(batches.append)
            page._start()

            self.assertEqual(len(batches), 1)
            self.assertEqual(len(batches[0]), 2)

    def test_compression_target_resets_to_98_mb_on_every_launch(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            settings = QSettings(str(Path(directory) / "settings.ini"), QSettings.Format.IniFormat)
            settings.setValue("compress/target_value", 37)
            settings.setValue("compress/target_unit", "GB")
            page = CompressPage(settings)
            self.assertEqual(page.target.raw_value(), 98)
            self.assertEqual(page.target.unit(), "MB")

    def test_overall_progress_tracks_active_task(self) -> None:
        task_id = "test-task"
        self.window.batch_task_ids.add(task_id)
        self.window.task_states[task_id] = "running"
        self.window.task_progress[task_id] = 42.0
        self.window.task_statuses[task_id] = "Processando • restante 8s"
        self.window.task_names[task_id] = "video.mp4"
        self.window._update_overall_progress()

        self.assertEqual(self.window.overall_progress.progress.value(), 420)
        self.assertIn("42%", self.window.overall_progress.summary.text())
        self.assertIn("video.mp4", self.window.overall_progress.status.text())

    def test_finished_progress_action_becomes_open_folder(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "video_compactado.mp4"
            task_id = "finished-task"
            self.window.batch_task_ids.add(task_id)
            self.window.task_states[task_id] = "running"
            self.window.task_progress[task_id] = 99.0
            self.window.task_statuses[task_id] = "Finalizando"
            self.window.task_names[task_id] = "video.mp4"
            self.window._task_succeeded(task_id, "video.mp4", TaskResult((output,), "Concluído"))

            self.assertEqual(self.window.overall_progress.action_button.text(), "Abrir pasta")
            self.assertTrue(self.window.overall_progress.action_button.isEnabled())
            self.assertEqual(self.window.last_output_dir, Path(directory))

    def test_theme_combo_value_is_normalized_before_saving(self) -> None:
        self.window.theme_combo.setCurrentIndex(2)
        self.window._theme_selected()
        self.assertEqual(self.window.settings.value("appearance/theme"), "dark")


if __name__ == "__main__":
    unittest.main()
