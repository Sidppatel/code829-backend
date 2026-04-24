# ADR-0007: No business calculations on the frontend

- **Status:** Accepted
- **Date:** 2026-04-23
- **Tags:** frontend, backend, correctness

## Context

Early code had pricing math duplicated on both sides: frontend computed subtotals + fees + tax for live display, backend re-computed for booking persistence. Two independent implementations drifted — one rounded cents at subtotal, the other at total; one applied platform fee to tax-inclusive amount, the other to tax-exclusive. Users saw a different total than what they were charged.

Any business math duplicated across process boundaries will drift. The cost of drift scales with the number of money-touching rules (discounts, tiered fees, tax, rounding).

## Decision

**All business calculations happen on the backend.** Frontend is display-only for derived values.

- `PricingService` on the backend is the sole arithmetic surface for subtotal, fees, tax, discounts, total.
- `POST /bookings/quote` is the live preview endpoint. Frontend debounces user input, posts selections, renders the returned breakdown verbatim.
- Booking confirmation (`POST /bookings/confirm`) calls the same `PricingService` — quote math and booking math are guaranteed identical.
- Stripe `PaymentIntent.amount_received` is compared against the stored `StripeTransaction.AmountCents`; mismatch fails the booking with `PAYMENT_AMOUNT_MISMATCH`.

**Allowed on frontend:**
- Currency formatting (`formatCents` → `"$12.34"`) — display only.
- Form-input cents conversion (user types `"12.34"` → `1234` sent to backend) — input only.

**Enforced by:** ESLint rule (configured in `packages/shared` eslint config) flagging arithmetic on properties named `*Cents`, `*Amount`, `*Price`, `*Total`, `*Fee`, `*Subtotal`, `*Tax` outside allow-listed files (formatter + input parser).

## Consequences

### Positive
- **One source of truth for money.** Changing the fee formula is a one-line backend change, no FE coordination.
- **Audit:** every chargeable number was computed by one service.
- **No drift between quote and charge.** Stripe amount check catches any bug before money moves.

### Negative
- Live preview requires a network round-trip per input change (debounced). No offline preview.
- Frontend cannot show a "total updated" indicator until the quote response lands. Mitigated by optimistic UI state + skeleton on the total line.

### Neutral
- Backend tests cover pricing edge cases (rounding, tier boundaries, 100%-off coupons) — frontend tests assert the quote payload is rendered correctly, not that the math is correct.

## Alternatives Considered

### Share a pricing library between FE and BE (compiled from a single source)
Rejected: requires a build step to keep in sync, and the library still runs against two different floating-point substrates (JS number vs C# decimal). Cents-integer arithmetic works, but rounding modes differ at tier boundaries.

### FE computes, BE validates on submit
Rejected: user sees "FE said $100, server charged $101" — even if server is correct, this is a UX failure.

### BE computes, FE caches and re-uses stale quote
Rejected: quote drift on input changes surfaces as "amount updated since last preview" warnings — worse than a debounced fresh fetch.

## References

- `api/Services/PricingService.cs`
- Memory: `feedback_frontend_calculations.md`
- Related: ADR-0001 (SP-only data access — same "one source of truth" principle)
