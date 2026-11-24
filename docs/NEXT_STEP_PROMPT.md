# Next step — copy-paste prompt for a new chat

Last finished (2026-11-24): Step 3.6 (app icon, About with version, F1). Stage 3.2–3.5 was merged to `main` in PR #1.
Branch `feature/stage-3-wpf-ui` (cut from `main`; keep using it for Stage 3, open a PR when 3.7 is done).

**Model and effort for the next chat:** Sonnet 5.5, effort **medium**; Opus 5.5 only if the FlaUI or Accessibility
Insights setup gets stuck.

```text
Branch feature/stage-3-wpf-ui (do not create another). Follow CLAUDE.md. Step 3.7: FlaUI smoke test + Accessibility
Insights FastPass (grep "^| 3.7" docs/PROJECT_PLAN.md for the row; expected result: 0 accessibility failures).
Put the FlaUI test in its own test project only if the plan names one (grep "FlaUI" docs/PROJECT_PLAN.md); it must
not run in the pre-push gate if it needs a desktop session. The manual UI Automation PowerShell scripts used in Steps
3.1-3.6 (encrypt, decrypt, wrong password, Save cancel, forced error, Cancel on a big file, F1 About) are the checklist
to automate. Also verify by hand and record: dark mode, 150%/200% scaling, real drag-and-drop, Open folder click.
Per step: build + tests, one commit, mark the plan row, CHANGELOG line, and REPLACE THIS FILE at the end. After 3.7:
LEARNING_LOG section for Stage 3, one reviewer agent over the Stage 3 diff, then open a PR.
```

**Open items**
- One unexplained flaky test failure in the pre-push run after Step 3.5 (passed on re-run; name not captured):
  hunt it in the final hardening stage.
- Deferred review items: Unix errno values in `ErrorMessageProvider`, `IsSameFile` case rule on Linux, `FileErrorLog`
  tests, `DateTime` vs `Stopwatch` for the repeat-error window.
- GitGuardian flags the public RFC 7914 test vector in `KeyDerivationTests.cs`; marked as false positive in its dashboard.
