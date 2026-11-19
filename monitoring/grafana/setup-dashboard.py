#!/usr/bin/env python3
"""
Thiết lập Grafana cho demo Nginx (Thịnh).

- Tạo (hoặc cập nhật) datasource Prometheus với UID cố định `cg393kpfzb2f4b`
  để khớp với `monitoring/aives-dashboard.json`.
- Import dashboard `monitoring/aives-dashboard.json` (nếu chưa có).

Dùng Basic auth admin/admin (biểu diễn local, không phải secret thật).

Cách dùng:
    python monitoring/grafana/setup-dashboard.py
    python monitoring/grafana/setup-dashboard.py --url http://localhost:3000 --user admin --password admin
"""

from __future__ import annotations

import argparse
import base64
import json
import os
import sys
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
DASHBOARD_FILES = [
    os.path.abspath(os.path.join(HERE, "..", "aives-dashboard.json")),
    os.path.abspath(os.path.join(HERE, "..", "nginx-dashboard.json")),
]

PROMETHEUS_URL = "http://prometheus:9090"
DATASOURCE_UID = "cg393kpfzb2f4b"


class GrafanaClient:
    def __init__(self, base_url: str, user: str, password: str):
        self.base_url = base_url.rstrip("/")
        token = base64.b64encode(f"{user}:{password}".encode()).decode()
        self._auth = f"Basic {token}"

    def _request(self, method: str, path: str, payload=None):
        data = json.dumps(payload).encode() if payload is not None else None
        req = urllib.request.Request(
            f"{self.base_url}{path}",
            data=data,
            method=method,
            headers={
                "Authorization": self._auth,
                "Content-Type": "application/json",
            },
        )
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                body = resp.read().decode()
                return resp.status, (json.loads(body) if body else None)
        except urllib.error.HTTPError as exc:
            body = exc.read().decode()
            try:
                return exc.code, json.loads(body)
            except json.JSONDecodeError:
                return exc.code, body
        except urllib.error.URLError as exc:
            raise SystemExit(f"Không kết nối được Grafana tại {self.base_url}: {exc.reason}")

    def ensure_datasource(self) -> str:
        status, existing = self._request("GET", "/api/datasources/uid/" + DATASOURCE_UID)
        payload = {
            "uid": DATASOURCE_UID,
            "name": "Prometheus",
            "type": "prometheus",
            "access": "proxy",
            "url": PROMETHEUS_URL,
            "isDefault": True,
            "jsonData": {
                "httpMethod": "POST",
                "timeInterval": "15s",
            },
        }
        if status == 200:
            self._request("PUT", "/api/datasources/uid/" + DATASOURCE_UID, payload)
            print(f"[ok] datasource Prometheus (uid={DATASOURCE_UID}) đã được cập nhật")
        else:
            status, body = self._request("POST", "/api/datasources", payload)
            if status not in (200, 409):
                raise SystemExit(f"Tạo datasource thất bại: HTTP {status} {body}")
            print(f"[ok] datasource Prometheus (uid={DATASOURCE_UID}) đã được tạo")

        # Chờ Prometheus sẵn sàng
        status, body = self._request("GET", "/api/datasources/uid/" + DATASOURCE_UID + "/health")
        if status == 200:
            print(f"[ok] datasource healthy: {body.get('message', 'OK')}")
        else:
            print(f"[warn] datasource health check: HTTP {status} {body}")
        return DATASOURCE_UID

    def ensure_dashboard(self, dashboard_file: str) -> None:
        with open(dashboard_file, encoding="utf-8") as fh:
            dashboard = json.load(fh)

        uid = dashboard.get("uid")
        status, _ = self._request("GET", f"/api/dashboards/uid/{uid}")
        if status == 200:
            print(f"[ok] dashboard '{dashboard.get('title')}' đã tồn tại (uid={uid})")
            return

        payload = {
            "dashboard": dashboard,
            "folderUid": "",
            "overwrite": True,
            "message": "AIVES demo dashboard",
        }
        status, body = self._request("POST", "/api/dashboards/db", payload)
        if status not in (200, 202):
            raise SystemExit(f"Import dashboard thất bại: HTTP {status} {body}")
        print(f"[ok] dashboard '{dashboard.get('title')}' đã import (uid={uid})")


def main() -> int:
    parser = argparse.ArgumentParser(description="Thiết lập datasource + dashboard Grafana")
    parser.add_argument("--url", default=os.environ.get("GRAFANA_URL", "http://localhost:3000"))
    parser.add_argument("--user", default=os.environ.get("GRAFANA_USER", "admin"))
    parser.add_argument("--password", default=os.environ.get("GRAFANA_PASSWORD", "admin"))
    args = parser.parse_args()

    client = GrafanaClient(args.url, args.user, args.password)
    client.ensure_datasource()
    for dashboard_file in DASHBOARD_FILES:
        if os.path.isfile(dashboard_file):
            client.ensure_dashboard(dashboard_file)
        else:
            print(f"[warn] không tìm thấy file dashboard: {dashboard_file}")
    print(f"\nMở: {args.url}/d/aives-nginx-lb  (Nginx Load Balancing)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
