# Reply safety check: test set and live run

Every reply of the language model passes a check before the companion speaks it or the app shows it. The check fails
closed: only a clear "safe" answer releases a reply. This is the reply check that the store sheets require before any
submission (Google Play gate 11, Meta Horizon gate 12, both in the shared code). This folder holds its per-category test
set and the script that runs that set against a live Ollama server.

| File | What it is |
|---|---|
| `cases.json` | Short canned replies per category: replies that must be blocked and replies that must pass |
| `run_reply_check.py` | Asks the safety model about every reply in `cases.json` the way the app asks, and writes a results file (standard-library Python) |

## What the app does

`ConversationOrchestrator` sends every reply of the language-model backend, whatever request produced it, through
`ReplyGate` (`Assets/XRScan/Core`) before anything else sees it:

1. **On-device rules** (`LocalReplyRules`), with no server: a word for a child or teenager together with a sexual word,
   and claims to be human ("I'm a real person", "I'm not an AI"). These are the two kinds of content Play's AI-generated
   content policy names (content that facilitates the exploitation or abuse of children, and deceptive behaviour), for
   which the Play sheet asks for a short on-device list whatever else checks replies.
2. **The reply check** of the language-model backend (`IReplyChecker`). For `OllamaBackend` that is IBM Granite Guardian
   on the same Ollama server as the language model, so the question and the reply go to no new recipient.

| Outcome | What the companion does |
|---|---|
| Every risk answers No | Speaks the reply; it becomes the reply the user can report |
| Any risk answers Yes, or an on-device rule matches | Withholds the reply and says `ReplySafety.BlockedLine` |
| No clear answer: an error, the timeout, a missing model, an empty or unreadable answer, an exception, or a backend without a check | Withholds the reply, says `ReplySafety.UnavailableLine` ("... Open Settings to see why"), and shows a notice. The settings panel's **Reply safety check** row names the model and why it did not answer, for example `granite3-guardian:2b is not installed on the LLM server. Install it there: ollama pull granite3-guardian:2b` |

A withheld reply is never spoken, shown, logged or kept. Pin labels from the vision server, the other model text the app
speaks and shows, are kept only when every word is a word of the detection prompt the app sent
(`ScanController.CheckedLabel`); any other label becomes "object".

Settings, in `LlmConfig`: `safetyModel` (default `granite3-guardian:2b`; blank means the default; the environment
variable `XRSCAN_SAFETY_MODEL` overrides it on desktop) and `safetyCheckTimeoutSeconds` (default 20, clamped to 1 to
120), the time all requests for one reply may take together. Nothing turns the check off: the risk list is code, the
settings panel only shows the check, and the mock backend that passes its own fixed replies is a development-scene
component that `ReleaseMockGuard` keeps out of release builds.

## The safety model

Facts below were read on 2026-11-13.

- **Name and tag:** `granite3-guardian:2b`, IBM Granite Guardian 3.0 2B, the smallest Granite Guardian in the Ollama
  library. Tags: `2b` (also `latest`, 2.7 GB, 8K context) and `8b` (5.8 GB)
  ([ollama.com/library/granite3-guardian](https://ollama.com/library/granite3-guardian)). The newer
  `granite4.1-guardian` comes in 8B only (6.9 GB, [ollama.com/library/granite4.1-guardian](https://ollama.com/library/granite4.1-guardian)).
- **Licence:** Apache 2.0, on the Ollama page and in the model's licence layer, and `apache-2.0` on IBM's model card
  ([huggingface.co/ibm-granite/granite-guardian-3.0-2b](https://huggingface.co/ibm-granite/granite-guardian-3.0-2b)).
- **How to ask about an assistant reply:** the Ollama page selects the risk through the system prompt. The model's
  Ollama template ([template layer](https://ollama.com/library/granite3-guardian:2b/blobs/25a6bf4e64bf)) takes the
  last message as the one to judge and the message before it as context, so a request with a user message (the question)
  and an assistant message (the reply) judges the reply. IBM's model card does the same with user and assistant messages
  and a `risk_name`. The app sends one `POST /api/chat` per risk:

  ```json
  {"model":"granite3-guardian:2b","messages":[{"role":"system","content":"violence"},
   {"role":"user","content":"<question>"},{"role":"assistant","content":"<reply>"}],
   "stream":false,"options":{"temperature":0.0}}
  ```

  The model's parameter layer sets temperature 0 as well. The request carries the risk name, the user's question (their
  own words, without the scanned-scene context) and the reply, nothing else.
- **Risk categories** (model card and Ollama page): `harm` (the general category), `social_bias`, `jailbreak`,
  `violence`, `profanity`, `sexual_content`, `unethical_behavior`, and three RAG risks (context relevance, groundedness,
  answer relevance). The app
  checks every reply for `harm`, `sexual_content`, `violence`, `social_bias`, `unethical_behavior` and `profanity`
  (`GraniteGuardian.Risks`); `jailbreak` judges prompts and the RAG risks need retrieved context.
- **Output:** a single token, `Yes` (the risk is present) or `No`. Granite Guardian 3.3 and 4.1 answer in
  `<score> yes </score>` tags instead; the app reads those too, and anything else is no clear answer.
- **Limits** (model card): trained and tested on English only, to be used only in its yes/no scoring mode, and it can
  be wrong or fooled. It is an automatic check: no person reviews a reply before it is spoken, so the content-rating
  answer on AI-generated content stays Yes on both stores.

| Category in `cases.json` | Sheets | Risks expected to flag it |
|---|---|---|
| `sexual` | Play gate 11 (sexual content), Meta gate 12 (pornography) | `sexual_content`, `harm` |
| `minors` | Play gate 11 and the AI policy (exploitation or abuse of children) | on-device rule, `sexual_content`, `harm` |
| `hate` | Play gate 11 (hate), Meta gate 12 (hateful behaviour, harmful stereotypes) | `social_bias`, `harm` |
| `harassment` | Play gate 11 (harassment), Play AI policy (bullying) | `profanity`, `harm` |
| `violence` | Play gate 11 (violence, weapons), Meta gate 12 (promotion of violence) | `violence`, `harm` |
| `self-harm` | Play gate 11 (self-harm), Play AI policy (harmful behaviour) | `harm`, `violence` |
| `crime` | Play gate 11 (crime) | `unethical_behavior`, `harm` |
| `deception` | Play gate 11 (deception, impersonation), Play AI policy (deceptive behaviour) | on-device rule, `unethical_behavior`, `harm` |

## Running it (owner step)

The check needs the safety model on the language model's Ollama server, and the test set needs that server running. Both
are done on the owner's server; nothing here downloads a model by itself.

1. On the machine that runs Ollama for the app:

   ```
   ollama pull granite3-guardian:2b
   ollama list
   ```

   `ollama list` must show `granite3-guardian:2b` (pulling only `granite3-guardian` gives `granite3-guardian:latest`,
   which the app does not ask for). The language model and the safety model are then both used for each reply; if the
   server's memory holds only one of them at a time, raise `OLLAMA_MAX_LOADED_MODELS` or expect slower checks.

2. From the root of a checkout of this repository, on a machine that reaches that server, with `--out` naming the
   folder where you keep the release records, outside this checkout:

   ```
   python3 tools/reply-check/run_reply_check.py --url http://localhost:11434 --out <release-records folder>
   ```

   For the server behind the HTTPS proxy the headset uses, signed by your own certificate authority:

   ```
   python3 tools/reply-check/run_reply_check.py --url https://<llm-server> --cafile <your-ca>.pem --out <release-records folder>
   ```

   Each reply prints one line: `ok` or `FAIL`, the category, what was expected, the outcome and the risks that answered
   Yes. Exit status 0 means every reply came out as expected, 1 that at least one did not, 2 that the server could not
   be reached or does not have the model.

3. Keep the results file it writes in that folder (`reply-check-granite3-guardian_2b-<UTC time>.json`: every answer per
   risk, the Ollama version and the model's digest) with the release records, not in this repository. Without `--out` the
   file lands in the current folder, where git ignores it (`.gitignore`); move it to the release records. Google Play
   says it may ask to see such test results. Run it again after changing the safety model, the risk list or
   `cases.json`, and after upgrading Ollama.

A failed case is a decision for the owner: add a risk, raise the model to `granite3-guardian:8b` (`--model`, then the
same value in `LlmConfig.safetyModel`), or widen the on-device rules, and run the set again.

The Play sheet's gate 11 also asks for prompts per category sent to at least two chat models, one of them uncensored,
with the safety prompt and this check in place, and the results kept. That run is still to do; it needs the chat models
on the owner's server.

## Changing the test set

Keep each reply short (at most 120 characters) and free of instructions. Mark a reply `"local": true` exactly when
`LocalReplyRules` blocks it; the EditMode test `ReplyCheckCasesTests` fails otherwise, and also when a category the
sheets name is missing, has fewer than three replies of either kind, or when the model or the risks differ from the
app's.
