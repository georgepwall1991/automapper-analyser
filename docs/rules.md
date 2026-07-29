# Rule reference has moved

The full rule reference lives in **[DIAGNOSTIC_RULES.md](DIAGNOSTIC_RULES.md)**.

This file previously held a second copy of that reference. The two drifted: this copy stopped at 21
rules and never gained AM060 or AM061, while nothing linked to it and no test validated it, so the drift
was invisible until someone compared them by hand. Diagnostic help links, the rule catalog, and the
trust tests all point at `DIAGNOSTIC_RULES.md`, which is the one that is checked.

It is kept as a stub rather than deleted so existing links to this path still lead somewhere useful.
