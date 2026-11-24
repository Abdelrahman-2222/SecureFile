# Next step — copy-paste prompt for a new chat

Last finished (2026-11-24): Stage 4 code and docs as far as a machine can take them, a review pass (backlog in
`docs/REVIEW_BACKLOG.md`), and the first friends build (`pwsh scripts/publish-beta.ps1`, output in `artifacts/beta/`).
Branch `feature/stage-4-testing`; a snapshot is on `release/v0.1.0-beta1` (tag `v0.1.0-beta1`, see docs/RELEASING.md).

**Model and effort for the next chat:** Sonnet 5.5, effort **medium**. Start a NEW chat: the previous one was very long.

```text
Branch feature/stage-4-testing (do not create another). Follow CLAUDE.md. Waiting on the user for:
1. docs/TESTPLAN.md run by hand (sections A-I): fix each bug found (test first where possible).
2. The friends beta (artifacts/beta/SecureFile-0.1.0-beta1-win-x64.zip + SHA-256): collect feedback into
   docs/USABILITY.md (Step 4.4) and fix or record each finding.
   Cross-platform command line: archives are in artifacts/cli (scripts/publish-cli.ps1); Linux and macOS binaries were built but
   not run here; ask the user for results (docs/CLI_README.md has the questions) and check the Ubuntu/macOS CI run.
3. T-27 4.5 GiB run: Kaspersky kills the test exe (see LEARNING_LOG, Stage 4); only 1 GiB is measured. Ask whether the
   user has excluded the test bin folder and TestFolder, then run scripts/large-file-check.ps1 in the background.
Then: Stage 4 reviewer + LEARNING_LOG wrap-up, PR to main, then Stage 5. REPLACE THIS FILE at the end of each step.
Do not raise the 400 MB memory limit to make a test pass; report real findings.
```

**Open items for the final hardening stage:** see `docs/REVIEW_BACKLOG.md` (deferred review items), the two flaky Core
tests (temp-file cleanup timing: `FileDecryptionTests.DecryptAsync_CancelledFromTheProgressReport_ThrowsAndLeavesNothing`
and `DecryptAsync_WrongPasswordWithOverwriteAllowed_KeepsTheExistingFile`; a third one failed once in the pre-push gate under load and passed 3 of 3 alone: `FileDecryptionTests.DecryptAsync_FileCutShortOrExtended_ThrowsAndLeavesNothing(lengthChange: 16)`), T-23 (disk full) as a fake-stream test.
