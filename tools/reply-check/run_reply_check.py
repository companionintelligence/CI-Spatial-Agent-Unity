#!/usr/bin/env python3
"""Run the reply safety check's per-category test set against a live Ollama server.

Asks the safety model the app uses (IBM Granite Guardian, granite3-guardian:2b by default) about every
canned reply in cases.json, exactly as the app does (Assets/XRScan/Llm/GraniteGuardian.cs): one
/api/chat request per risk, with the risk name as the system message, the question as the user message
and the reply as the assistant message, temperature 0. A reply is blocked when any risk answers Yes and
passes only when every risk answers No; anything else is no clear answer, which the app treats as
"withhold". Replies marked "local" are blocked on the headset before the check
(Assets/XRScan/Core/LocalReplyRules.cs, kept true by the EditMode test ReplyCheckCasesTests); for them
the model's own answers are recorded too.

Every reply under "block" must be blocked and every reply under "pass" must pass. The results, with the
Ollama version and the model's digest, go to a JSON file to keep with the release records.

Usage:
  python3 tools/reply-check/run_reply_check.py [--url http://localhost:11434] [--model granite3-guardian:2b]
                                               [--out FILE|FOLDER] [--cafile CA.pem] [--timeout 60]

Exit status: 0 = every expectation met, 1 = at least one case failed, 2 = the server or the model
could not be used (nothing was judged).
Standard library only.
"""

import argparse
import datetime
import json
import os
import re
import ssl
import sys
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
SCORE = re.compile(r"<score>\s*(yes|no)\s*</score>", re.IGNORECASE)


def request(base, path, body, timeout, context):
    """POST (or GET when body is None) to the server; returns (status, parsed JSON or None, raw text)."""
    data = None if body is None else json.dumps(body).encode("utf-8")
    req = urllib.request.Request(base + path, data=data, method="GET" if body is None else "POST",
                                 headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout, context=context) as response:
            text = response.read().decode("utf-8", "replace")
            status = response.status
    except urllib.error.HTTPError as error:
        text = error.read().decode("utf-8", "replace")
        status = error.code
    try:
        parsed = json.loads(text)
    except ValueError:
        parsed = None
    return status, parsed, text


def read_answer(status, parsed):
    """'yes', 'no' or None (no clear answer), read the way GraniteGuardian.ReadAnswer reads it."""
    if status != 200 or not isinstance(parsed, dict) or parsed.get("error"):
        return None
    message = parsed.get("message")
    content = message.get("content") if isinstance(message, dict) else None
    if not isinstance(content, str) or not content.strip():
        return None
    answer = content.strip().lower()
    if answer in ("yes", "no"):
        return answer
    found = {m.lower() for m in SCORE.findall(content)}
    return found.pop() if len(found) == 1 else None


def check(base, model, risks, question, reply, timeout, context):
    """The model's answer per risk, and the verdict: 'blocked', 'safe' or 'unclear'."""
    answers = {}
    for risk in risks:
        body = {
            "model": model,
            "messages": [
                {"role": "system", "content": risk},
                {"role": "user", "content": question},
                {"role": "assistant", "content": reply},
            ],
            "stream": False,
            "options": {"temperature": 0.0},
        }
        try:
            status, parsed, raw = request(base, "/api/chat", body, timeout, context)
            answer = read_answer(status, parsed)
            answers[risk] = answer if answer else "unclear (HTTP %d: %s)" % (status, raw.strip()[:80])
        except (urllib.error.URLError, OSError) as error:
            answers[risk] = "unclear (%s)" % error
    values = list(answers.values())
    if "yes" in values:
        return answers, "blocked"
    if all(v == "no" for v in values):
        return answers, "safe"
    return answers, "unclear"


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--url", default="http://localhost:11434", help="the Ollama server (default %(default)s)")
    parser.add_argument("--model", help="the safety model (default: the one cases.json names, as the app uses)")
    parser.add_argument("--cases", default=os.path.join(HERE, "cases.json"), help="the test set (default: cases.json here)")
    parser.add_argument("--out", help="results file, or an existing folder to write reply-check-<model>-<UTC time>.json in "
                                      "(default: that file in the current folder); keep it outside this repository")
    parser.add_argument("--cafile", help="CA certificate(s) for an https:// server signed by your own CA")
    parser.add_argument("--timeout", type=float, default=60.0, help="seconds per request (default %(default)s)")
    args = parser.parse_args()
    if args.out and not os.path.isdir(args.out) and not os.path.isdir(os.path.dirname(os.path.abspath(args.out))):
        parser.error("--out: there is no folder %s" % os.path.dirname(os.path.abspath(args.out)))

    with open(args.cases, encoding="utf-8") as f:
        cases = json.load(f)
    model = args.model or cases["model"]
    risks = cases["risks"]
    base = args.url.rstrip("/")
    context = ssl.create_default_context(cafile=args.cafile) if args.cafile else None

    try:
        _, version, _ = request(base, "/api/version", None, args.timeout, context)
        _, tags, _ = request(base, "/api/tags", None, args.timeout, context)
    except (urllib.error.URLError, OSError) as error:
        print("Cannot reach the Ollama server at %s: %s" % (base, error), file=sys.stderr)
        return 2
    installed = {m.get("name"): m for m in (tags or {}).get("models", []) if isinstance(m, dict)}
    if model not in installed:
        print("%s is not installed on %s. Install it there first: ollama pull %s" % (model, base, model), file=sys.stderr)
        return 2

    started = datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0)
    results = []
    met = failed = 0
    for category in cases["categories"]:
        rows = []
        for expect in ("block", "pass"):
            for case in category[expect]:
                answers, verdict = check(base, model, risks, case["question"], case["reply"], args.timeout, context)
                local = bool(case.get("local"))
                outcome = "blocked" if local else verdict
                ok = outcome == ("blocked" if expect == "block" else "safe")
                met += ok
                failed += not ok
                rows.append({"expect": expect, "question": case["question"], "reply": case["reply"], "local": local,
                             "answers": answers, "model_verdict": verdict, "outcome": outcome, "met": ok})
                flagged = ", ".join(r for r, a in answers.items() if a == "yes") or "-"
                print("%-4s %-10s %-5s %-8s%s  flagged: %s | %s" % (
                    "ok" if ok else "FAIL", category["id"], expect, outcome, " (on-device)" if local else "",
                    flagged, case["reply"]))
        results.append({"id": category["id"], "name": category["name"], "sheets": category.get("sheets", ""),
                        "cases": rows})

    record = {
        "tool": "tools/reply-check/run_reply_check.py",
        "run_utc": started.isoformat(),
        "ollama_version": (version or {}).get("version"),
        "model": model,
        "model_digest": installed[model].get("digest"),
        "risks": risks,
        "request": "POST /api/chat, messages [system: risk, user: question, assistant: reply], stream false, temperature 0",
        "summary": {"cases": met + failed, "met": met, "failed": failed},
        "categories": results,
    }
    name = "reply-check-%s-%s.json" % (re.sub(r"[^A-Za-z0-9._-]", "_", model), started.strftime("%Y%m%dT%H%M%SZ"))
    out = os.path.join(args.out, name) if args.out and os.path.isdir(args.out) else (args.out or name)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(record, f, indent=2)
        f.write("\n")

    print("\n%d of %d cases as expected (%s, digest %s, Ollama %s). Results: %s" % (
        met, met + failed, model, record["model_digest"], record["ollama_version"], out))
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
