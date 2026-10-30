from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from core.commands import compress_commands, video_bitrate_kbps, webm_commands
from core.errors import FluxoError
from core.models import CompressOptions, CompressionPreset, VideoCodec, WebmOptions
from core.processor import VideoProcessor


class CoreTests(unittest.TestCase):
    def test_bitrate_respects_budget(self) -> None:
        result = video_bitrate_kbps(60, 10, 128)
        self.assertGreater(result, 1000)
        self.assertLess(result, 1300)

    def test_impossible_target_is_rejected(self) -> None:
        with self.assertRaises(FluxoError):
            video_bitrate_kbps(3600, 2, 128)

    def test_compression_two_pass_has_progress_and_unique_log(self) -> None:
        commands, passlog = compress_commands(
            Path("ffmpeg.exe"), Path("in.mp4"), Path("out.mp4"), VideoCodec.H264,
            60, 10, 128, False, True,
        )
        self.assertEqual(len(commands), 2)
        self.assertIn("-progress", commands[0])
        self.assertIsNotNone(passlog)

    def test_vp9_encoder(self) -> None:
        commands, _ = webm_commands(
            Path("ffmpeg.exe"), Path("in.mp4"), Path("out.webm"), VideoCodec.VP9,
            60, 10, 128, False,
        )
        self.assertIn("libvpx-vp9", commands[0])

    def test_output_name_is_non_destructive(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            original = Path(directory) / "video.mp4"
            original.touch()
            candidate = VideoProcessor._unique(original)
            self.assertEqual(candidate.name, "video (2).mp4")

    def test_requested_defaults(self) -> None:
        self.assertEqual(CompressOptions().preset, CompressionPreset.CUSTOM)
        self.assertEqual(CompressOptions().target_mb, 98)
        self.assertTrue(CompressOptions().copy_audio)
        self.assertEqual(WebmOptions().codec, VideoCodec.VP8)


if __name__ == "__main__":
    unittest.main()
