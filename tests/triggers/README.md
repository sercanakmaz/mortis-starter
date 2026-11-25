# Trigger tests

Each skill's description against should-trigger and should-not-trigger prompts (phase 2 step 07).

- `cases.json`: 80 prompts, 15 per skill and 20 near misses, each with what the working folder holds.
- `cases-hard.json`: 40 prompts with no folder context, phrased the way people start, and near misses that share words with the descriptions (a web API, CQRS, a pull request in another stack).
- `prompt.txt`, `prompt-hard.txt`: the question a model answers: the four descriptions among eight distractor skills (`listing.txt`), pick one skill or none per prompt.
- `run-*.json`: one run per model size (haiku, sonnet, opus); `score.py cases.json run.json ...` prints, per skill, the share triggered and the share of other prompts that chose it.

Result (2026-11-25): every skill triggered on every should-trigger prompt of both sets with all three models; one false trigger in 360 should-not answers (sonnet chose `mortis-develop` for "add a consumer that reacts when stock drops below 5" with no project context). The bar is at least 90 % and at most 10 %.

This is a proxy: the model is shown the menu and asked to choose, where a real session also decides whether to use a skill at all. The live measurement, `claude -p` with the skills installed (the skill-creator's `run_eval.py`), needs a signed-in Claude Code; run it before a release when one is at hand. Descriptions were not changed: nothing failed.
