# Verification discipline

Read this when verifying a phase, or before reporting any feature as complete.
Use these checks when they apply to the feature's actual risk and contracts.

## 1. A green gate is necessary, not sufficient

A green required gate proves that its commands and assertions passed. It cannot
prove that the assertions are the right ones or that an untested path works.
Compare the result with the approved requirements in `docs/requirements/` and the
canonical contracts in `contracts/`.

## 2. Do not take a subagent's word for it

Every subagent report is a claim, not evidence. Before reporting a phase as
done, open the files it says it changed and confirm the change is there and is
what the task asked for. A subagent that reports success without writing
anything is common and silent.

## 3. Every bug-catching test needs a negative control

A test that has never been seen to fail proves nothing — it may assert nothing.

For any test written to catch a specific bug:

1. Break the code the way the bug happened.
2. Confirm the test goes RED, and fails for the right reason.
3. Restore the code.
4. Confirm the test goes GREEN.

Report both colours. "Test passes" without the red half is an incomplete claim.

## 4. Require one real end-to-end path per feature

When the feature changes an integration path, exercise the actual journey in an
authorized environment: a real request, a real event on the real bus, and the
relevant persisted state. Assert expected values, not merely that no error was
returned. If the environment is unavailable, report the feature as not
end-to-end verified.

## 5. When two documents disagree, stop

`spec.md` vs `plan.md`, or either vs `constitution.md`. Report the conflict
naming both sides and ask. Do not pick a winner, and do not quietly align the
loser to the winner — that erases a decision the developer may not have made.

## 6. Check producer/consumer ordering when relevant

When event ordering, staleness, or replay semantics are in scope, assert the
producer timestamp and consumer comparison against the canonical event contract.
Use distinct clocks where needed to prove the intended ordering; do not infer a
universal timestamp policy from this reference.

## 7. Accidental side effects on real data

Before running a sweep, migration or backfill, confirm its date window cannot
reach pre-existing production rows.

A destructive or externally visible path is only as safe as its target and data
window. Check both before running, not after, and obtain permission before any
live-data or production action.

## 8. One real path, then stop

Run the selected end-to-end path once per feature near close-out and assert
concrete values. Re-run only if relevant inputs changed after the first run.
