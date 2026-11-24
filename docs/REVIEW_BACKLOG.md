# Review backlog (product owner and tester pass, 2026-11-24)

An independent review of the app found 39 issues, test gaps and out-of-date documents. The ones that hurt trust or are
cheap were fixed in the same stage (see the CHANGELOG, "Review pass"). Everything below is **deferred on purpose** to the
final hardening phase (plan Stage 5/9) or to `SUGGESTIONS.md` (Stage 8). Numbers are the review's own.

## Fixed in Stage 4
1 Show password left the typed password on screen after success · 2 closing during an operation (now asks, cancels
cleanly) · 3 strength meter called repeats, runs and common words "Strong" · 4 no reason when Encrypt is disabled (the
confirm prompt) · 5 `.enc` file with a damaged start · 7 focus (start, failure, success) · 9 Alt+F clash (now Co_nfirm) ·
10 Cancel shown as a failure and logged as an error · 13 stale progress text · 14 Open folder failure replaced the success
message · 17 stale "choose Encrypt or Decrypt" wording · TESTPLAN rows that could no longer be done.

## Deferred: needs a decision or a bigger change
- **6 Wrong password on a huge file is reported only at the end** (verify-then-commit; no key-check value in the header).
  Options: a key-check value in the format (a version bump, so a deliberate decision), or a clear note before starting.
  Put in `SUGGESTIONS.md`.
- **34 / 6 No free-disk-space check** before encrypting or decrypting a large file.
- **2b No startup sweep** of leftover `.~*.securefile-tmp` files: they sit next to arbitrary targets, so the app cannot know
  where to look. A crash or power loss can still leave one; the file name pattern is documented in TESTPLAN H3.
- **35 Cancel during "Reading the file details…"** on a stalled network drive (the read is not cancellable).

## Deferred: polish and edge cases
- 8 Show password toggle moves the focus off the checkbox; paste of text with a newline differs between box types.
- 11 Enter in the first password box could move to the confirm box when it is shown.
- 12 A bad drop or Browse clears the previous good selection; a dropped folder says "can't be found or opened" (should say
  it is a folder); several dropped files silently use the first; dropping while busy shows a copy cursor; only the dashed
  zone is a drop target; no drag-over highlight.
- 15 Error dialogs do not say where the log file is (`FileErrorLog.LogFilePath` exists) and have no owner window.
- 16 File card: long names hide the extension and size; no tooltip, folder, date or exact bytes (plan §11.1 promised them).
- 18 The 8-character rule and the 1,024 limit count UTF-16 units (4 emoji pass; a long paste is cut silently).
- 19 Save dialog: "choose a different name" comes only after Windows asks about replacing; Decrypt of `x.enc` suggests an
  extensionless name; `.lnk` differs between Browse and drop; the Open dialog has no filter.
- 20 Old red message stays while the user retypes; cancelling the Save dialog leaves it.
- 21 File facts (size, encrypted or not) are read once when the file is chosen.
- 22 Progress text: estimate includes key derivation, "125 min", "0.0 MB/s", mixed culture formats, whole-percent steps.
- 23 Caps Lock warning may linger after Tab; not announced to screen readers.
- 24 Show password stays ticked for the next file; the visible box keeps an undo stack (set `UndoLimit=0`).
- 25 Success text does not say the unencrypted original is still on disk.
- 26 Window sizing: manual resize stops auto-height; small screens at 200% scaling; secondary monitors.
- 27 Accessibility: live-region announcements, strength bar value, Cancel not focused, high-contrast not checked.
- 28 Wording: USABILITY.md says "lock/unlock"; "Wrong password," is blunt.
- 29 The error log keeps full paths and a stack trace (documented in `FileErrorLog`).

## Suggestions
30 initial focus tweaks · 31 "Drop a file here or Browse…" and a highlight · 32 an always-visible reason line for every
disabled button · 33 file card details · 36 single-instance warning · 37 offer to delete or secure the original after
encryption · 38 clear the clipboard after a password-manager paste · 39 keep the Debug-only error hook out of the demo build.

## Test gaps still open
Window code-behind (password sync, Caps Lock, drop handler, closing) has no unit test and only the FlaUI smoke tests; a
FlaUI case with "Show password" ticked and one for close-during-operation are worth adding. No test for progress text above
one hour or with a comma decimal separator, Esc with no operation, a bad drop keeping the old selection, or the Save filter.

## Documents still out of line
PROJECT_PLAN §11.1 describes a mode switch, a "Change…" output button, a file-card tooltip and date, and "Drop a file here or
Browse…"; update the plan or build them. `FileErrorLog.LogFilePath` says the dialog shows the path (it does not).
