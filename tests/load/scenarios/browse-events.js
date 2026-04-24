// k6 scenario: public event catalog browse.
// 100 VUs for 5 min hitting GET /events, /events/{id}, /events/{id}/ticket-types.
// Run:  k6 run scenarios/browse-events.js
// Env:  BASE_URL (default http://localhost:8000)

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8000';

export const options = {
    scenarios: {
        browse: {
            executor: 'constant-vus',
            vus: 100,
            duration: '5m',
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<500'],
        http_req_failed: ['rate<0.01'],
    },
};

const listLatency   = new Trend('ep_events_list_ms',   true);
const detailLatency = new Trend('ep_events_detail_ms', true);
const typesLatency  = new Trend('ep_events_types_ms',  true);

export default function () {
    const listRes = http.get(`${BASE_URL}/api/v1/events?page=1&pageSize=20`, {
        tags: { name: 'events_list' },
    });
    listLatency.add(listRes.timings.duration);
    check(listRes, {
        'list 200': (r) => r.status === 200,
        'list body has items': (r) => {
            try {
                const body = r.json();
                return Array.isArray(body) || Array.isArray(body.items) || Array.isArray(body.data);
            } catch { return false; }
        },
    });

    // Pick an event id from the list to hit detail + ticket-types.
    let eventId = null;
    try {
        const body = listRes.json();
        const arr = Array.isArray(body) ? body : (body.items || body.data || []);
        if (arr.length > 0) eventId = arr[0].id;
    } catch { /* swallow */ }

    if (eventId) {
        const detailRes = http.get(`${BASE_URL}/api/v1/events/${eventId}`, {
            tags: { name: 'events_detail' },
        });
        detailLatency.add(detailRes.timings.duration);
        check(detailRes, { 'detail 200': (r) => r.status === 200 });

        const typesRes = http.get(`${BASE_URL}/api/v1/events/${eventId}/ticket-types`, {
            tags: { name: 'events_ticket_types' },
        });
        typesLatency.add(typesRes.timings.duration);
        check(typesRes, { 'ticket-types 200': (r) => r.status === 200 });
    }

    sleep(1);
}
