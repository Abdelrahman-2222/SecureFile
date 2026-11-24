# Next step — copy-paste prompt for a new chat

Last finished (2026-11-24): Step 3.7 automated part (FlaUI UI tests). Branch `feature/stage-3-wpf-ui` (cut from `main`).

**Model and effort for the next chat:** Sonnet 5.5, effort **medium** (docs + review + PR). The reviewer agent runs on Sonnet.

**Needs the user first (cannot be automated by Claude):**
1. Run **Accessibility Insights for Windows → FastPass** against SecureFile.exe (Debug build is fine); expected: 0 failures.
   Tell Claude the result (or paste failures) so Step 3.7 can be marked done.
2. Check by hand and report: dark mode (switch Windows theme), 150%/200% scaling, a real drag-and-drop from Explorer,
   the "Open folder" click, Narrator reads every control (T-41, T-43).

```text
Branch feature/stage-3-wpf-ui (do not create another). Follow CLAUDE.md. Stage 3 wrap-up: (1) apply the user's
FastPass / manual-check results (fix real failures, mark plan row 3.7 done only when FastPass reports 0 failures);
(2) append one short "Stage 3" section to docs/LEARNING_LOG.md (built, decisions, review table, gate table, 5 questions)
without reading the file; (3) one reviewer agent (Sonnet, all §1.8 roles, read-only, diff main..HEAD, report <= 30 lines),
fix High/Medium; (4) push, open a PR into main (no AI attribution), confirm CI green with `gh pr checks`.
UI tests: run locally with SECUREFILE_UI_TESTS=1 dotnet test --project tests/SecureFile.Wpf.UiTests -c Release
(they take over the keyboard for ~30 s; they are skipped in the gate and CI).
Replace this file at the end.
```

**Open items**
- One unexplained flaky test failure in the pre-push run after Step 3.5 (passed on re-run; name not captured): hunt it in
  the final hardening stage.
- Deferred review items: Unix errno values in `ErrorMessageProvider`, `IsSameFile` case rule on Linux, `FileErrorLog`
  tests, `DateTime` vs `Stopwatch` for the repeat-error window.
- GitGuardian flags the public RFC 7914 test vector in `KeyDerivationTests.cs` (false positive).
