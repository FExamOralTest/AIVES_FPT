# AIVES local monitoring load test

- Date: 2026-11-04 (Asia/Ho_Chi_Minh)
- Application: Docker Compose project `aives-dev`
- Database: local PostgreSQL container `aives-dev-postgres-1` (`aives_dev`); no Neon connection used
- Load target: `GET /api/health/db` through the AIVES backend
- k6 script: `monitoring/k6/health-db.js`
- Duration: 60 seconds per VU level; each VU waits one second between requests
- Metrics: k6 summaries plus Prometheus average and peak CPU, working-set memory, and network receive/transmit for frontend, backend, and AI service during each measured interval
- Screenshots: Grafana dashboard, captured during each load level

The endpoint performs a database connectivity check and reads user/course counts, so the load exercises the backend and local database. This synthetic test does not represent every application workflow.
