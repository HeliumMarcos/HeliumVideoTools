# Installed performance measurement plan

**Goal:** Measure the existing 3.0.14 installed self-contained application, without changing product behavior or creating a release merely for measurements.

**Method:** External Windows UI Automation client launches only its own app processes. Six normal launches record time until the main action exists/enabled; last launch processes a new 180-second/720p synthetic video through the real UI (98 MB H.264/two-pass/original audio, three precise parts, VP8/two-pass). Sample concurrent working/private bytes of app plus owned descendants. Verify outputs with installed ffprobe, applicable caps, source hash and settings restoration. Store raw JSON under work and a path-free report in docs.

**Files:** tests/HeliumVideoTools.Tests/InstalledPerformanceBenchmarks.cs; Program.cs CLI entry; docs/performance/installed-3.0.14.json; docs/DESEMPENHO-INSTALADO.md.

**Constraints:** Refuse an existing app session; close/kill only a process started by this run with the verified installed path. Do not touch user videos, clear OS caches, restart Windows or change display/Narrator settings. Preserve original preferences bytes. First observed launch is not a guaranteed cold launch. No comparison against an incompatible host/runtime/DPI baseline.

- [x] Implement measurement client and verify process-tree memory aggregation with focused checks.
- [x] Run six observed launches, then three UI processing scenarios with synthetic media and output/hash checks.
- [x] Inspect evidence, document polling/measurement limitations and any material bottleneck; no speculative optimization.
- [x] Commit results and harness, update remaining scope honestly. No installer copied to Music.

## Evidence

Successful series: work/installed-performance/run-573c251c177c4a17854990b37005511d. Startup and media measured through installed 3.0.14 UI; preferences restored byte-for-byte. Native picker trials aborted before processing; final selection assisted via Computer Use outside timing. Sampler host/owned-child check passed; Release build has no warnings/errors. Cold-cache startup remains unmeasured and is explicitly retained in the scope; no reboot/cache flushing was performed. See DESEMPENHO-INSTALADO.md.
