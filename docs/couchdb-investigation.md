# CouchDB Completed-Meet Investigation

## Scope and method

Parsed the complete local `_all_docs` response in `local-data/completed-meet.json`: `total_rows` is 402 and all 402 rows contain a document. The report uses aliases such as `D002` based only on row order; original document IDs and all personal or payment values are omitted. No external requests were made, and the fixture was not modified.

This completed competition contains 402 documents. The earlier 327-document observation relates to a different, upcoming competition (mnk13ga524h1). These counts should not be interpreted as changes within the same database.

## Dataset Context and Limitations

This investigation analyses a **single completed LiftingCast competition**.

- Competition ID: `m80in4tnzkgx`
- Competition name: `2026 Powerlifting America Haunted Platform`
- Competition date: 18 October 2026
- Source: Read-only CouchDB `_all_docs` response
- Local fixture: `local-data/completed-meet.json`
- Snapshot size: 402 documents

This dataset represents a competition that has already taken place. It is being used to understand the structure and relationships of LiftingCast documents.

**It is a reference dataset, not a universal representation of every competition or a live-data sample.**

### Important distinctions

Upcoming competitions may contain:
- Registered competitors and division configurations
- Preassigned flights, sessions and platforms
- Empty or incomplete attempt records
- No recorded lifting decisions

Live competitions may contain:
- Changing attempt weights
- Updated judge decisions and results
- Active platform and clock state
- Incomplete totals and provisional placings

Completed competitions typically contain:
- Populated attempt results
- Final or near-final lifter information
- Historical changes
- Inactive platform state

These are expected lifecycle differences, not fully verified schema guarantees.

### Development implications

- Never hardcode document counts or assume every meet contains nine fully populated attempts per lifter.
- Do not infer live platform behaviour from a completed competition snapshot.
- Treat absent, null and incomplete fields as legitimate states until their semantics are established.
- Do not assume the `changes` array represents a complete event history.
- Keep competition-specific observations separate from general schema findings.
- Confirm behaviour using an upcoming competition and an actively running competition before implementing live synchronization.

The application must support multiple competitions and their different lifecycle stages. It must not be designed specifically around this completed meet.

## Document classification

Classification is based on repeated field structures and references, not verified application schema. Example IDs below are anonymous row aliases.

| Likely type | Count | Example | Structural evidence |
|---|---:|---|---|
| Attempt records | 252 | D002 | `_id`, `_rev`, `lifterId`, `liftName`, string `attemptNumber`, `weight`, string `result`, `decisions`, `changes`, `createDate`; some optional fields vary |
| Lifter profiles | 28 | D365 | Identity/profile fields, `divisions`, `flight`, `session`, `lot`, `platformId`, equipment/rack fields, and `changes`. Personal and payment-related fields are present but their values are excluded. |
| Division/scoring configuration | 110 | D254 | `gender`, `lifts`, `rawOrEquipped`, `scoreBy`, `usaplDivisionCode`, and `weightClasses`; nested weight-class entries have `_id`, `maxWeight`, and `name` fields |
| Platform search/index records | 5 | D398 | `name`, `platformId`, `search`; exact role is uncertain |
| Platform runtime/configuration | 1 | D394 | `clockState`, `clockTimerLength`, `currentAttemptId`, `barAndCollarsWeight`, `name`, and `liveStreamLink` |
| Referee/card/decision records | 3 | D395 | `platformId`, `position`, `decision`, and `cards`; `decision` and `cards` are null in these records |
| Competition metadata/configuration | 1 | D393 | Competition date/format, federation, units, display/payment status, and other configuration fields |
| Registration configuration | 1 | D364 | Entry limits, fee fields, registration flags, custom questions, description/disclaimer fields |
| CouchDB design document | 1 | D001 | `_id`, `_rev`, and `validate_doc_update`; excluded from ordinary document samples |

The profile and attempt groups have small structural variants, including optional fields and numeric weights represented as either integers or floats. The division/scoring group contains 818 nested weight-class entries across its 110 documents.

## Upcoming vs completed comparison

No upcoming competition `_all_docs` fixture is present locally; only the completed-meet fixture is available. The prior upcoming-meet observation reported 327 documents, an update-sequence prefix, an empty `since=now` changes response, and documents with `_id`, `_rev`, `liftName`, `attemptNumber`, `lifterId`, `changes`, and `createDate`. Those field names also occur in the completed fixture’s attempt records. This confirms a field-name overlap, not identical document structures or behavior.

### Confirmed observations

- In the completed fixture, 240 of 252 attempt records have `weight`, `result`, and `decisions`; 12 lack all three. The completed platform/runtime record has a null `currentAttemptId`.
- The earlier upcoming observation does not provide comparable per-document counts, field types, or null rates. The two competitions are different meets, so even a full comparison would not isolate lifecycle as the cause of differences.
- The completed fixture also contains lifter profiles, division/scoring configuration, platform-related documents, and custom `changes` histories; availability and field populations in the upcoming meet were not established.

### Hypotheses and unknowns

The overlapping attempt-related fields suggest both competitions use related document shapes. Missing result fields could represent unrecorded attempts, but the 12 incomplete completed-meet records do not establish how upcoming placeholders behave. Attempt edits and platform state may change during a live meet, but a completed snapshot and a `since=now` response cannot show those transitions.

There is not enough evidence to identify which fields are commonly null before results are recorded, whether the same fields are populated during the event, or whether platform clocks and `currentAttemptId` behave consistently across meets. These require an upcoming fixture and repeated manual observations of one active competition.

### Minimum first-version data

Keep document IDs and revisions plus the opaque CouchDB checkpoint. For a live attempt view, retain the current attempt fields `lifterId`, `liftName`, `attemptNumber`, `weight`, `result`, and `decisions` as optional values; join only the profile fields needed for display/order and division, and the platform runtime fields needed for current-attempt/clock display. Keep unit and scoring configuration needed to interpret results. Do not require completed results, persist the custom `changes` history, or calculate totals/placings until their semantics are verified.

## Relationships and attempt representation

- **Attempt → lifter:** Every one of the 252 attempt records has a `lifterId` matching a profile document’s `_id`. There are 28 distinct referenced profiles, with exactly nine attempt records per profile. The three distinct `liftName` categories and three string-valued attempt numbers form 252 unique lifter/lift/attempt combinations. This is strong evidence of the join key and attempt slots, but not proof of how missing slots should be interpreted.
- **Lifter → division:** Profiles contain 55 division entries. These reference 11 distinct division-config document IDs, and every entry matched a config document in this fixture. Seven distinct `declaredAwardsWeightClassId` values matched keys in the nested `weightClasses` maps.
- **Platform:** All 36 `platformId` references share one value in this fixture; that reference matches the runtime/config document ID and the `platformId` field of all five search/index records. Profiles and decision/card records carry this reference. Attempts do not carry a platform ID directly, so associating them with a platform requires following the lifter profile.
- **Flight/session:** `flight` appears on profiles and has two distinct string values; `session` is numeric. No separate flight-document structure was found. The values did not match document IDs, so they appear to be attributes rather than document references, but that interpretation needs live testing.
- **Weights and outcomes:** 240 attempts contain numeric `weight`, string `result`, and a `decisions` object; 12 attempt records lack all three. Of the numeric weights, 143 are integers and 97 are fractional values. The competition configuration’s units field identifies kilograms.
- **Decision detail:** Each populated `decisions` object has `head`, `left`, and `right` entries. Each entry contains a string `decision` and a `cards` object with Boolean values. The 240 result strings have two distinct labels: 192 are success-term-like and 48 failure-term-like by their text. The labels appear to encode attempt outcomes, but their exact meaning and how they relate to judge decisions should be confirmed before calculating a successful lift.
- **Totals and placings:** No field names corresponding to total, place, rank, or points were found recursively. `scoreBy` appears in the division/scoring configuration, but no lifter total or placing is stored under an identifiable field. Totals and placings are therefore likely derived from attempts and competition rules; this is not proof that no opaque or differently named aggregate exists.

## History, updates, and edge cases

The document property named `changes` is an application-level history array; it is distinct from CouchDB’s `_changes` endpoint.

- It occurs on 391 documents. Of those arrays, 120 are empty and 271 contain entries; the longest observed array has 14 entries. There are 1,019 object entries in total.
- Entries consistently have `attribute`, `rev`, `timeStamp`, and `value`. `attribute` is a string in 1,007 entries and an array in 12; `value` types are string, number, object, array, or Boolean. Attribute names/paths generally match current document fields.
- All 1,019 `timeStamp` strings parse as offset-aware ISO timestamps with a zero UTC offset. In every one of the 271 documents with history, timestamps are nondecreasing in array order and the final history timestamp is later than `createDate`. This observed ordering does not establish whether history is complete or what `createDate` semantically records.
- Of 970 last entries compared per string-named attribute, 966 values match the current top-level field and four do not. This suggests entries often record a resulting field value, but exceptions mean it is unsafe to treat the array as a definitive event log without further checks.
- Each history entry’s `rev` is a revision-like string, but the last entry’s `rev` did not equal the document’s CouchDB `_rev` in any of the 271 non-empty histories. Do not treat these as interchangeable checkpoints or CouchDB revision tokens.
- One profile document contains a one-entry `_conflicts` list. No row or document in this response has an explicit deleted marker. Since ordinary `_all_docs` results may omit tombstones, this does not establish that the database has no deleted documents.
- The runtime record has a numeric `clockTimerLength` and `clockState`, but its `currentAttemptId` is null in this completed-meet snapshot. Clock units, state transitions, and linkage to an attempt were not established.

## Minimal synchronization recommendation

For a future live synchronizer, start with a manually verified, read-only `_changes` batch using the exact opaque `last_seq` from the prior response. For each returned document ID/revision, fetch the current document as needed, apply an idempotent update, handle deletion/conflict indicators, and only then save the returned checkpoint alongside the applied batch. Do not parse or increment sequence tokens. The current fixture supports a compact read model built from attempt, lifter, division/scoring, and platform/runtime records; avoid persisting the raw `changes` arrays or personal/payment fields unless a confirmed feature requires them.

Live attempt tracking would at least need attempt ID, lifter link, lift and attempt number, weight, verified outcome/judge decision, and profile division/flight/session/lot data. Warm-up or clock timing additionally needs verified platform state and scheduling semantics. Projected totals require verified outcome rules and scoring configuration; placings also require verified division/tie-breaking rules. The fixture alone does not establish these calculations.

## Performance and strategy follow-up

The existing completed fixture was rechecked for calculation inputs. These observations describe this meet only:

- Attempt `liftName` values are `squat`, `bench`, and `dead`; attempt numbers are strings `"1"`–`"3"`. Of 252 attempts, 192 have result `good`, 48 have `bad`, and 12 have no result and no selected weight. Populated weights are JSON integers or decimals. `good` and `bad` are the only observed result strings.
- All 28 lifter profiles have numeric bodyweight, `gender`, `platformId`, `flight`, `session`, and `lot`. The competition metadata has `units: "KG"`.
- Division configuration documents have `gender`, `rawOrEquipped`, `scoreBy`, `lifts`, and `weightClasses`. The fixture contains three `lifts` combinations: squat/bench/dead (38 configs), bench only (36), and bench/dead (36). Configured weight-class entries expose `_id`, `name`, and numeric `maxWeight`; `9999` appears for an `"All"` class and should not be treated as a normal class limit.
- Gender values in division configurations are `MALE` and `FEMALE`; equipment categories are `RAW` and `EQUIPPED`; `scoreBy` is `TOTAL` or `IPF_POINTS`. The lifter's `divisions` entries connect the lifter to a division document and a declared weight-class ID. Opponents should be compared by those identifiers and compatible configuration, not merely equal numeric limits.
- The current synchronization allowlist already includes these calculation fields and the nested division/weight-class keys. It excludes birth dates and payment/contact data; no allowlist expansion was needed for this first calculation pass.

Only exact observed `good` outcomes are treated as successful; `bad` is excluded, and unresolved attempts remain unknown. A positive selected unresolved weight can support a clearly hypothetical baseline projection; an absent weight is never filled in. Pound conversion is supported by the calculation code, but this fixture only verifies kilograms. No additional real meet was available to validate other unit/category representations.

The repository does not contain the original scoring JavaScript reference or its coefficient tables. `scoreBy` identifies source configuration, not the exact formulas or coefficients for IPF GL, DOTS, Wilks, or legacy IPF. Those scores and Best Overall ranking remain unavailable until the reference is supplied; coefficients must not be inferred.

The completed snapshot also cannot establish live lifting order. Attempt documents expose lift, round, selected weight, and lifter ID; profiles expose platform, flight, session, and lot; the runtime document exposes current state but no verified upcoming queue. These fields do not prove a live sort order, and this completed meet has no current attempt. The interface therefore reports order unavailable and does not estimate attempts ahead or wait time. Verify ordering against an actively running meet before implementing such estimates.

## Outstanding questions for live testing

1. Does a saved full `last_seq` retrieve the expected next changes, and how does CouchDB behave at batch limits, for repeated updates to one document, and for empty batches?
2. Do `_changes` result IDs/revisions/deletion flags track the same events represented by each document’s custom `changes` array?
3. What do `result`, `decision`, and `cards` values mean, and what rule determines a successful attempt?
4. Are the 12 incomplete attempt records placeholders, withdrawn attempts, or another state?
5. Does `currentAttemptId` point to an attempt document during a live meet, and what are the units/transitions for `clockTimerLength` and `clockState`?
6. How are flight/session order, weight-class selection, totals, scoring, and placings determined, including ties and incomplete results?
7. Why does this fixture contain 402 rows while the earlier observation reported 327, and are deleted documents or unresolved conflicts visible through the read-only endpoints?
