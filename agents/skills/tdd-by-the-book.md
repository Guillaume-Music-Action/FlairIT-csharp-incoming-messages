# Skill: tdd-by-the-book

## Use when

- the user asks to implement something "using TDD"
- writing a new domain type, method, or behavior test-first
- any task explicitly framed as red/green/refactor

## Goal

Follow the TDD loop exactly, one behavior at a time, so every line of production
code exists because a failing test demanded it — never ahead of it.

## The loop

1. **Name the rule** — before writing any TypeScript, state the single behavior
   being tested next as one plain-English sentence in the domain's `*.spec.md`
   (or equivalent doc). If the rule already exists there, point to it instead of
   restating it. This sentence is what the next test's name/assertions must match.
2. **Red** — write **one** small test for that plain-English rule. Run only that
   test file, e.g. `pnpm test -- <path/to>.spec.ts`. Confirm it fails, and
   confirm *why*: a missing module/class/method, not a typo or a broken test
   harness.
3. **Green** — write the **minimum** production code to make that one test pass.
   Do not implement invariants, branches, or parameters no test has asked for
   yet, even if the spec/doc mentions them. "Fake it" (e.g. return a constant,
   skip validation) is an acceptable and often correct green step. Run only the
   targeted test file to confirm.
4. **Refactor** — with the test green, **immediately tackle code smells and
   technical debt** (duplication, primitive obsession, magic strings, ignored
   results, inconsistent errors, god methods, missing helpers). Clean up
   duplication or naming in either the test or the production code. Re-run the
   targeted test file to confirm still green. No behavior change. No new test added.
5. **Verify** — run mutation testing scoped to only the source file just
   touched, e.g. `pnpm exec stryker run --mutate '<path/to/source>.ts'`. A
   surviving mutant means the code contains a branch, condition, or literal no
   test pins down. Report the surviving mutant(s) and **stop** — do not add a test to kill it without discussing the fix
   first : it may call for removing untested code, or for adding a new test, but that may also need to discuss a plain
   english specification in `*.spec.md` or require adding a new one. Do not proceed to the
   next test while mutants survive.
6. **Commit** — once the cycle is green with zero surviving mutants, commit
   that one behavior on its own before starting the next test. Small commits
   that each correspond to one passing test, not a batch of several.
7. Repeat: pick the next smallest untested behavior and go back to step 1.

## Property-based tests

Add property-based tests (`fast-check`) alongside the example-based ones for behaviors defined over a range of inputs
rather than discrete cases. If fixing a failing property means loosening, narrowing, or bounding what it checks, update
`*.spec.md` with a matching rule in the same change — a property's assertion is part of the spec, same as an example
test's.

## Do

- write the plain-English rule in the spec markdown before writing the
  corresponding TypeScript test — never the reverse
- write only one failing test before writing any implementation
- run the test and observe the actual failure before writing a line of production code
- treat "it compiles/passes on the first try" as a signal something is wrong —
  either the test isn't asserting the right thing, or code was written ahead of it
- let each subsequent test drive one new invariant, branch, or edge case
  (e.g. reject negative amount, reject bad currency, rounding) — not all at once
- keep steps small enough that the diff for "green" is obvious and minimal
- narrate the cycle explicitly: which step (red/green/refactor/verify) is happening now
- run tests and mutation testing scoped to only the targeted test/source file for
  each cycle, not the whole suite — a full-suite run is for a final sanity check,
  not for driving the loop
- run mutation testing after each green step to prove no untested code was written
- **refactor immediately after Green** — eliminate duplication, primitive obsession,
  magic strings, ignored results, inconsistent errors, god methods, missing helpers
  before writing the next test
- commit as soon as a test goes green with zero surviving mutants, before moving
  to the next test
- when a mutant survives, report it and propose a fix, then wait for confirmation
  before writing the killing test or removing the untested code
- when a property-based test's assertion needs to be loosened, narrowed, or bounded to pass, update `*.spec.md` with a
  matching rule in the same change

## Avoid

- writing a full implementation (all invariants from a spec/doc) after a single test
- writing the test and the implementation in the same turn without running red first
- adding validation, error cases, or methods that no test currently requires
- treating a design document's list of invariants as a checklist to implement
  upfront instead of as a backlog of future failing tests
- skipping the "confirm it fails" step
- adding a test to kill a surviving mutant without first discussing it with the user
- fixing a failing property-based test by only editing the `*.ts` test file without updating `*.spec.md`

## Deliverables

- one new failing test, run and confirmed red, before any implementation
- minimal implementation making only that test pass, run and confirmed green
- a mutation testing run on the touched file(s) with no surviving mutants
- an explicit next-smallest-test proposed for the following cycle

## Done when

- every production line traces back to a test that failed before it existed
- no untested invariant or branch exists in the code
- mutation testing confirms zero surviving mutants on the code just written
- the test suite reads as an incremental log of the behaviors built
