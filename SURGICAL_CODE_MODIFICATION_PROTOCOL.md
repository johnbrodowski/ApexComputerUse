

## Surgical Code Modification Protocol

When working in this codebase, optimize for **minimum token usage and minimal file reads**.

### File Reading Rules

* **Do not open entire files by default.**
* First use search tools (`grep`, symbol search, references, definitions) to locate only the relevant functions, classes, or blocks.
* Read **only the smallest necessary section** surrounding the target code.
* Expand to larger sections **only if dependencies or surrounding logic require it**.
* Never re-read the same file region unless necessary.
* Never read more than **80 lines at a time** unless explicitly required for dependency resolution.

### Dependency Tracing Rules

Before making edits:

1. Identify the exact symbol(s) to modify.
2. Trace where they are called or referenced.
3. Determine whether the requested change is:

   * local to one function
   * local to one module
   * cross-module
4. Only inspect files directly involved in the execution path.

### Editing Rules

* Make **surgical edits only**.
* Modify only code directly involved in implementing the requested behavior.
* Change the **smallest viable code region**.
* Do not refactor unrelated code.
* Do not rewrite entire functions if only a few lines need changing.
* Preserve surrounding formatting and patterns.

**Scope Control Constraints**

* Introduce only the **minimum number of new variables, methods, or helper classes** required.
* Do not alter unrelated systems.
* Do not introduce new abstractions unless absolutely necessary to complete the task.
* Do not redesign existing patterns or architecture.

### Escalation Rules

Only widen scope if:

* the implementation depends on upstream context
* the symbol behavior is inherited or abstracted elsewhere
* the change affects interfaces/shared types
* local modification would break existing logic

If scope widens:

1. state why
2. identify the additional file(s) needed
3. inspect only the relevant sections

### Hard Stop Rule

* If the requested change **cannot be completed within a clearly minimal and localized scope**, **stop immediately**.
* Do not proceed with broad refactors, speculative fixes, or architectural changes.
* Instead, return:

  1. why the task exceeds surgical scope
  2. what additional areas would need modification
  3. a concise set of options for how to proceed

### Output Rules

Before editing, provide:

1. target symbol/file
2. expected scope of change
3. why that scope is sufficient

After editing, provide:

1. exact files changed
2. exact symbols changed
3. any downstream areas that may be affected

### Token Efficiency Priority

Prioritize:

1. symbol search
2. targeted reads
3. localized edits
4. minimal verification reads

Avoid:

* full-file reads
* repository-wide scans unless necessary
* unnecessary context loading
* speculative inspection of unrelated modules

Treat every additional file read as a cost that must be justified.
