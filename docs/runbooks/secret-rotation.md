# Secret rotation runbook

Each secret below has a trigger condition, generation command, deployment path, and verification step. All secrets live in Render env vars (production), Infisical `dev` (local), and Infisical `staging` (staging). Never commit a secret to git or `.env.local`.

## Universal procedure

1. Generate new secret value with the noted command.
2. Push to Infisical: `infisical secrets set NAME=<value> --env=<env>`.
3. Update Render dashboard env var. Do **not** trigger redeploy yet.
4. Apply per-secret coordination steps (e.g., dual-key for JWT, webhook re-register for Stripe).
5. Restart Render service (`Manual Deploy → Clear cache & deploy`).
6. Run the verification smoke.
7. After grace window (if any), remove the legacy value.

## JWT_SECRET (signing key)

- **Trigger:** quarterly, or on suspected compromise.
- **Generate:** `openssl rand -hex 32`
- **Deploy with grace window:**
  1. Copy current `JWT_SECRET` value into a new env var `JWT_SECRET_PREVIOUS`.
  2. Replace `JWT_SECRET` with the freshly generated value.
  3. Redeploy.
  4. Existing user sessions continue to validate against `JWT_SECRET_PREVIOUS`. New tokens sign with the new `JWT_SECRET`.
  5. After 24 hours (longest valid token lifetime + buffer), remove `JWT_SECRET_PREVIOUS` and redeploy.
- **Verify:** before removing the previous key, hit `/auth/me` with both an old (pre-rotation) cookie and a freshly-issued cookie — both must return 200.

## STRIPE_SECRET_KEY

- **Trigger:** on suspected compromise; otherwise rotate when revoking team access.
- **Generate:** Stripe Dashboard → Developers → API keys → Roll secret key.
- **Deploy:** update env var → restart Render. Stripe keys do not have a dual-key window — there is a brief gap where in-flight requests using the old key fail. Coordinate with low-traffic window.
- **Webhook secret (`STRIPE_WEBHOOK_SECRET`)** rotates independently — generate via Dashboard → Webhooks → Roll signing secret. Brief window where in-flight webhooks fail with 401; Stripe retries with backoff.
- **Verify:** `POST /bookings/quote` returns 200; tail logs for `PAYMENT_AMOUNT_MISMATCH`.

## RESEND_API_KEY

- **Trigger:** team member off-boarding, suspected compromise.
- **Generate:** Resend Dashboard → API Keys → Create.
- **Deploy:** update env var → restart Render. Old key remains valid until manually revoked in Resend Dashboard — keep it active for 1h grace, then revoke.
- **Verify:** trigger a magic-link request; confirm email arrives.

## S3_ACCESS_KEY + S3_SECRET_KEY

- **Trigger:** team member off-boarding, suspected compromise.
- **Generate:** Cloudflare R2 → Manage R2 API Tokens → Create token (R/W on bucket only).
- **Deploy:**
  1. Add the new credentials to Infisical/Render.
  2. Restart Render.
  3. Verify uploads work (see below).
  4. Revoke the old token in R2 dashboard.
- **Verify:** upload a small image via admin UI; confirm 200 + asset visible at CDN URL.

## Supabase database password (`DATABASE_URL` password component)

- **Trigger:** annually, or on suspected compromise.
- **Generate:** `openssl rand -base64 24`
- **Deploy:**
  1. Supabase Dashboard → Project Settings → Database → Reset database password.
  2. Update `DATABASE_URL` env var (both Render and any local `.env` via Infisical).
  3. Restart Render — connections will reconnect with new credentials. Existing pool connections drop and reconnect.
- **Verify:** `/health/ready` returns 200; check for `Npgsql.NpgsqlException` errors in logs.

## Upstash Redis password (`REDIS_URL` password component)

- **Trigger:** annually, or on suspected compromise.
- **Generate:** Upstash Console → Database → Reset password.
- **Deploy:** update `REDIS_URL` → restart Render.
- **Verify:** `/health/ready` returns 200; confirm `RedisCacheService` does not log connection errors.

## CLAMAV_HOST / CLAMAV_PORT

- Not a secret — connection coordinates only. Update when migrating ClamAV deployment. No dual-key needed.

## Audit cadence

- Quarterly: `JWT_SECRET`.
- Annually: `DATABASE_URL`, `REDIS_URL`, S3 keys.
- On-demand: any key on suspected compromise; revoke and rotate within 1h.
