# Notion export migration mapping, version 1

Status: Approved migration contract
Specification version: `1.0`
Fixture format version: `1`

This specification defines how a Notion export may be interpreted by a future
SecondBrain importer. It does not define an importer, persistence schema, or
public API. Version 1 is immutable once an import uses it; incompatible mapping
changes require a new version directory.

## Source selection and identity

Only the canonical export for each database is eligible. Ignore a file when its
base name ends in `_all.csv` (case-insensitive); those files are duplicate
Notion views, not additional databases. Ignore template rows identified by an
`Is template` value of `true`.

Use the Notion database ID and page ID from export metadata as source identity.
Never infer identity from a directory name, CSV filename, title, or row number.
An audit may use a CSV filename only as a non-authoritative database
classification hint so the report can describe the apparent section. A
filename-only Core classification remains ambiguous until authoritative export
metadata supplies its database identity. A recognizable Tasks, Chores, or
PHOODAB filename hint may still be conservatively marked module-owned/excluded;
it must never be promoted into Core content.
For every imported record, retain this provenance:

- source system: `notion`;
- specification version: `1.0`;
- Notion database ID;
- Notion page ID;
- source filename for diagnostics only.

Within canonical files, identical rows with the same page ID collapse to one
record. Repeated IDs with conflicting content are rejected and reported for
manual resolution. A filename change never creates a new identity.

## Database and field mapping

All property names below are logical names. An importer may accept Notion's
localized display labels only through an explicitly versioned alias table.

| Notion database or row classification | Eligibility | Explicit Core target | Field mapping |
| --- | --- | --- | --- |
| Projects | Canonical rows only | `Project` | Name → name; Status → status; Priority → priority; Start/Due → dates; Archived → archive lifecycle |
| Areas | Canonical rows only | `Area` | Name → name; Archived → archive lifecycle |
| Notes | Canonical, non-template rows | `BrainItem` with kind `Note` | Name → title; Content → content; Note kind → note kind; Placement → primary placement; Tags/Links → normalized relationships; Archived → archive lifecycle |
| Ideas | Canonical rows only | `BrainItem` with kind `Idea` | Name → title; Content → content; Maturity → idea maturity; Placement/Tags/Links/Archived → common fields |
| Journals | Canonical rows only | `BrainItem` with kind `JournalEntry` | Name → title; Content → content; Entry date → entry date; Placement/Tags/Links/Archived → common fields |
| Captures | Canonical rows only | `BrainItem` with kind `KnowledgeCapture` | Name → title; Content → content; Source type/URL/citation → capture provenance; Reminder/processing state → capture fields; Placement/Tags/Links/Archived → common fields |
| Resources, classified `Topic` | Canonical rows only | `ResourceTopic` | Name → name; Archived → archive lifecycle |
| Resources, classified `Artifact` | Canonical rows only | `BrainItem` with kind `ResourceArtifact` | Name → title; Content → content; Artifact kind/freshness/review date → resource fields; Placement/Tags/Links/Archived → common fields |
| Resources, classified `Note` | Canonical, non-template rows | `BrainItem` with kind `Note` | Name → title; Content → content; Note kind → note kind; Placement/Tags/Links/Archived → common fields |
| Resources, missing or unknown classification | Never automatic | No target until classified | Defer the row and report `ambiguous-resource-classification-required` |

Resource Topic, Resource Artifact, and Note are intentionally distinct
destinations. A Resources filename, title, URL, or attachment is not enough to
choose among them.

## Common fields

- Trim tags, compare them case-insensitively, discard empty values, and retain
  the first spelling of each unique tag. A slash-delimited tag is not
  automatically a hierarchy.
- Resolve primary placements and links by Notion page ID after all eligible
  canonical rows have been classified. Do not resolve by filename or title.
- An explicit `Archived=true` applies the target's archive lifecycle after its
  type and primary placement are established. Missing, empty, or `false` means
  active. Archive never changes the content type or former placement.
- Timestamps use the exported ISO-8601 value. No timestamp may be synthesized
  from filesystem metadata.

## Exclusions

Tasks and Chores belong to ShuffleTask and are always excluded from Core.
Rows or databases owned by PHOODAB, including pantry, shopping, household
inventory, and replenishment data, are always excluded. An importer must report
`module-owned-shuffletask` or `module-owned-phoodab` and must not coerce these
records into Notes, Projects, Areas, or Resources.

## Links and unresolved references

Eligible relations are resolved by Notion page ID to an imported Core identity.
Preserve the declared relation type when one is present; otherwise use the
version 1 default `Related`.

If a target ID is missing, excluded, ambiguous, or outside the export, keep the
source record and emit an unresolved-link diagnostic containing the source and
target Notion IDs. Do not create a placeholder Core record, fall back to a
matching title, or silently discard the relation. A later, explicit resolution
may attach the link without changing either source identity.

## Sanitized fixtures

`tests/fixtures/notion-export/v1/representative-export.json` is a compact,
CSV-normalized manifest of representative Notion export files and rows.
`expected-migration.json` records the required imported, skipped, deferred, and
unresolved decisions. The fixture is deliberately synthetic, uses reserved
`.invalid` URLs, and contains no personal export content.

The fixture covers all canonical databases, a duplicate `_all.csv` view, a
template, ambiguous Resource classification, an unresolved relation, an
archived row, and ShuffleTask and PHOODAB exclusions.
