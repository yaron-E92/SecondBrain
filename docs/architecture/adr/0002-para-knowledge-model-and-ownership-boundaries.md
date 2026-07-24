# ADR 0002: PARA Knowledge Model and Ownership Boundaries

- Status: Accepted
- Date: 2026-07-24

## Context

SecondBrain Core needs a stable vocabulary for knowledge without taking
ownership of task management, household inventory, or other optional-module
domains. It also needs consistent placement and lifecycle rules so consumers do
not infer domain meaning from storage shape or presentation.

[ADR 0001](0001-modular-app-family-strategy.md) establishes that Core and
Abstractions do not depend on concrete modules. The
[dependency direction rules](../dependency-rules.md) map Brain.Core to
`SecondBrain.Domain` and permit concrete modules to depend only on shared
abstractions, not on Core implementations.

This decision records semantic concepts and ownership. It does not prescribe a
database schema, public inheritance hierarchy, synchronization design, or
runtime plugin-loading mechanism.

## Decision

### Core content model

`BrainItem` is the Core-owned identity and lifecycle boundary for retained
knowledge. It supplies the common semantics needed to identify, place, link,
archive, and trace knowledge. Content types remain distinct semantic kinds
rather than subclasses in a deep public inheritance tree.

Core owns these content types:

- **Note** is deliberately authored knowledge. A note kind describes the
  consumer-visible purpose of the note, not its file format or storage shape.
  Initial semantic kinds are General, Meeting, Decision, and How-to. Adding a
  kind must represent meaning that a consumer treats differently; formatting
  variants do not create new kinds.
- **Knowledge Capture** is unprocessed or lightly processed input retained from
  a person, device, import, or external source. It remains distinct from an
  authored Note even when its text looks note-like.
- **Resource Artifact** is retained source material, or a stable reference to
  it, that consumers can consult. It is distinct from a Note that interprets or
  synthesizes that material.
- **Journal** is a chronological, date-oriented record. Its temporal identity
  and ordering are part of its meaning rather than merely presentation
  metadata.

### PARA contexts and placement

Core owns the PARA contexts:

- **Project** is a temporary outcome-oriented context.
- **Area** is an ongoing responsibility or standard to maintain.
- **Resource** is a topic or body of useful reference knowledge.
- **Archive** is an inactive lifecycle state, not a content type or an
  additional primary context.

Every BrainItem has exactly one primary home: one Project, Area, or Resource.
That home determines where the item is managed and prevents ambiguous
ownership. An item may also have any number of secondary links to other PARA
contexts or BrainItems for discovery and navigation. A secondary link does not
move the item, create another primary home, or transfer ownership.

Archiving preserves the item's original content type, primary home, links, and
provenance. For example, an archived Journal remains a Journal and an archived
Resource Artifact remains a Resource Artifact. Restoring an item removes the
archived state without reconstructing or changing its type.

### Capture provenance and resource freshness

Promoting a Knowledge Capture does not mutate it into another content type.
Instead, the operation creates an authored Note or Resource Artifact and
records provenance back to the originating Capture. The Capture retains its
source identity and source metadata and may then be archived according to its
own lifecycle. Provenance remains available when either side is moved, linked,
or archived.

Each Resource Artifact has one freshness state:

- **Draft** means the artifact is incomplete or has not yet been accepted as
  the current reference.
- **Current** means it is the presently trusted reference for its purpose.
- **Outdated** means it is retained for history or provenance but should not be
  treated as current guidance.

Freshness is independent of Archive. A Current artifact can be archived, and an
active Outdated artifact can remain available while a replacement is prepared.

### Ownership boundaries

Core owns BrainItem identity and lifecycle, the four content types above, PARA
placement, secondary links, provenance, and Resource Artifact freshness.

ShuffleTask continues to own tasks and task-specific behavior, including
workflow state, prioritization, scheduling, recurrence, and shuffling. A task
may link to a Core BrainItem through an integration contract, but it does not
become a Note or a PARA Project, and Core does not model or depend on
ShuffleTask behavior.

PHOODAB continues to own pantry and household inventory, replenishment,
shopping, locations, and durable items. Those concepts do not become Core
Resources, Areas, or Resource Artifacts merely because they can be linked to
knowledge. Core does not model or depend on PHOODAB behavior.

Concrete modules may expose opaque references and integration capabilities
through `SecondBrain.Abstractions`. Core must not reference a concrete module or
interpret module-owned state. Only an application composition root may combine
Core and module experiences, preserving the dependency direction established
by ADR 0001.

## Consequences

### Positive

- Consumers share clear semantics for authored, captured, sourced, and
  chronological knowledge.
- A single primary home gives each item an unambiguous management context while
  secondary links support cross-context discovery.
- Provenance survives capture processing, and freshness can be evaluated
  independently from archival state.
- Optional modules can relate their data to knowledge without moving their
  domain rules into Core.

### Negative

- Consumers must handle links and lifecycle separately from content type.
- Capture promotion retains two related items instead of replacing one record.
- Integrations need explicit contracts to relate module-owned data to
  BrainItems.

## Alternatives considered

### Treat every captured value as a Note

Rejected because it erases the difference between ingested source material and
deliberately authored knowledge and makes provenance unreliable.

### Make Archive a content type or a second primary home

Rejected because archiving should not change what an item is or erase the
context in which it was managed.

### Put task and household concepts in Core

Rejected because ShuffleTask and PHOODAB own those domain rules. Moving them
into Core would reverse the dependency boundary and couple all consumers to
optional products.

### Define a public class hierarchy and persistence schema now

Rejected because this decision is semantic. Committing to inheritance or
storage before use cases require it would turn consumer distinctions into
premature framework constraints.

## Follow-up decisions

- Define Core domain types and invariants that implement these semantics.
- Define integration contracts for opaque links between BrainItems and
  module-owned entities.
- Decide how permissions, deletion, and provenance visibility apply across
  module boundaries.
