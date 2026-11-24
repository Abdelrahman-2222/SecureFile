# Next step — copy-paste prompt for a new chat

Last finished (2026-11-24): Step 4.1 (integration tests on real files). Branch `feature/stage-4-testing` (cut from `main`
after PR #2). Stages 1–3 are merged.

**Model and effort for the next chat:** Sonnet 5.5, effort **medium**. Step 4.2 is mostly a script plus waiting for runs.

```text
Branch feature/stage-4-testing (do not create another). Follow CLAUDE.md. Step 4.2: large-file runs (grep "^| 4.2"
docs/PROJECT_PLAN.md): 1 GB and a file larger than 4 GB, round trip OK, memory stays flat. These are manual or nightly,
NOT part of the pre-push gate or per-commit CI. Suggested shape: a script scripts/large-file-check.ps1 that creates the
file with `fsutil file createnew`, runs encrypt then decrypt through the real FileEncryptionService (a small console
project or a test marked to run only with SECUREFILE_LARGE_TESTS=1, like SECUREFILE_UI_TESTS), compares hashes, and
samples the process's peak working set. Run it on this machine (needs ~10 GB free temp space; say so before starting) and
record the numbers (time, MB/s, peak memory) in docs/LEARNING_LOG.md under a short "Stage 4" section.
Per step: build + tests, one commit, mark the plan row, CHANGELOG line, and REPLACE THIS FILE at the end of the step.
Then 4.3 (TESTPLAN.md manual script) and 4.4 (usability check).
```

**Still open (cheap, do whenever convenient)**
- Not verified by hand from Stage 3: FastPass Tab Stops, Narrator, dark mode, 150%/200% scaling, real drag-and-drop,
  Open folder click. They belong in the TESTPLAN.md manual script (Step 4.3).

**Open items for the final hardening stage**
- Two flaky Core tests, both temp-file cleanup timing on Windows (see the project notes): 
  `FileDecryptionTests.DecryptAsync_CancelledFromTheProgressReport_ThrowsAndLeavesNothing(length: 3145728)` and
  `FileDecryptionTests.DecryptAsync_WrongPasswordWithOverwriteAllowed_KeepsTheExistingFile`.
- Deferred review items: Unix errno values in `ErrorMessageProvider`, `IsSameFile` case rule on Linux, `FileErrorLog`
  tests, `DateTime` vs `Stopwatch` for the repeat-error window, About dialog width at large text scaling.
- T-23 (disk full) cannot be made with real files here; consider a fake-stream unit test.
