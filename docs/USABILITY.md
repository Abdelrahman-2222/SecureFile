# SecureFile — usability check (Step 4.4, T-45)

Goal: find out whether people who are not technical can encrypt and decrypt a file **unaided**, how long the first
encryption takes, and where they hesitate. 3–5 participants, about 15 minutes each. The observer writes; the
participant is never helped (see "Rules").

## Setup

- Release build on a normal Windows laptop, window already open, a folder with `letter.txt` and `family-photo.jpg`.
- Screen recording or notes only with the participant's OK; no personal data is stored (use P1, P2, P3…).
- A stopwatch. A printout of the tasks. The SUS form below.

## Rules for the observer

1. Read the intro aloud, then stay silent. Do not point, hint, or explain a control.
2. Ask the participant to think aloud ("say what you are looking at and what you expect").
3. If they are stuck for 2 minutes, write "gave up", note the step, and move on (this counts as a failure).
4. Never defend the design. Thank them.

**Intro (read aloud):** "This program locks a file with a password so nobody else can open it, and unlocks it again.
We are testing the program, not you. There are no wrong answers. Please say your thoughts aloud."

## Tasks

| # | Task given to the participant | Success means |
|---|---|---|
| U1 | "Lock `letter.txt` with a password you choose." | A `.enc` file exists; time from first look to success is noted (**time to first encryption**). |
| U2 | "Now open the locked file again, to check it still reads the same." | Restored file opens and matches. |
| U3 | "Try to unlock it with a wrong password. What happens?" | Participant can say in their own words what went wrong and what to do next. |
| U4 | "Find out what version of the program this is." (no hint) | About dialog found. |
| U5 | "Lock `family-photo.jpg`, then find where the new file was saved." | Participant locates the file (Open folder or Explorer). |

## Observer sheet (one per participant)

Participant ____ · date ____ · age group ____ · uses computer: rarely / daily · used encryption before: yes / no

| Task | Success (unaided) | Time (s) | Errors / hesitations | Quote or remark |
|---|---|---|---|---|
| U1 | yes / no / gave up | | | |
| U2 | yes / no / gave up | | | |
| U3 | yes / no / gave up | | | |
| U4 | yes / no / gave up | | | |
| U5 | yes / no / gave up | | | |

Did the participant understand that the password cannot be recovered? yes / no. Did they write it down or choose a
password they can remember? ____

## SUS questionnaire (optional, ask after the tasks)

Participant rates each statement from 1 (strongly disagree) to 5 (strongly agree).

1. I think I would like to use this program frequently.
2. I found the program unnecessarily complex.
3. I thought the program was easy to use.
4. I think I would need help from a technical person to use this program.
5. I found the various functions well integrated.
6. I thought there was too much inconsistency in this program.
7. I imagine most people would learn to use this program very quickly.
8. I found the program very cumbersome to use.
9. I felt very confident using the program.
10. I needed to learn a lot of things before I could get going.

**Score:** for odd items take (rating − 1); for even items take (5 − rating); add the ten values and multiply by 2.5
(0–100). Around 68 is average; above 80 is good.

## Results summary (fill in after the sessions)

| Measure | Result | Target |
|---|---|---|
| Participants | | 3–5 |
| U1–U5 completed unaided | __ / __ | all |
| Median time to first encryption | | under 2 minutes |
| Mean SUS | | 68 or more |

| Finding | Seen by (n) | Severity | Fix or decision |
|---|---|---|---|
| | | | |

Step 4.4 is done when all participants complete U1–U2 unaided and every finding is fixed or recorded here with a
reason.
