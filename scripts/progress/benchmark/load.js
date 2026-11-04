import http from 'k6/http';
import execution from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';
const clients = JSON.parse(open('/output/clients.json'));
const latency = new Trend('business_latency');
const outcomes = new Counter('business_outcomes');
export const options = {
  scenarios: {
    warmup: { executor: 'constant-arrival-rate', rate: 120, timeUnit: '1s', duration: '15s', preAllocatedVUs: 240, maxVUs: 240, gracefulStop: '5s' },
    steady: { executor: 'constant-arrival-rate', rate: 120, timeUnit: '1s', duration: '60s', startTime: '20s', preAllocatedVUs: 240, maxVUs: 240, gracefulStop: '10s' },
  },
  discardResponseBodies: true,
};
export default function () {
  const n = execution.scenario.iterationInTest;
  const slot = n % 10;
  const client = clients[slot < 3 ? 0 : 1 + n % 32];
  // Exactly 60% reads: history20, dashboard20, water10, fasting10; writes20+20.
  const routes = ['/metrics/history?pageNumber=1&pageSize=20', '/metrics/history?pageNumber=2&pageSize=20', '/dashboard/summary', '/dashboard/summary', '/water/today', '/fasting/current', '/water/log', '/water/log', '/metrics', '/metrics'];
  const route = routes[slot];
  const kind = slot < 6 ? 'read' : 'write';
  const phase = execution.scenario.name;
  const params = { headers: { Authorization: `Bearer ${client.token}`, 'Content-Type': 'application/json' }, timeout: '5s', tags: { phase, kind, route } };
  const started = Date.now();
  const payload = slot < 8 ? { amountMl: 250 } : { loggedAt: new Date(started - 1000).toISOString(), weightKg: 70.00 };
  const response = kind === 'read' ? http.get(`${__ENV.BASE_URL}${route}`, params) : http.post(`${__ENV.BASE_URL}${route}`, JSON.stringify(payload), params);
  const tags = { phase, kind, route, status: String(response.status), window_start: String(execution.scenario.startTime) };
  // k6 transport timings use its monotonic clock; blocked includes connect/TLS/DNS.
  latency.add(response.timings.duration + response.timings.blocked, tags);
  outcomes.add(1, tags);
}
