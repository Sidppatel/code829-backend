// k6 scenario: sustained read load on public event catalog.
// Target: 1000 req/min on /api/v1/events for 5 minutes.
//
// Run: k6 run scenarios/read-heavy.js

import http from 'k6/http';
import { check } from 'k6';
import { Trend } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8000';

export const options = {
    scenarios: {
        read_heavy: {
            executor: 'constant-arrival-rate',
            rate: 1000,
            timeUnit: '1m',
            duration: '5m',
            preAllocatedVUs: 50,
            maxVUs: 200,
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<500', 'p(99)<1000'],
        http_req_failed: ['rate<0.01'],
    },
};

const latency = new Trend('ep_read_ms', true);

export default function () {
    const res = http.get(`${BASE_URL}/api/v1/events?page=1&pageSize=20`, {
        tags: { name: 'events_list_read_heavy' },
    });
    latency.add(res.timings.duration);
    check(res, { 'status 200': (r) => r.status === 200 });
}
