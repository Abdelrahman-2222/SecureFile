# Next step — copy-paste prompt for a new chat

This file is replaced at the end of every chat. It holds the prompt for starting the **next** piece of work in a
fresh chat, so a long conversation does not use up tokens.

Last finished: **Step 1.6** (`DecryptAsync`) and the process change (PROJECT_PLAN §1.7 "Cadence"), 2026-11-24.

```text
Do Step 1.7 and then the vertical slice of docs/PROJECT_PLAN.md (§7: Steps 2.1–2.3 and 2.6 in their simplest form,
then 3.1 with a plain window: pick file, password, Encrypt, Decrypt, message), on the existing branch
feature/stage-1-core. Don't create a new branch.

PROCESS (PROJECT_PLAN §1.7 "Cadence"): risk-based testing, no per-step reviewer, no Stryker, no per-step lesson.
- Core: full tests. ViewModels: a few tests each (main path, state changes, one error case), written with the code.
- WPF window: no unit tests; one end-to-end integration test (encrypt → decrypt a real file through the Core
  service) and a short manual smoke checklist that you run in the app.
- Follow §1.9/§1.10 silently; record only real decisions. Terse updates.
- One commit per step (Conventional Commits, no AI attribution); scripts/verify.ps1 must pass before every push.

Context (already done):
- Core: FileEncryptionService.EncryptAsync/DecryptAsync (verify-then-commit), AtomicFileWriter (isSensitive: hidden
  on Windows, 0600 on Linux/macOS, emptied on failure), SecureFileException, InvalidEncryptedFileException (Reason),
  AuthenticationFailedException (pulled forward into 1.6).
- Decrypt can also throw CryptographicException (valid tag but bad padding: foreign writer that knows the password)
  and EndOfStreamException (file shrank while read); both documented on IFileEncryptionService.

Step 1.7 (keep it small):
- Check test catalog §9 (T-05 to T-11, T-22, T-23) against the ADR-13 table (docs/adr/0013-exception-handling.md):
  every failure maps to one documented exception type. Add the missing ADR-13 rows (AuthenticationFailedException,
  CryptographicException after a valid tag, EndOfStreamException, the lone-surrogate ArgumentException that arrives
  only on await). Decide and record whether to wrap CryptographicException as InvalidEncryptedFileException(Damaged).
- Add tests only for real gaps.

Vertical slice: the simplest end-to-end app that encrypts and decrypts a real file. No UI polish.

At the end of the stage-1 slice:
- One reviewer agent (all roles from §1.8) over the whole slice; fix High/Medium or record a decision.
- One short LEARNING_LOG section for the slice (built, decisions, review table, gate table, ~5 self-check questions),
  plan rows, CHANGELOG.
- Replace docs/NEXT_STEP_PROMPT.md with the prompt for the next chunk of work, commit, push, confirm CI is green.
```
