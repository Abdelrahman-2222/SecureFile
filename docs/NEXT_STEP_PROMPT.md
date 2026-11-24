# Next step — copy-paste prompt for a new chat

Last finished (2026-11-24): Step 4.1, and the **code** for Step 4.2 (not yet run). Branch `feature/stage-4-testing`
(cut from `main` after PR #2). Stages 1–3 are merged.

**Model and effort for the next chat:** Sonnet 5.5, effort **medium**. Start a NEW chat: the previous one was very long.

```text
Branch feature/stage-4-testing (do not create another). Follow CLAUDE.md. Finish Step 4.2 (large-file runs, T-27).
The test (tests/SecureFile.Integration.Tests/LargeFileTests.cs, opt-in with SECUREFILE_LARGE_TESTS=1) and the script
(scripts/large-file-check.ps1) are already written, built and committed, but NOT yet run on the real sizes.
Run it with the work folder the user chose (417 GB free on D:, needs ~15 GB):
  pwsh scripts/large-file-check.ps1 -WorkFolder "D:\Abelrahman\Abdelrahman Current\TestFolder"
(1 GiB and 4.5 GiB; takes several minutes; run it in the background and wait for the notification). Record time, MB/s and
peak memory for both sizes in docs/LEARNING_LOG.md under a short "Stage 4" section (append, do not read the file),
confirm the work folder is empty afterwards, mark plan row 4.2, add a CHANGELOG line, commit, push.
If the memory assertion fails (limit 400 MB), that is a real finding: report it, do not raise the limit to make it pass.
Then Step 4.3 (TESTPLAN.md manual script) and 4.4 (usability check). REPLACE THIS FILE at the end of each step.
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
