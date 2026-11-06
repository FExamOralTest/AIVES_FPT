# Kết quả demo Nginx Load Balancing (Thịnh)

> **Nguồn số liệu:** mọi giá trị dưới đây đến từ lượt chạy THẬT của k6 (`monitoring/k6/nginx-submit.js`) trên Docker Desktop, lưu tại `monitoring/results/<ngày>/nginx-demo-*/`.  Kịch bản: `login → start → submit-answer`, mỗi VU = 1 học sinh, 1 lần nộp bài.

## 1. Cấu hình hai lượt so sánh

| Nhãn | Đường đi | Số backend |
|---|---|---:|
| `direct` | k6 → `backend:8080` | 1 |
| `nginx` | k6 → `nginx:8088` → backend-1..3 | 3 |

Cả hai lượt dùng **cùng DB local PostgreSQL**, **cùng payload** (1 câu trả lời text), **cùng executor** (`per-vu-iterations`, `iterations = 1`).

## 2. Test matrix — số liệu tổng hợp

| Profile | Cấu hình | VUs | Requests | Throughput | Avg | p95 | p99 | Error rate | Checks |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Load 1 — 50 users | 1 backend (trực tiếp) | 50 | 150 | 5.30 req/s | 1130 ms | 3529 ms | — | 0.00 % | 100.00 % |
| Load 1 — 50 users | Nginx + 3 backend replica | 50 | 150 | 5.08 req/s | 619 ms | 1512 ms | — | 0.00 % | 100.00 % |
| Load 2 — 100 users | 1 backend (trực tiếp) | 100 | 300 | 5.62 req/s | 1359 ms | 4133 ms | — | 0.00 % | 100.00 % |
| Load 2 — 100 users | Nginx + 3 backend replica | 100 | 300 | 6.33 req/s | 1046 ms | 3086 ms | — | 0.00 % | 100.00 % |
| Load 3 — 200 users | 1 backend (trực tiếp) | 200 | 600 | 5.42 req/s | 2919 ms | 9681 ms | — | 0.00 % | 100.00 % |
| Load 3 — 200 users | Nginx + 3 backend replica | 200 | 600 | 5.74 req/s | 1956 ms | 6485 ms | — | 0.00 % | 100.00 % |


## 3. So sánh trước / sau tối ưu

| Profile | p95 trước | p95 sau | Thay đổi | Error rate trước | Error rate sau | Ghi chú |
|---|---|---|---|---|---|---|
| Load 1 — 50 users | 3529 ms | 1512 ms | p95 -57.1 % | 0.00 % | 0.00 % | p95 -57.1 % |
| Load 2 — 100 users | 4133 ms | 3086 ms | p95 -25.3 % | 0.00 % | 0.00 % | p95 -25.3 % |
| Load 3 — 200 users | 9681 ms | 6485 ms | p95 -33.0 % | 0.00 % | 0.00 % | p95 -33.0 % |


## 4. Các lượt chạy đã lưu

| File | Ngày | VUs | Cấu hình | Requests | p95 | Error rate |
|---|---|---:|---|---:|---:|---:|
| `monitoring/results/2026-11-06/nginx-demo-20261106-133803/50/k6-summary-direct-20261106-133811.json` | 2026-11-06 | 50 | direct | 150 | 3529 ms | 0.00 % |
| `monitoring/results/2026-11-06/nginx-demo-20261106-133845/50/k6-summary-nginx-20261106-133847.json` | 2026-11-06 | 50 | nginx | 150 | 1512 ms | 0.00 % |
| `monitoring/results/2026-11-06/nginx-demo-20261106-133921/100/k6-summary-direct-20261106-133922.json` | 2026-11-06 | 100 | direct | 300 | 4133 ms | 0.00 % |
| `monitoring/results/2026-11-06/nginx-demo-20261106-134019/100/k6-summary-nginx-20261106-134021.json` | 2026-11-06 | 100 | nginx | 300 | 3086 ms | 0.00 % |
| `monitoring/results/2026-11-06/nginx-demo-20261106-134113/200/k6-summary-direct-20261106-134114.json` | 2026-11-06 | 200 | direct | 600 | 9681 ms | 0.00 % |
| `monitoring/results/2026-11-06/nginx-demo-20261106-134311/200/k6-summary-nginx-20261106-134312.json` | 2026-11-06 | 200 | nginx | 600 | 6485 ms | 0.00 % |
