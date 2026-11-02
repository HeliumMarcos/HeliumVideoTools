# Recovery and accessibility implementation plan

> **For agentic workers:** Execute inline with superpowers:executing-plans; no delegation required.

**Goal:** Complete batch-interruption recovery and the accessibility checks possible in this environment together.

**Architecture:** A leased, atomic batch journal stores metadata and published-output references outside the installation. Startup converts abandoned snapshots into idempotent reports, restoring the latest result into the existing summary. No automatic encoding restart, task queue or new visual layout. Existing media/tool/installer recovery remains separate.

**Tech Stack:** C#/.NET 10, WPF, Windows UI Automation, existing standalone tests and GitHub Actions.

**Spec:** docs/ESCOPO-RESTANTE.md; user requests items 2 and 3 together. Existing previews/layout approvals and no installer copy to Music remain binding.

## Constraints and review focus

- Preserve originals, completed outputs, unknown files and active leases; only the app's journal metadata may be removed.
- Snapshot writes atomic; crash while publishing cannot fabricate a successful item. Unknown/corrupt snapshots preserved and ignored.
- Recovery/report creation idempotent; active job never restored as abandoned. Missing/changed outputs reported honestly.
- No resumed encoding, queue, monitor configuration changes or claimed Narrator/audio validation without evidence.
- Tests isolate profile, processes and windows; no user video or app process is terminated.

## Tasks

- [x] Add failing recovery tests: lease, interrupted publication, completed/not-started state, missing output, corrupt metadata, repeated recovery.
- [x] Implement Infrastructure/BatchRecoveryStore and optional BatchRunner checkpoint integration, then test forced worker death.
- [x] Integrate production ViewModel journal and startup restoration through existing summary/report/pasta controls. Test WPF restoration without changing layout.
- [x] Extend keyboard/UIA and available display checks; add scale-render checks clearly distinguished from physical display tests. Do not enable Narrator or change display settings blindly.
- [x] Run recovery/media, WPF, keyboard/UIA, display and installer suites; publish one stable release only after checks. Update remaining environmental limits honestly.

## Execution record

Implementation and automated validation completed inline. Missing API produced the initial failing build; forced-worker tests passed after implementation. User explicitly deferred Narrator audio. Physical mixed-DPI monitors are unavailable; this is an environmental limit, not a passed test. No delegation or new UI layout. Stable 3.0.14 passed distribution run 41493104918 and anonymous release verification 41493942250. See docs/BATCH-RECOVERY.md for remaining guarantees/limits.
