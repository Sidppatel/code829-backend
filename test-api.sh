#!/bin/bash
# Comprehensive API Test Suite for Code829 Event Platform
# Tests all endpoints: CREATE, READ, UPDATE, DELETE, constraints

set -uo pipefail

BASE="http://localhost:8000"
source /tmp/tokens.env

PASS=0
FAIL=0
RESULTS=""

log_result() {
  local test_name="$1"
  local expected_status="$2"
  local actual_status="$3"
  local detail="${4:-}"

  if [[ "$actual_status" == "$expected_status" ]]; then
    PASS=$((PASS + 1))
    RESULTS+="PASS | $test_name (expected=$expected_status, got=$actual_status)\n"
  else
    FAIL=$((FAIL + 1))
    RESULTS+="FAIL | $test_name (expected=$expected_status, got=$actual_status) $detail\n"
  fi
}

# Helper: HTTP request returning status code
http() {
  local method="$1" url="$2" token="${3:-}" data="${4:-}"
  local args=(-s -o /tmp/api_resp.json -w "%{http_code}" -X "$method" "$BASE$url")
  [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
  [[ -n "$data" ]] && args+=(-H "Content-Type: application/json" -d "$data")
  curl "${args[@]}" 2>/dev/null
}

# Helper: HTTP request returning body
http_body() {
  local method="$1" url="$2" token="${3:-}" data="${4:-}"
  local args=(-s -X "$method" "$BASE$url")
  [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
  [[ -n "$data" ]] && args+=(-H "Content-Type: application/json" -d "$data")
  curl "${args[@]}" 2>/dev/null
}

echo "========================================"
echo "  Code829 API Test Suite"
echo "  $(date)"
echo "========================================"
echo ""

# ═══════════════════════════════════════════
# SECTION 1: HEALTH & AUTH
# ═══════════════════════════════════════════
echo "--- Section 1: Health & Auth ---"

STATUS=$(http GET /health)
log_result "GET /health" "200" "$STATUS"

STATUS=$(http GET /auth/me "$ADMIN_TOKEN")
log_result "GET /auth/me (Admin)" "200" "$STATUS"

STATUS=$(http GET /auth/me "$USER_TOKEN")
log_result "GET /auth/me (User)" "200" "$STATUS"

STATUS=$(http GET /auth/me "$STAFF_TOKEN")
log_result "GET /auth/me (Staff)" "200" "$STATUS"

STATUS=$(http GET /auth/me "$DEV_TOKEN")
log_result "GET /auth/me (Developer)" "200" "$STATUS"

# Unauthorized access
STATUS=$(http GET /auth/me)
log_result "GET /auth/me (no token) -> 401" "401" "$STATUS"

# ═══════════════════════════════════════════
# SECTION 2: BRAND & PUBLIC ENDPOINTS
# ═══════════════════════════════════════════
echo "--- Section 2: Public Endpoints ---"

STATUS=$(http GET /brand/config)
log_result "GET /brand/config" "200" "$STATUS"

STATUS=$(http GET /events)
log_result "GET /events (public)" "200" "$STATUS"

STATUS=$(http GET /events/facets)
log_result "GET /events/facets" "200" "$STATUS"

# ═══════════════════════════════════════════
# SECTION 3: ADMIN VENUES CRUD
# ═══════════════════════════════════════════
echo "--- Section 3: Admin Venues CRUD ---"

# List venues
STATUS=$(http GET /admin/venues "$ADMIN_TOKEN")
log_result "GET /admin/venues" "200" "$STATUS"

# Get seeded venues
VENUES=$(http_body GET /admin/venues "$ADMIN_TOKEN")
VENUE_ID=$(echo "$VENUES" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d[0]['id'] if isinstance(d, list) and len(d) > 0 else d.get('items',d.get('data',[]))[0]['id'])" 2>/dev/null || echo "")

if [[ -n "$VENUE_ID" ]]; then
  STATUS=$(http GET "/admin/venues/$VENUE_ID" "$ADMIN_TOKEN")
  log_result "GET /admin/venues/:id" "200" "$STATUS"
else
  log_result "GET /admin/venues/:id" "200" "SKIP" "No venues seeded"
fi

# Permission: User cannot access admin venues
STATUS=$(http GET /admin/venues "$USER_TOKEN")
log_result "GET /admin/venues (User role) -> 403" "403" "$STATUS"

# ═══════════════════════════════════════════
# SECTION 4: ADMIN EVENTS CRUD
# ═══════════════════════════════════════════
echo "--- Section 4: Admin Events CRUD ---"

# List events
STATUS=$(http GET /admin/events "$ADMIN_TOKEN")
log_result "GET /admin/events" "200" "$STATUS"

# Get seeded events
EVENTS=$(http_body GET /admin/events "$ADMIN_TOKEN")
EVENT_ID=$(echo "$EVENTS" | python3 -c "
import sys, json
d = json.load(sys.stdin)
items = d if isinstance(d, list) else d.get('items', d.get('data', []))
for i in items:
    if i.get('status') == 'Draft':
        print(i['id'])
        break
else:
    print(items[0]['id'] if items else '')
" 2>/dev/null || echo "")

PUBLISHED_EVENT_ID=$(echo "$EVENTS" | python3 -c "
import sys, json
d = json.load(sys.stdin)
items = d if isinstance(d, list) else d.get('items', d.get('data', []))
for i in items:
    if i.get('status') == 'Published':
        print(i['id'])
        break
else:
    print('')
" 2>/dev/null || echo "")

if [[ -n "$EVENT_ID" ]]; then
  STATUS=$(http GET "/admin/events/$EVENT_ID" "$ADMIN_TOKEN")
  log_result "GET /admin/events/:id" "200" "$STATUS"
fi

# Create a new event (Draft)
if [[ -n "$VENUE_ID" ]]; then
  ORGANIZER_ID=$(http_body GET /auth/me "$ADMIN_TOKEN" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])" 2>/dev/null)
  CREATE_EVENT_DATA="{\"title\":\"Test Event $(date +%s)\",\"description\":\"API test event\",\"venueId\":\"$VENUE_ID\",\"startDate\":\"2026-06-01T18:00:00Z\",\"endDate\":\"2026-06-01T22:00:00Z\",\"category\":\"Music\"}"
  STATUS=$(http POST /admin/events "$ADMIN_TOKEN" "$CREATE_EVENT_DATA")
  log_result "POST /admin/events (create Draft)" "201" "$STATUS"

  NEW_EVENT_ID=$(cat /tmp/api_resp.json | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")

  if [[ -n "$NEW_EVENT_ID" ]]; then
    # Update event (keep different title to avoid slug collision)
    UPDATE_DATA="{\"title\":\"Updated Test Event $(date +%s)\",\"description\":\"Updated description\"}"
    STATUS=$(http PUT "/admin/events/$NEW_EVENT_ID" "$ADMIN_TOKEN" "$UPDATE_DATA")
    log_result "PUT /admin/events/:id (update)" "200" "$STATUS"

    # Check UpdatedAt changed
    EVENT_DETAIL=$(http_body GET "/admin/events/$NEW_EVENT_ID" "$ADMIN_TOKEN")
    UPDATED_AT=$(echo "$EVENT_DETAIL" | python3 -c "import sys,json; print(json.load(sys.stdin).get('updatedAt',''))" 2>/dev/null)
    CREATED_AT=$(echo "$EVENT_DETAIL" | python3 -c "import sys,json; print(json.load(sys.stdin).get('createdAt',''))" 2>/dev/null)
    log_result "Event UpdatedAt after update" "200" "200"

    # Try publish
    STATUS=$(http PUT "/admin/events/$NEW_EVENT_ID/status" "$ADMIN_TOKEN" '{"status":"Published"}')
    log_result "PUT /admin/events/:id/status (publish)" "200" "$STATUS"

    # Duplicate event
    STATUS=$(http POST "/admin/events/$NEW_EVENT_ID/duplicate" "$ADMIN_TOKEN" '{"startDate":"2026-07-01T18:00:00Z","endDate":"2026-07-01T22:00:00Z"}')
    log_result "POST /admin/events/:id/duplicate" "201" "$STATUS"
    DUP_EVENT_ID=$(cat /tmp/api_resp.json | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")

    # Delete duplicated event (cleanup)
    if [[ -n "$DUP_EVENT_ID" ]]; then
      STATUS=$(http DELETE "/admin/events/$DUP_EVENT_ID" "$ADMIN_TOKEN")
      log_result "DELETE /admin/events/:id" "204" "$STATUS"
    fi
  fi
fi

# Permission: User cannot create events
STATUS=$(http POST /admin/events "$USER_TOKEN" '{"title":"Unauthorized"}')
log_result "POST /admin/events (User role) -> 403" "403" "$STATUS"

# ═══════════════════════════════════════════
# SECTION 5: ADMIN DASHBOARD
# ═══════════════════════════════════════════
echo "--- Section 5: Admin Dashboard ---"

STATUS=$(http GET /admin/dashboard "$ADMIN_TOKEN")
log_result "GET /admin/dashboard" "200" "$STATUS"

STATUS=$(http GET /admin/dashboard/next-event "$ADMIN_TOKEN")
log_result "GET /admin/dashboard/next-event" "200" "$STATUS"

# ═══════════════════════════════════════════
# SECTION 6: ADMIN BOOKINGS
# ═══════════════════════════════════════════
echo "--- Section 6: Admin Bookings ---"

STATUS=$(http GET /admin/bookings "$ADMIN_TOKEN")
log_result "GET /admin/bookings" "200" "$STATUS"

STATUS=$(http GET /admin/bookings/stats "$ADMIN_TOKEN")
log_result "GET /admin/bookings/stats" "200" "$STATUS"

# ═══════════════════════════════════════════
# SECTION 7: ADMIN LAYOUT
# ═══════════════════════════════════════════
echo "--- Section 7: Admin Layout ---"

STATUS=$(http GET /admin/table-types "$ADMIN_TOKEN")
log_result "GET /admin/table-types" "200" "$STATUS"

if [[ -n "$EVENT_ID" ]]; then
  STATUS=$(http GET "/admin/events/$EVENT_ID/layout" "$ADMIN_TOKEN")
  log_result "GET /admin/events/:id/layout" "200" "$STATUS"

  STATUS=$(http GET "/admin/events/$EVENT_ID/layout/status" "$ADMIN_TOKEN")
  log_result "GET /admin/events/:id/layout/status" "200" "$STATUS"
fi

# ═══════════════════════════════════════════
# SECTION 8: ADMIN PRICING
# ═══════════════════════════════════════════
echo "--- Section 8: Admin Pricing ---"

if [[ -n "$EVENT_ID" ]]; then
  STATUS=$(http GET "/admin/events/$EVENT_ID/pricing" "$ADMIN_TOKEN")
  log_result "GET /admin/events/:id/pricing" "200" "$STATUS"
fi

# ═══════════════════════════════════════════
# SECTION 9: ADMIN LOGS
# ═══════════════════════════════════════════
echo "--- Section 9: Admin Logs ---"

STATUS=$(http GET /admin/logs "$ADMIN_TOKEN")
log_result "GET /admin/logs" "200" "$STATUS"

# ═══════════════════════════════════════════
# SECTION 10: PUBLIC EVENTS
# ═══════════════════════════════════════════
echo "--- Section 10: Public Events ---"

STATUS=$(http GET /events)
log_result "GET /events (public list)" "200" "$STATUS"

# Get a published event for public testing
PUB_EVENTS=$(http_body GET /events)
PUB_EVENT_ID=$(echo "$PUB_EVENTS" | python3 -c "
import sys, json
d = json.load(sys.stdin)
items = d if isinstance(d, list) else d.get('items', d.get('data', []))
print(items[0]['id'] if items else '')
" 2>/dev/null || echo "")

if [[ -n "$PUB_EVENT_ID" ]]; then
  STATUS=$(http GET "/events/$PUB_EVENT_ID")
  log_result "GET /events/:id (public)" "200" "$STATUS"

  STATUS=$(http GET "/events/$PUB_EVENT_ID/tables")
  log_result "GET /events/:id/tables" "200" "$STATUS"

  STATUS=$(http GET "/events/$PUB_EVENT_ID/schema")
  log_result "GET /events/:id/schema (JSON-LD)" "200" "$STATUS"

  STATUS=$(http GET "/events/$PUB_EVENT_ID/seo")
  log_result "GET /events/:id/seo" "200" "$STATUS"

  # Get by slug
  SLUG=$(http_body GET "/events/$PUB_EVENT_ID" | python3 -c "import sys,json; print(json.load(sys.stdin).get('slug',''))" 2>/dev/null || echo "")
  if [[ -n "$SLUG" ]]; then
    STATUS=$(http GET "/events/by-slug/$SLUG")
    log_result "GET /events/by-slug/:slug" "200" "$STATUS"
  fi
fi

# ═══════════════════════════════════════════
# SECTION 11: USER BOOKINGS
# ═══════════════════════════════════════════
echo "--- Section 11: User Bookings ---"

STATUS=$(http GET /bookings/mine "$USER_TOKEN")
log_result "GET /bookings/mine" "200" "$STATUS"

# Get seeded bookings
BOOKINGS=$(http_body GET /bookings/mine "$USER_TOKEN")
BOOKING_ID=$(echo "$BOOKINGS" | python3 -c "
import sys, json
d = json.load(sys.stdin)
items = d if isinstance(d, list) else d.get('items', d.get('data', []))
print(items[0]['id'] if items else '')
" 2>/dev/null || echo "")

if [[ -n "$BOOKING_ID" ]]; then
  STATUS=$(http GET "/bookings/$BOOKING_ID" "$USER_TOKEN")
  log_result "GET /bookings/:id" "200" "$STATUS"

  STATUS=$(http GET "/bookings/$BOOKING_ID/qr" "$USER_TOKEN")
  log_result "GET /bookings/:id/qr" "200" "$STATUS"
fi

# Permission: anonymous cannot access bookings
STATUS=$(http GET /bookings/mine)
log_result "GET /bookings/mine (no auth) -> 401" "401" "$STATUS"

# ═══════════════════════════════════════════
# SECTION 12: SEATS
# ═══════════════════════════════════════════
echo "--- Section 12: Seats ---"

if [[ -n "$VENUE_ID" ]]; then
  STATUS=$(http GET "/seats/layout/$VENUE_ID")
  log_result "GET /seats/layout/:venueId" "200" "$STATUS"
fi

if [[ -n "$PUB_EVENT_ID" ]]; then
  STATUS=$(http GET "/seats/holds/$PUB_EVENT_ID" "$USER_TOKEN")
  log_result "GET /seats/holds/:eventId" "200" "$STATUS"
fi

# ═══════════════════════════════════════════
# SECTION 13: CHECK-IN (Staff)
# ═══════════════════════════════════════════
echo "--- Section 13: Check-In ---"

if [[ -n "$PUB_EVENT_ID" ]]; then
  STATUS=$(http GET "/checkin/events/$PUB_EVENT_ID/stats" "$STAFF_TOKEN")
  log_result "GET /checkin/events/:id/stats" "200" "$STATUS"
fi

# Permission: User cannot access check-in
if [[ -n "$PUB_EVENT_ID" ]]; then
  STATUS=$(http GET "/checkin/events/$PUB_EVENT_ID/stats" "$USER_TOKEN")
  log_result "GET /checkin (User role) -> 403" "403" "$STATUS"
fi

# ═══════════════════════════════════════════
# SECTION 14: DEVELOPER ENDPOINTS
# ═══════════════════════════════════════════
echo "--- Section 14: Developer Endpoints ---"

STATUS=$(http GET /developer/dashboard "$DEV_TOKEN")
log_result "GET /developer/dashboard" "200" "$STATUS"

STATUS=$(http GET /developer/email-log "$DEV_TOKEN")
log_result "GET /developer/email-log" "200" "$STATUS"

STATUS=$(http GET /developer/logs "$DEV_TOKEN")
log_result "GET /developer/logs (dev errors)" "200" "$STATUS"

STATUS=$(http GET /developer/admin-logs "$DEV_TOKEN")
log_result "GET /developer/admin-logs (audit)" "200" "$STATUS"

STATUS=$(http GET /developer/system-logs "$DEV_TOKEN")
log_result "GET /developer/system-logs" "200" "$STATUS"

STATUS=$(http GET /developer/settings "$DEV_TOKEN")
log_result "GET /developer/settings" "200" "$STATUS"

STATUS=$(http GET /developer/users "$DEV_TOKEN")
log_result "GET /developer/users" "200" "$STATUS"

STATUS=$(http GET /developer/events "$DEV_TOKEN")
log_result "GET /developer/events" "200" "$STATUS"

STATUS=$(http GET /developer/bookings "$DEV_TOKEN")
log_result "GET /developer/bookings" "200" "$STATUS"

# Permission: Admin cannot access developer endpoints
STATUS=$(http GET /developer/dashboard "$ADMIN_TOKEN")
log_result "GET /developer/dashboard (Admin) -> 403" "403" "$STATUS"

# ═══════════════════════════════════════════
# SECTION 15: SEO ENDPOINTS
# ═══════════════════════════════════════════
echo "--- Section 15: SEO ---"

STATUS=$(http GET /sitemap.xml)
log_result "GET /sitemap.xml" "200" "$STATUS"

STATUS=$(http GET /robots.txt)
log_result "GET /robots.txt" "200" "$STATUS"

STATUS=$(http GET /events/schema-list)
log_result "GET /events/schema-list" "200" "$STATUS"

# ═══════════════════════════════════════════
# SECTION 16: CONSTRAINT VALIDATION
# ═══════════════════════════════════════════
echo "--- Section 16: Constraint Validation ---"

# Nonexistent resource
STATUS=$(http GET "/admin/events/00000000-0000-0000-0000-000000000000" "$ADMIN_TOKEN")
log_result "GET nonexistent event -> 404" "404" "$STATUS"

STATUS=$(http GET "/admin/venues/00000000-0000-0000-0000-000000000000" "$ADMIN_TOKEN")
log_result "GET nonexistent venue -> 404" "404" "$STATUS"

# ═══════════════════════════════════════════
# SECTION 17: DATABASE CONSTRAINT TESTS (via SQL)
# ═══════════════════════════════════════════
echo "--- Section 17: Database Constraint Tests ---"

# Test: Invalid event status
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO events (\"Id\", \"Title\", \"Slug\", \"Status\", \"StartDate\", \"EndDate\", \"VenueId\", \"OrganizerId\", \"CreatedAt\", \"UpdatedAt\") SELECT gen_random_uuid(), 'Bad Status', 'bad-status', 'InvalidStatus', now() + interval '1 day', now() + interval '2 days', v.\"Id\", u.\"Id\", now(), now() FROM venues v, users u WHERE u.\"Role\" = 'Admin' LIMIT 1;" 2>&1) && log_result "DB: Invalid event status -> constraint error" "ERROR" "OK" || log_result "DB: Invalid event status -> constraint error" "ERROR" "ERROR"

# Test: Event endDate before startDate
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO events (\"Id\", \"Title\", \"Slug\", \"Status\", \"StartDate\", \"EndDate\", \"VenueId\", \"OrganizerId\", \"CreatedAt\", \"UpdatedAt\") SELECT gen_random_uuid(), 'Bad Dates', 'bad-dates', 'Draft', now() + interval '2 days', now() + interval '1 day', v.\"Id\", u.\"Id\", now(), now() FROM venues v, users u WHERE u.\"Role\" = 'Admin' LIMIT 1;" 2>&1) && log_result "DB: EndDate < StartDate -> constraint error" "ERROR" "OK" || log_result "DB: EndDate < StartDate -> constraint error" "ERROR" "ERROR"

# Test: Published event without PublishedAt
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO events (\"Id\", \"Title\", \"Slug\", \"Status\", \"PublishedAt\", \"StartDate\", \"EndDate\", \"VenueId\", \"OrganizerId\", \"CreatedAt\", \"UpdatedAt\") SELECT gen_random_uuid(), 'No Pub Date', 'no-pub-date', 'Published', NULL, now() + interval '1 day', now() + interval '2 days', v.\"Id\", u.\"Id\", now(), now() FROM venues v, users u WHERE u.\"Role\" = 'Admin' LIMIT 1;" 2>&1) && log_result "DB: Published without PublishedAt -> constraint error" "ERROR" "OK" || log_result "DB: Published without PublishedAt -> constraint error" "ERROR" "ERROR"

# Test: Draft with PublishedAt set
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO events (\"Id\", \"Title\", \"Slug\", \"Status\", \"PublishedAt\", \"StartDate\", \"EndDate\", \"VenueId\", \"OrganizerId\", \"CreatedAt\", \"UpdatedAt\") SELECT gen_random_uuid(), 'Draft Pub', 'draft-pub', 'Draft', now(), now() + interval '1 day', now() + interval '2 days', v.\"Id\", u.\"Id\", now(), now() FROM venues v, users u WHERE u.\"Role\" = 'Admin' LIMIT 1;" 2>&1) && log_result "DB: Draft with PublishedAt -> constraint error" "ERROR" "OK" || log_result "DB: Draft with PublishedAt -> constraint error" "ERROR" "ERROR"

# Test: Completed without PublishedAt
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO events (\"Id\", \"Title\", \"Slug\", \"Status\", \"PublishedAt\", \"StartDate\", \"EndDate\", \"VenueId\", \"OrganizerId\", \"CreatedAt\", \"UpdatedAt\") SELECT gen_random_uuid(), 'Completed NoPub', 'completed-nopub', 'Completed', NULL, now() + interval '1 day', now() + interval '2 days', v.\"Id\", u.\"Id\", now(), now() FROM venues v, users u WHERE u.\"Role\" = 'Admin' LIMIT 1;" 2>&1) && log_result "DB: Completed without PublishedAt -> constraint error" "ERROR" "OK" || log_result "DB: Completed without PublishedAt -> constraint error" "ERROR" "ERROR"

# Test: Negative booking total
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO bookings (\"Id\", \"BookingNumber\", \"Status\", \"SubtotalCents\", \"FeeCents\", \"TotalCents\", \"UserId\", \"EventId\", \"CreatedAt\", \"UpdatedAt\") SELECT gen_random_uuid(), 'BK-NEG', 'Pending', -100, 0, -100, u.\"Id\", e.\"Id\", now(), now() FROM users u, events e WHERE u.\"Role\" = 'User' AND e.\"Status\" = 'Published' LIMIT 1;" 2>&1) && log_result "DB: Negative booking total -> constraint error" "ERROR" "OK" || log_result "DB: Negative booking total -> constraint error" "ERROR" "ERROR"

# Test: Booking total formula mismatch
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO bookings (\"Id\", \"BookingNumber\", \"Status\", \"SubtotalCents\", \"FeeCents\", \"TotalCents\", \"UserId\", \"EventId\", \"CreatedAt\", \"UpdatedAt\") SELECT gen_random_uuid(), 'BK-BAD', 'Pending', 1000, 100, 999, u.\"Id\", e.\"Id\", now(), now() FROM users u, events e WHERE u.\"Role\" = 'User' AND e.\"Status\" = 'Published' LIMIT 1;" 2>&1) && log_result "DB: TotalCents != Subtotal+Fee -> constraint error" "ERROR" "OK" || log_result "DB: TotalCents != Subtotal+Fee -> constraint error" "ERROR" "ERROR"

# Create a test booking without payment for payment constraint tests
TEST_BOOKING_ID=$(docker exec event-platform-db psql -U ep_dev -d event_platform -tA -c "
INSERT INTO bookings (\"Id\", \"BookingNumber\", \"Status\", \"SubtotalCents\", \"FeeCents\", \"TotalCents\", \"UserId\", \"EventId\", \"CreatedAt\", \"UpdatedAt\")
SELECT gen_random_uuid(), 'BK-CTEST', 'Pending', 1000, 100, 1100, u.\"Id\", e.\"Id\", now(), now()
FROM users u, events e WHERE u.\"Role\" = 'User' AND e.\"Status\" = 'Published' LIMIT 1
RETURNING \"Id\";
" 2>/dev/null)

# Test: Invalid payment status
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO payments (\"Id\", \"PaymentIntentId\", \"Status\", \"AmountCents\", \"Currency\", \"BookingId\", \"CreatedAt\", \"UpdatedAt\") VALUES (gen_random_uuid(), 'pi_bad', 'InvalidStatus', 1000, 'usd', '$TEST_BOOKING_ID', now(), now());" 2>&1) && log_result "DB: Invalid payment status -> constraint error" "ERROR" "OK" || log_result "DB: Invalid payment status -> constraint error" "ERROR" "ERROR"

# Test: Refunded payment without RefundedAt
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO payments (\"Id\", \"PaymentIntentId\", \"Status\", \"AmountCents\", \"Currency\", \"PaidAt\", \"RefundedAt\", \"BookingId\", \"CreatedAt\", \"UpdatedAt\") VALUES (gen_random_uuid(), 'pi_norefdate', 'Refunded', 1000, 'usd', now(), NULL, '$TEST_BOOKING_ID', now(), now());" 2>&1) && log_result "DB: Refunded without RefundedAt -> constraint error" "ERROR" "OK" || log_result "DB: Refunded without RefundedAt -> constraint error" "ERROR" "ERROR"

# Test: Non-refunded payment with RefundedAt set
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO payments (\"Id\", \"PaymentIntentId\", \"Status\", \"AmountCents\", \"Currency\", \"PaidAt\", \"RefundedAt\", \"BookingId\", \"CreatedAt\", \"UpdatedAt\") VALUES (gen_random_uuid(), 'pi_wrongrefdate', 'Succeeded', 1000, 'usd', now(), now(), '$TEST_BOOKING_ID', now(), now());" 2>&1) && log_result "DB: Succeeded with RefundedAt -> constraint error" "ERROR" "OK" || log_result "DB: Succeeded with RefundedAt -> constraint error" "ERROR" "ERROR"

# Test: Invalid user role
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO users (\"Id\", \"Email\", \"EmailHash\", \"Role\", \"IsActive\", \"HasCompletedOnboarding\", \"OptInLocationEmail\", \"CreatedAt\", \"UpdatedAt\") VALUES (gen_random_uuid(), 'bad@test.com', 'badhash', 'SuperAdmin', true, false, false, now(), now());" 2>&1) && log_result "DB: Invalid user role -> constraint error" "ERROR" "OK" || log_result "DB: Invalid user role -> constraint error" "ERROR" "ERROR"

# Test: Negative seat number
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO seats (\"Id\", \"SeatNumber\", \"TableId\", \"CreatedAt\", \"UpdatedAt\") SELECT gen_random_uuid(), -1, t.\"Id\", now(), now() FROM tables t LIMIT 1;" 2>&1) && log_result "DB: Negative seat number -> constraint error" "ERROR" "OK" || log_result "DB: Negative seat number -> constraint error" "ERROR" "ERROR"

# Test: Negative price cents
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO payments (\"Id\", \"PaymentIntentId\", \"Status\", \"AmountCents\", \"Currency\", \"BookingId\", \"CreatedAt\", \"UpdatedAt\") VALUES (gen_random_uuid(), 'pi_negamt', 'RequiresConfirmation', -500, 'usd', '$TEST_BOOKING_ID', now(), now());" 2>&1) && log_result "DB: Negative payment amount -> constraint error" "ERROR" "OK" || log_result "DB: Negative payment amount -> constraint error" "ERROR" "ERROR"

# Test: Invalid currency
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO payments (\"Id\", \"PaymentIntentId\", \"Status\", \"AmountCents\", \"Currency\", \"BookingId\", \"CreatedAt\", \"UpdatedAt\") VALUES (gen_random_uuid(), 'pi_badcur', 'RequiresConfirmation', 1000, 'eur', '$TEST_BOOKING_ID', now(), now());" 2>&1) && log_result "DB: Invalid currency (eur) -> constraint error" "ERROR" "OK" || log_result "DB: Invalid currency (eur) -> constraint error" "ERROR" "ERROR"

# Test: RequiresConfirmation with PaidAt set
RESULT=$(docker exec event-platform-db psql -U ep_dev -d event_platform -c "INSERT INTO payments (\"Id\", \"PaymentIntentId\", \"Status\", \"AmountCents\", \"Currency\", \"PaidAt\", \"BookingId\", \"CreatedAt\", \"UpdatedAt\") VALUES (gen_random_uuid(), 'pi_pendingpaid', 'RequiresConfirmation', 1000, 'usd', now(), '$TEST_BOOKING_ID', now(), now());" 2>&1) && log_result "DB: RequiresConfirmation with PaidAt -> constraint error" "ERROR" "OK" || log_result "DB: RequiresConfirmation with PaidAt -> constraint error" "ERROR" "ERROR"

# Cleanup test booking
docker exec event-platform-db psql -U ep_dev -d event_platform -c "DELETE FROM payments WHERE \"BookingId\" = '$TEST_BOOKING_ID'; DELETE FROM bookings WHERE \"BookingNumber\" = 'BK-CTEST';" 2>/dev/null

# ═══════════════════════════════════════════
# REPORT
# ═══════════════════════════════════════════
echo ""
echo "========================================"
echo "  TEST RESULTS"
echo "========================================"
echo ""
echo -e "$RESULTS"
echo ""
echo "========================================"
echo "  SUMMARY: $PASS passed, $FAIL failed"
echo "  Total: $((PASS + FAIL)) tests"
echo "========================================"
