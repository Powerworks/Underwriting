What's actually inside a bordereau

A bordereau is essentially a structured spreadsheet/report — one row per policy transaction — that the cell sends to each capacity provider it owes an account to. A typical premium bordereau row includes:

- Policy/certificate reference
- Insured name, class of business
- Inception/expiry dates
- Gross premium, brokerage deducted, MGA fee deducted, net premium due to this provider
- The provider's quota share % and their premium share (if split across multiple providers)
- Transaction type: new business, renewal, endorsement, cancellation
- Currency

A claims bordereau mirrors this for claims: claim reference, policy it relates to, amount paid/reserved, the provider's share, claim status (open/closed/reopened).

Some larger treaty arrangements also use a risk/declaration bordereau — where the treaty requires every individual risk bound under it to be formally "declared" to the reinsurer, separate from the premium settlement itself. Worth knowing exists, but you can treat it as a variant of the same pattern rather than a wholly separate concept.

The settlement cycle, end to end

1. Period close — typically monthly (sometimes quarterly for smaller/simpler arrangements). The cell "cuts" a bordereau covering everything bound, endorsed, or cancelled in that period.
2. Bordereau produced and sent to each capacity provider on the panel for that class of business.
3. Provider review/query window — the provider (or their TPA/back-office) checks the bordereau against their own records, raises queries on anything that looks wrong (wrong quota share applied, a risk outside authority that shouldn't have been bound, a rate that doesn't match agreed terms).
4. Agreement — once queried lines are resolved, the bordereau is "agreed" — this is the point it becomes a real financial obligation, not just a draft.
5. Cash settlement — net premium actually moves (cell → provider, since providers are owed the net-after-fees premium; conversely providers owe claims float top-ups going the other way).

The critical thing: bind time and settlement time are different events, potentially weeks or months apart, and a single bordereau aggregates many policy transactions. A system that only stores "current state" of a policy will struggle here — you need every bind/endorsement/cancellation to leave an immutable transaction record, because that's literally what gets swept into the next bordereau run.

The messy real-world complications

- Endorsements and cancellations generate their own bordereau lines, often negative (return premium on a cancellation, an adjustment on an endorsement) — not updates to the original line. The original bind's bordereau line stays as history; the correction is a new row.
- A single policy can spawn multiple bordereau lines — one per capacity provider on the panel, split by quota share %. If Pelagos has 40% and Syndicate 3123 has 60% of a risk, one bind produces (at minimum) two bordereau line entries, one per provider's bordereau.
- Disputed lines can span multiple periods. A query raised on July's bordereau might not  need a status per line (draft/submitted/queried/agreed/settled), not just a per-bordereaustatus, and the ability to carry unresolved items forward.
- Prior-period corrections happen. If an error is found after a bordereau was already agr via an adjustment line in a future period's bordereau, not by editing history — bordereaux are treated as an append-only ledger, mirroring how the underlying policy transactions should be modeled too.
- Multi-currency. Given this platform spans Bermuda, LatAm, APAC — premium is often writtiders may want figures in a reporting currency (USD), so there's a currency +FX-rate-at-transaction-date concern on every line.

What this implies for the data model

Two aggregates that need to exist, roughly:

- Policy Transaction (bind / endorsement / cancellation / renewal) — the immutable, append-only record of everything that happened at the underwriting level, each one tagged with its financial breakdown     (gross, brokerage, MGA fee, net-per-provider).
- Settlement Period / Bordereau Run — scoped to a cell + capacity provider + period, which pulls together all policy transactions falling in that window that haven't yet been included in a prior bordereau, atracks the bordereau's own lifecycle (draft → sent → queried → agreed → settled) independy transactions' state.
                                                                                                                                                                                                               The key discipline: the bordereau doesn't own the financial truth — the policy transactioerated, periodic view over transactions, with its own separate agreement/settlementworkflow layered on top. If you conflate the two (e.g. store "amount owed to provider" only on the bordereau, not derivable from the transactions themselves), reconciliation and prior-period corrections get very painful.

Ready when you want to move to compliance/referral-authority limits or reporting.