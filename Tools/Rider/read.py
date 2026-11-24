"""Read-only access to the local Rider MCP server, using only Python's standard library."""
import json
from pathlib import Path
import sys
import urllib.request

URL = "http://127.0.0.1:64342/stream"
PROJECT = str(Path(__file__).resolve().parents[2])
session = None
counter = 0


def request(method, params=None, notification=False):
    global session, counter
    counter += 1
    payload = {"jsonrpc": "2.0", "method": method}
    if not notification:
        payload["id"] = counter
    if params is not None:
        payload["params"] = params
    headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
    if session:
        headers["mcp-session-id"] = session
    req = urllib.request.Request(URL, json.dumps(payload).encode(), headers, method="POST")
    with urllib.request.urlopen(req, timeout=30) as response:
        session = response.headers.get("mcp-session-id", session)
        if notification or response.status == 202:
            return {}
        if "event-stream" in response.headers.get("Content-Type", ""):
            for line in response:
                if line.startswith(b"data:"):
                    value = json.loads(line[5:])
                    if value.get("id") == counter:
                        break
            else:
                raise RuntimeError("Rider closed the stream without a response")
        else:
            value = json.load(response)
    if "error" in value:
        raise RuntimeError(value["error"])
    return value["result"]


def read_tool(name, **arguments):
    # Keep terminal execution, refactorings and edits unavailable through this helper.
    if name not in {"read_file", "search_text", "get_file_problems"}:
        raise ValueError("Unsupported read-only tool")
    result = request("tools/call", {"name": name, "arguments": {"projectPath": PROJECT, **arguments}})
    if result.get("isError"):
        raise RuntimeError(result.get("content"))
    structured = result.get("structuredContent")
    if structured is not None:
        print(json.dumps(structured, ensure_ascii=False), flush=True)
    else:
        for item in result.get("content", []):
            if item.get("type") == "text":
                print(item["text"], flush=True)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) < 2:
        raise SystemExit("Usage: read.py list | schema NAME... | read FILE [START COUNT] | problems FILE... | search TEXT [GLOB]")
    request("initialize", {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "BuroCrazy-read-only", "version": "1.0"}})
    request("notifications/initialized", notification=True)
    mode = sys.argv[1]
    if mode in {"list", "schema"}:
        entries = request("tools/list")["tools"]
        print(json.dumps([entry["name"] for entry in entries] if mode == "list" else
                         [entry for entry in entries if entry["name"] in sys.argv[2:]], ensure_ascii=False))
    elif mode == "read":
        read_tool("read_file", file_path=sys.argv[2], start_line=int(sys.argv[3]) if len(sys.argv) > 3 else 1,
                  max_lines=int(sys.argv[4]) if len(sys.argv) > 4 else 80)
    elif mode == "problems":
        for path in sys.argv[2:]:
            read_tool("get_file_problems", filePath=path, timeout=12000)
    elif mode == "search":
        read_tool("search_text", q=sys.argv[2], paths=sys.argv[3:] or ["Assets/Scripts/**"], limit=20)
    else:
        raise SystemExit("Unknown read-only command")


if __name__ == "__main__":
    main()
