"""Upload một file bất kỳ (vd: file webm thật) qua API chunked để tái hiện lỗi R2."""
import json
import sys
import urllib.error
import urllib.request
import uuid

BASE = "http://localhost:5000"


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
        raise RuntimeError(f"HTTP {e.code}: {e.read().decode()[:300]}") from None


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
    path = sys.argv[1]
    session_id = int(sys.argv[2]) if len(sys.argv) > 2 else 54
    question_id = int(sys.argv[3]) if len(sys.argv) > 3 else 35

    with open(path, "rb") as f:
        payload = f.read()

    token = post_json(
        f"{BASE}/api/auth/login", {"email": "demo-student@aives.local", "password": "Demo@12345"}
    )["accessToken"]

    uid = f"file-{uuid.uuid4().hex[:8]}"
    init = post_json(
        f"{BASE}/api/audio/chunked/init",
        {
            "uploadId": uid,
            "sessionId": session_id,
            "questionId": question_id,
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
    print(f"file={path} size={len(payload)}")
    print(f"  fallback : {done.get('storageFallback')}")
    print(f"  error    : {done.get('storageError')}")
    print(f"  key      : {done.get('audioKey')}")


if __name__ == "__main__":
    main()
