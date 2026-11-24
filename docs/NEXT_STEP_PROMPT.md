# Next step — copy-paste prompt for a new chat

Last finished (2026-11-24): Steps 4.2–4.4 as far as a machine can take them. Branch `feature/stage-4-testing`
(cut from `main` after PR #2). Stages 1–3 are merged.

**Model and effort for the next chat:** Sonnet 5.5, effort **medium**. Start a NEW chat: the previous one was long.

```text
Branch feature/stage-4-testing (do not create another). Follow CLAUDE.md. Stage 4 remaining work needs the user:
1. T-27 4.5 GiB run: Kaspersky killed SecureFile.Integration.Tests.exe both times (see LEARNING_LOG, Stage 4). Only
   the 1 GiB case is measured (96/100 MB/s, 46 MB peak). Ask whether the user has excluded the test bin folder and
   TestFolder in Kaspersky (or has another machine); if yes, run
   pwsh scripts/large-file-check.ps1 -WorkFolder "D:\Abelrahman\Abdelrahman Current\TestFolder" in the background,
   fill in the 4.5 GiB row in LEARNING_LOG (append a short note, do not read the file), mark plan row 4.2 done.
2. Step 4.3: the user runs docs/TESTPLAN.md by hand and reports failures; fix each bug (test first where possible).
3. Step 4.4: the user runs docs/USABILITY.md with 3-5 people and reports findings; fix or record them.
4. Then the Stage 4 reviewer + LEARNING_LOG wrap-up, PR to main. REPLACE THIS FILE at the end of each step.
Do not raise the 400 MB memory limit to make a test pass; report real findings.
```

**Open items for the final hardening stage**
- Two flaky Core tests, both temp-file cleanup timing on Windows (see the project notes):
  `FileDecryptionTests.DecryptAsync_CancelledFromTheProgressReport_ThrowsAndLeavesNothing(length: 3145728)` and
  `FileDecryptionTests.DecryptAsync_WrongPasswordWithOverwriteAllowed_KeepsTheExistingFile`.
- Deferred review items: Unix errno values in `ErrorMessageProvider`, `IsSameFile` case rule on Linux, `FileErrorLog`
  tests, `DateTime` vs `Stopwatch` for the repeat-error window, About dialog width at large text scaling.
- T-23 (disk full) cannot be made with real files here; consider a fake-stream unit test.
