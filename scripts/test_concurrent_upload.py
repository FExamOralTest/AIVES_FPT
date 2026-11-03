"""Test upload đồng thời (concurrent) lên R2 qua API — tái hiện lỗi signature mismatch."""
import json
import sys
import threading
import urllib.request
import uuid

BASE = "http://localhost:5000"
LOGIN = "demo-student@aives.local"
PWD = "Demo@12345"


def post_json(url, body, token=None):
    data = json.dumps(body).encode()
    req = urllib.request.Request(url, data=data, method="POST")
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"HTTP {e.code} @ {url.split('/')[-1]}: {e.read().decode()[:300]}") from None


def post_multipart(url, fields, token):
    boundary = "----aives" + uuid.uuid4().hex
    body = b""
    for k, v in fields.items():
        if isinstance(v, (bytes, bytearray)):
            body += f"--{boundary}\r\n".encode()
            body += f'Content-Disposition: form-data; name="{k}"; filename="chunk.bin"\r\n'.encode()
            body += b"Content-Type: application/octet-stream\r\n\r\n"
            body += bytes(v) + b"\r\n"
        else:
            body += f"--{boundary}\r\n".encode()
            body += f'Content-Disposition: form-data; name="{k}"\r\n\r\n'.encode()
            body += str(v).encode() + b"\r\n"
    body += f"--{boundary}--\r\n".encode()
    req = urllib.request.Request(url, data=body, method="POST")
    req.add_header("Content-Type", f"multipart/form-data; boundary={boundary}")
    req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"HTTP {e.code}: {e.read().decode()[:300]}") from None


def main():
    token = post_json(f"{BASE}/api/auth/login", {"email": LOGIN, "password": PWD})["accessToken"]
    payload = b"\x1a\x45\xdf\xa3" + bytes(range(256)) * 400  # ~100KB

    session_id = int(sys.argv[1]) if len(sys.argv) > 1 else 54
    n = int(sys.argv[2]) if len(sys.argv) > 2 else 3

    results = {}

    def worker(i):
        try:
            uid = f"conc{i}-{uuid.uuid4().hex[:8]}"
            init = post_json(
                f"{BASE}/api/audio/chunked/init",
                {
                    "uploadId": uid,
                    "sessionId": session_id,
                    "questionId": 35,
                    "fileName": "answer.webm",
                    "contentType": "audio/webm",
                    "totalSize": len(payload),
                    "chunkSize": len(payload),
                    "totalChunks": 1,
                },
                token,
            )
            post_multipart(
                f"{BASE}/api/audio/chunked/chunk",
                {"uploadId": init["uploadId"], "chunkIndex": 0, "chunkData": payload},
                token,
            )
            done = post_json(
                f"{BASE}/api/audio/chunked/complete", {"uploadId": init["uploadId"]}, token
            )
            results[i] = "OK" if not done.get("storageFallback") else f"FALLBACK: {done.get('storageError')}"
        except Exception as e:
            results[i] = f"EXC: {e}"

    threads = [threading.Thread(target=worker, args=(i,)) for i in range(n)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()

    ok = sum(1 for v in results.values() if v == "OK")
    print(f"concurrent uploads: {ok}/{n} succeeded")
    for i in sorted(results):
        print(f"  worker {i}: {results[i]}")
    sys.exit(0 if ok == n else 1)


if __name__ == "__main__":
    main()
