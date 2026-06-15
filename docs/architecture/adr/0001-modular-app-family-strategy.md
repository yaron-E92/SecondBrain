# ADR 0001: Modular App Family Strategy

- Status: Accepted
- Date: 2026-06-15

## Context

SecondBrain is intended to host optional product modules while those products
remain useful as standalone applications. ShuffleTask, PHOODAB, and
SurvivalGarden are current examples of modules that may join this app family;
they are not hard-coded dependencies of the SecondBrain core.

The integration model must preserve clean dependency direction. It must also
distinguish a module being present in a SecondBrain build from a user choosing
to enable that module and allow it to access data.

## Decision

Optional modules ship compiled into SecondBrain so a supported distribution can
offer an integrated experience without installing module code after release.
Compile-time inclusion does not enable a module at runtime. Every optional
module remains disabled until the user explicitly opts in.

`Brain.Abstractions`, represented in this repository by
`SecondBrain.Abstractions`, is the dependency boundary between the host and
concrete modules. Concrete modules depend on Brain.Abstractions for integration
contracts. Brain.Core and Brain.Abstractions must not depend on concrete
modules. The SecondBrain composition root is responsible for referencing and
assembling the selected concrete modules without exposing them to the core.

Each module retains a standalone composition root that does not require
SecondBrain. Standalone applications remain independently buildable, usable,
testable, packaged, and released. Their release paths may differ from the
SecondBrain release path.

Opt-in governs runtime activation and is also a privacy and data-boundary
constraint. A compiled module must not assume permission to read, combine, or
write user data before activation and the required consent or permissions have
been established. The detailed consent and permission mechanism is a separate
decision.

## Consequences

### Positive

- SecondBrain can provide integrated module experiences through stable,
  host-owned abstractions.
- Core projects remain independent of optional product implementations.
- Standalone products can continue to evolve and ship without requiring
  SecondBrain.
- Explicit opt-in makes module activation and data access visible user choices.

### Negative

- SecondBrain distributions may include code and increase artifact size for
  modules that a user never enables.
- The host and modules must maintain compatibility with shared abstraction
  contracts.
- Independent composition roots create additional build, test, and release
  combinations to support.
- Runtime enablement, permissions, and data isolation require infrastructure
  beyond compile-time module inclusion.

## Constraints

- Concrete modules may depend on Brain.Abstractions; the dependency must never
  point from Brain.Core or Brain.Abstractions to a concrete module.
- Example modules do not become mandatory core dependencies by being listed or
  included in a distribution.
- A module's standalone build and runtime must not depend on SecondBrain.
- Compile-time inclusion must not bypass explicit runtime opt-in or data-access
  permission checks.

## Rejected Alternatives

### Make concrete modules dependencies of Brain.Core

Rejected because it reverses the intended dependency direction, couples the
core to optional products, and makes adding or removing a module a core change.

### Require standalone apps to run through SecondBrain

Rejected because it removes independent composition roots and release paths
and prevents the products from remaining independently usable.

### Enable every compiled module by default

Rejected because code availability is not user consent and must not imply
permission to activate a feature or cross its data boundary.

### Load all modules only after installation

Rejected as the default strategy because it adds distribution and trust
complexity before the initial integrated model needs it. Future packaging
decisions may introduce optional delivery mechanisms without changing the
dependency boundary established here.

## Follow-up Decisions

- How modules register with the SecondBrain composition root.
- How opt-in state, feature availability, and module lifecycle are represented
  and persisted.
- How consent, permissions, privacy boundaries, and data sharing are enforced.
- How abstraction contract compatibility and versioning are managed.
- Which packaging and deployment mechanisms each platform and distribution use.
- Which automated checks validate dependency direction and standalone builds.
