# API parity report

- Baseline: `D:\event-platform\code829-backend\tools\ApiDiff\baseline`
- Current:  `http://localhost:8000`
- Pass: 55   Fail: 4   Total: 59

| id | tier | baseline | current | result | detail |
|---|---|---|---|---|---|
| admin_dashboard | detail | 200 | 200 | PASS |  |
| admin_dashboard_next_event | detail | 200 | 200 | PASS |  |
| developer_dashboard | detail | 200 | 200 | PASS |  |
| developer_dashboard_next_event | detail | 200 | 200 | PASS |  |
| developer_monthly_report | detail | 200 | 200 | PASS |  |
| admin_events_list | detail | 200 | 200 | PASS |  |
| admin_event_stats | detail | 200 | 200 | PASS |  |
| admin_layout_stats | detail | 200 | 200 | PASS |  |
| admin_layout_status | detail | 200 | 200 | PASS |  |
| admin_purchases_stats | detail | 200 | 200 | PASS |  |
| events_facets | detail | 200 | 200 | PASS |  |
| events_schema_list | detail | 200 | 200 | PASS |  |
| events_list | smoke | 200 | 200 | PASS |  |
| event_by_id | smoke | 200 | 200 | PASS |  |
| event_tables | smoke | 200 | 200 | PASS |  |
| event_ticket_types | smoke | 200 | 200 | PASS |  |
| event_images | smoke | 200 | 200 | PASS |  |
| event_seo | smoke | 200 | 200 | PASS |  |
| event_schema | smoke | 200 | 200 | PASS |  |
| admin_event_by_id | smoke | 200 | 200 | PASS |  |
| admin_event_ticket_types | smoke | 200 | 200 | PASS |  |
| admin_event_layout_locked | smoke | 200 | 200 | PASS |  |
| admin_event_layout | smoke | 200 | 200 | PASS |  |
| admin_event_layout_draft | smoke | 200 | 200 | PASS |  |
| admin_event_layout_locked2 | smoke | 200 | 200 | PASS |  |
| admin_event_tables | smoke | 200 | 200 | PASS |  |
| admin_event_staff | smoke | 200 | 200 | PASS |  |
| admin_table_templates | smoke | 200 | 200 | PASS |  |
| admin_purchases_list | smoke | 200 | 200 | PASS |  |
| admin_staff_list | smoke | 200 | 200 | PASS |  |
| admin_staff_invitations | smoke | 200 | 200 | PASS |  |
| admin_venues | smoke | 200 | 200 | PASS |  |
| admin_platform_images | smoke | 200 | 200 | PASS |  |
| admin_logs | smoke | 200 | 200 | PASS |  |
| admin_auth_me | smoke | 200 | 200 | PASS |  |
| admin_auth_sessions | smoke | 200 | 200 | COUNT_MISMATCH | items 1 -> 5 |
| admin_stripe_status | smoke | 409 | 409 | PASS |  |
| checkin_events | smoke | 200 | 200 | PASS |  |
| developer_email_log | smoke | 200 | 200 | PASS |  |
| developer_logs | smoke | 200 | 200 | COUNT_MISMATCH | items 0 -> 10 |
| developer_system_logs | smoke | 200 | 200 | COUNT_MISMATCH | items 0 -> 10 |
| developer_admin_logs | smoke | 200 | 200 | PASS |  |
| developer_settings | smoke | 200 | 200 | PASS |  |
| developer_users | smoke | 200 | 200 | PASS |  |
| developer_admin_users | smoke | 200 | 200 | PASS |  |
| developer_organizations | smoke | 200 | 200 | PASS |  |
| developer_invitations | smoke | 200 | 200 | PASS |  |
| developer_logo | smoke | 401 | 401 | PASS |  |
| developer_stripe_status | smoke | 200 | 200 | PASS |  |
| feedback_list | smoke | 200 | 200 | PASS |  |
| tickets_mine | smoke | 200 | 200 | PASS |  |
| auth_me | smoke | 200 | 200 | PASS |  |
| auth_sessions | smoke | 200 | 200 | COUNT_MISMATCH | items 1 -> 5 |
| purchases_mine | smoke | 200 | 200 | PASS |  |
| purchases_stripe_config | smoke | 200 | 200 | PASS |  |
| health | smoke | 404 | 404 | PASS |  |
| health_live | smoke | 200 | 200 | PASS |  |
| sitemap | smoke | 200 | 200 | PASS |  |
| robots | smoke | 200 | 200 | PASS |  |

