# ADR 0001: Modular app family strategy

## Status

Accepted - 2026-06-15

## Context

SecondBrain is intended to host capabilities from a family of products while
preserving those products as useful standalone applications. ShuffleTask,
PHOODAB, and SurvivalGarden are examples of potential modules in this family;
they are not hard-coded dependencies of the SecondBrain core.

The integration boundary must prevent optional products from becoming required
by the core architecture. It must also distinguish including module code in a
SecondBrain build from enabling that module for a particular user.

In this decision, `Brain.Abstractions` names the shared dependency boundary. The
current repository project that fulfills that role is
`SecondBrain.Abstractions`. `Brain.Core` names the core SecondBrain behavior and
composition that consumes those abstractions.

## Decision

SecondBrain will use a modular app family architecture with these dependency
rules:

- `Brain.Core` and `Brain.Abstractions` must not depend on any concrete module.
- Concrete modules depend on `Brain.Abstractions` for contracts exposed to
  SecondBrain. A module must not require core implementation details.
- The SecondBrain composition root may reference concrete modules to assemble
  the integrated application. This composition responsibility does not move
  into `Brain.Core` or `Brain.Abstractions`.

Optional modules will ship compiled into SecondBrain, but compile-time inclusion
does not mean runtime enablement. Every optional module remains disabled until
the user explicitly opts in. Before opt-in, the module must not expose its
features, begin background processing, or access module-specific user data.

Each product retains its own standalone composition root and release path. A
standalone application must remain independently buildable, releasable, and
usable without SecondBrain. The shared abstraction contracts may be used by
both hosts, but the standalone host does not depend on the SecondBrain host.

Opt-in creates a privacy and data-boundary transition. Module activation must be
explicit, and later implementation decisions must define what data becomes
available to the module, where that data is stored, and how access is revoked.
This ADR does not design the consent or persistence mechanism.

## Consequences

### Positive

- SecondBrain can provide an integrated experience without making optional
  products part of its core dependency graph.
- Standalone products can continue to evolve and release independently.
- A stable abstraction boundary makes dependency direction reviewable and
  supports testing modules outside the SecondBrain host.
- Runtime opt-in provides a clear point at which module features and data access
  may become active.

### Negative and constraints

- SecondBrain builds include code for modules that many users may never enable,
  increasing package size and build coordination costs.
- Shared abstraction changes require compatibility discipline across the app
  family.
- The integrated and standalone composition roots can drift unless both paths
  are tested.
- Module initialization must be designed so disabled modules have no active
  behavior or module-specific data access.
- This decision constrains dependency direction but does not select repository,
  package, deployment, feature-flag, consent, or persistence mechanisms.

## Alternatives considered

### Make concrete modules dependencies of `Brain.Core`

Rejected because it reverses the intended dependency direction, couples core
releases to optional products, and turns examples such as ShuffleTask, PHOODAB,
and SurvivalGarden into required core dependencies.

### Require standalone products to run through SecondBrain

Rejected because it removes independent composition roots and release paths and
would prevent the products from remaining independently buildable and usable.

### Download modules only after opt-in

Deferred rather than selected. Dynamic packaging could reduce initial package
size, but it introduces deployment, compatibility, trust, and update concerns
that are outside this decision. The current decision is compile-time inclusion
with runtime enablement after opt-in.

### Enable every compiled module by default

Rejected because compile-time availability is not user consent. Default
activation would weaken privacy boundaries and make optional behavior implicit.

## Follow-up decisions

- Define module discovery, registration, and lifecycle contracts.
- Define how opt-in state is represented, persisted, revoked, and audited.
- Define module-specific data ownership, access, isolation, and deletion rules.
- Define compatibility and versioning policy for `Brain.Abstractions`.
- Decide repository and package boundaries for shared contracts and concrete
  modules.
- Define verification for both integrated and standalone composition roots.
