import http from 'k6/http';
import { check } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// ─────────────────────────────────────────────────────────────────────
//  k6 — demo submit-answer qua Nginx load balancer (Thịnh).
//
//  Luồng mô phỏng:  học sinh đăng nhập → bắt đầu ca thi → nộp câu trả lời
//  Mỗi VU = 1 học sinh (executor per-vu-iterations, iterations = 1).
//
//  BASE_URL trỏ vào Nginx (http://nginx:8088) để đo được tác động của
//  load balancing, hoặc thẳng vào backend (http://backend:8080) để đo
//  trạng thái "trước tối ưu".
// ─────────────────────────────────────────────────────────────────────

const vus = Number(__ENV.VUS || 50);
const sessionId = Number(__ENV.SESSION_ID);
const baseUrl = __ENV.BASE_URL || 'http://nginx:8088';
const userPrefix = __ENV.USER_PREFIX || 'demo-';
const password = __ENV.TEST_PASSWORD;
const fixturePath = __ENV.FIXTURE_PATH || '/results/fixture.json';

const submitDuration = new Trend('submit_answer_duration', true);
const submitFailures = new Rate('submit_answer_failures');
const loginFailures = new Rate('login_failures');

if (!Number.isInteger(vus) || vus < 1 || vus > 500) {
  throw new Error('VUS must be an integer from 1 to 500.');
}
if (!Number.isInteger(sessionId) || sessionId < 1) {
  throw new Error('SESSION_ID must be a positive integer.');
}
if (!password) {
  throw new Error('TEST_PASSWORD must be provided for the local seeded test accounts.');
}

// Fixture do prepare-demo-fixture.ps1 sinh ra (danh sách user + session).
// `open()` chỉ dùng được ở init stage (global scope) — không gọi trong setup().
const fixture = JSON.parse(open(fixturePath));

export const options = {
  setupTimeout: '10m',
  // 1 VU = 1 lần nộp bài → số iteration = số VU (không lặp lại học sinh).
  scenarios: {
    submit_answers: {
      executor: 'per-vu-iterations',
      vus,
      iterations: 1,
      maxDuration: '10m',
      gracefulStop: '30s',
    },
  },
  thresholds: {
    // Ngưỡng lỗi mềm — k6 vẫn xuất summary khi vượt (exit code 99).
    'submit_answer_failures': ['rate<0.05'],
    'checks': ['rate>0.95'],
  },
};

function multipart(fields) {
  const boundary = `k6-aives-${__VU}-${Date.now()}`;
  const body = Object.entries(fields)
    .map(([name, value]) => (
      `--${boundary}\r\n` +
      `Content-Disposition: form-data; name="${name}"\r\n\r\n` +
      `${value}\r\n`
    ))
    .join('') + `--${boundary}--\r\n`;

  return {
    body,
    headers: { 'Content-Type': `multipart/form-data; boundary=${boundary}` },
  };
}

export function setup() {
  const users = [];
  for (let i = 0; i < vus; i += 1) {
    const username = `${userPrefix}${String(i + 1).padStart(4, '0')}`;

    const login = http.post(
      `${baseUrl}/api/auth/login`,
      JSON.stringify({ identifier: username, password }),
      {
        headers: { 'Content-Type': 'application/json' },
        tags: { name: 'setup_login' },
        timeout: '30s',
      },
    );
    if (login.status !== 200 || !login.json('accessToken')) {
      loginFailures.add(1);
      throw new Error(`Setup login failed for ${username}: HTTP ${login.status}.`);
    }

    const token = login.json('accessToken');
    const start = http.post(`${baseUrl}/api/student/viva-sessions/${sessionId}/start`, null, {
      headers: { Authorization: `Bearer ${token}` },
      tags: { name: 'setup_start_session' },
      timeout: '30s',
    });
    const questionId = start.json('currentQuestion.questionId');
    if (start.status !== 200 || !Number.isInteger(questionId)) {
      throw new Error(`Setup could not start session for ${username}: HTTP ${start.status}.`);
    }
    users.push({ token, questionId });
  }
  return users;
}

export default function (users) {
  const user = users[__VU - 1];
  if (!user) {
    throw new Error(`No prepared student credentials for VU ${__VU}.`);
  }
  const authHeaders = { Authorization: `Bearer ${user.token}` };
  const form = multipart({
    QuestionId: user.questionId,
    TextAnswer: 'Nginx demo: round-robin distributes each request to the next healthy backend replica.',
    IsFollowUp: 'false',
  });

  const submit = http.post(
    `${baseUrl}/api/student/viva-sessions/${sessionId}/submit-answer`,
    form.body,
    {
      headers: { ...authHeaders, ...form.headers },
      tags: { name: 'submit_answer' },
      timeout: '60s',
    },
  );

  const submitOk = check(submit, {
    'answer is accepted': (r) => r.status === 200 && r.json('success') === true,
  });
  if (!submitOk) {
    console.error(`Submit failed for VU ${__VU}: HTTP ${submit.status} ${String(submit.body).slice(0, 300)}`);
  }
  submitFailures.add(!submitOk);
  submitDuration.add(submit.timings.duration);
}

export function handleSummary(data) {
  delete data.setup_data;
  return {
    // Chỉ xuất ra stdout — file summary được ghi bằng --summary-export.
    // Nếu cùng trỏ 1 path, k6 sẽ ghi đè/ghi lỗi 2 lần.
    stdout: JSON.stringify(data, null, 2),
  };
}
