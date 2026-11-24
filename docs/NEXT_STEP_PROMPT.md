# Next step — copy-paste prompt for a new chat

Last finished (2026-11-24): the vertical slice (Steps 2.1–2.6, 3.1). Branch `feature/stage-1-core`.

**Model and effort for the next chat:** Sonnet 5.5, effort **medium** for Step 3.2 (layout from the §11 spec). Use Opus 5.5
only if the theme/accessibility steps (3.3, 3.7) need design judgement.

```text
Branch feature/stage-1-core (don't create a branch). Follow CLAUDE.md. Step 3.2: main window layout per PROJECT_PLAN §11
(grep "^## 11" or "§11" and read only that range): drop zone, file card, password, output, actions, progress, status.
Keep AutomationIds from src/SecureFile.Wpf/MainWindow.xaml and the code-behind PasswordChanged. Known deferred items from
the slice review (docs/LEARNING_LOG.md, "Vertical slice" table): progress bar indeterminate while browsing; Cancel
(Step 3.5); IsSameFile case rule on Linux; FileErrorLog tests.
Per step: build + tests, one commit, then smoke checklist (UI Automation). Replace this file at the end.
```
