"""Read-only Yonote API client. Never print credentials or server error bodies."""

import argparse
import json
import sys
from pathlib import Path
from urllib import error, request


WORKSPACE = "https://mindeaterteaceremony.yonote.ru"
SETTINGS = Path.home() / ".config" / "yonote" / "settings.json"
READ_METHODS = frozenset({
    "documents.info", "documents.list", "documents.search",
    "collections.info", "collections.list",
})


class NoRedirect(request.HTTPRedirectHandler):
    # Redirects could forward the Authorization header outside our workspace.
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def read_api(method, payload, settings):
    if method not in READ_METHODS:
        raise ValueError("Only explicitly allowed read operations are supported.")
    if settings.get("workspace_url") != WORKSPACE:
        raise ValueError("Workspace must be https://mindeaterteaceremony.yonote.ru")
    token = settings.get("api_token", "").strip()
    if not token:
        raise ValueError(f"Fill api_token in {SETTINGS}")
    req = request.Request(
        f"{WORKSPACE}/api/{method}",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json"},
        method="POST",  # Yonote uses POST even for read-only RPC methods.
    )
    with request.build_opener(NoRedirect()).open(req, timeout=30) as response:
        result = json.load(response)
    if isinstance(result, dict) and result.get("ok") is False:
        raise ValueError("Yonote rejected the read request. Check token permissions.")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("method", nargs="?", default="documents.info", choices=sorted(READ_METHODS))
    parser.add_argument("--id", help="Document or collection ID")
    parser.add_argument("--query", help="Text for documents.search")
    parser.add_argument("--collection", help="Limit documents.list to a collection")
    parser.add_argument("--type", choices=["document", "database", "row"], help="Document type filter")
    parser.add_argument("--limit", type=int, default=25)
    parser.add_argument("--offset", type=int, default=0)
    args = parser.parse_args()
    try:
        settings = json.loads(SETTINGS.read_text(encoding="utf-8-sig"))
        payload = {}
        if args.method.endswith(".info"):
            payload["id"] = args.id or (settings.get("document_id") if args.method == "documents.info" else None)
            if not payload["id"]:
                raise ValueError("This operation requires --id.")
        else:
            if not 1 <= args.limit <= 100 or args.offset < 0:
                raise ValueError("Use limit 1..100 and offset >= 0.")
            payload.update(limit=args.limit, offset=args.offset)
        if args.method == "documents.search":
            if not args.query:
                raise ValueError("documents.search requires --query.")
            payload["query"] = args.query
        if args.collection:
            payload["collectionId"] = args.collection
        if args.type:
            payload["type"] = [args.type]
        result = read_api(args.method, payload, settings)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except error.HTTPError as exc:
        print(f"Yonote HTTP {exc.code}: check token, permissions, and document ID.", file=sys.stderr)
    except error.URLError:
        print("Cannot connect to Yonote. Check network access.", file=sys.stderr)
    except (OSError, ValueError) as exc:
        # JSON parsing errors do not echo the input (which contains the token).
        message = "Invalid settings or server JSON." if isinstance(exc, json.JSONDecodeError) else str(exc)
        print(message, file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    sys.exit(main())
