import http from 'k6/http';
import { check } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const vus = Number(__ENV.VUS || 50);
const sessionId = Number(__ENV.SESSION_ID);
const baseUrl = __ENV.BASE_URL || 'http://backend:8080';
const userPrefix = __ENV.USER_PREFIX || 'loadtest-';
const userOffset = Number(__ENV.USER_OFFSET || 0);
const password = __ENV.TEST_PASSWORD;

const submitDuration = new Trend('submit_answer_duration', true);
const submitFailures = new Rate('submit_answer_failures');

if (!Number.isInteger(vus) || vus < 1 || vus > 500) {
  throw new Error('VUS must be an integer from 1 to 500.');
}
if (!Number.isInteger(sessionId) || sessionId < 1) {
  throw new Error('SESSION_ID must be a positive integer.');
}
if (!Number.isInteger(userOffset) || userOffset < 0 || userOffset + vus > 850) {
  throw new Error('USER_OFFSET and VUS must select users within the 850-account local fixture.');
}
if (!password) {
  throw new Error('TEST_PASSWORD must be provided for the local seeded test accounts.');
}

export const options = {
  setupTimeout: '15m',
  scenarios: {
    submit_answers: {
      executor: 'per-vu-iterations',
      vus,
      iterations: 1,
      maxDuration: '5m',
    },
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
  for (let userNumber = userOffset + 1; userNumber <= userOffset + vus; userNumber += 1) {
    const username = `${userPrefix}${String(userNumber).padStart(4, '0')}`;
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
      throw new Error(`Load-test setup login failed for ${username}: HTTP ${login.status}.`);
    }

    const token = login.json('accessToken');
    const authHeaders = { Authorization: `Bearer ${token}` };
    const start = http.post(`${baseUrl}/api/student/viva-sessions/${sessionId}/start`, null, {
      headers: authHeaders,
      tags: { name: 'setup_start_session' },
      timeout: '30s',
    });
    const questionId = start.json('currentQuestion.questionId');
    if (start.status !== 200 || !Number.isInteger(questionId)) {
      throw new Error(`Load-test setup could not start session for ${username}: HTTP ${start.status}.`);
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
    TextAnswer: 'Load-test response: latency and throughput are measured as concurrent users increase.',
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
    'answer is accepted': (response) => response.status === 200 && response.json('success') === true,
  });
  if (!submitOk) {
    console.error(`Answer submission failed for VU ${__VU}: HTTP ${submit.status} ${submit.body.slice(0, 500)}`);
  }
  submitFailures.add(!submitOk);
  submitDuration.add(submit.timings.duration);
}

export function handleSummary(data) {
  delete data.setup_data;
  return {
    '/results/k6-summary.json': JSON.stringify(data, null, 2),
  };
}
