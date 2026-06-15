# Dependency direction rules

This document defines the allowed project-reference graph for SecondBrain. The
rules apply to project references between governed projects; framework and
third-party package dependencies are outside this graph.

The architecture issue terminology maps to the current projects as follows:

| Architecture role | Current or expected project |
| --- | --- |
| `Brain.Abstractions` | `SecondBrain.Abstractions` |
| `Brain.Core` | `SecondBrain.Domain` and any future `SecondBrain.Core` project |
| `Brain.Application` | `SecondBrain.Application` |
| `Brain.Persistence` | `SecondBrain.Persistence` |
| Application host | `SecondBrain.App` |
| Concrete modules | ShuffleTask, PHOODAB, SurvivalGarden, and future module projects |

## Allowed graph

```text
SecondBrain.App ---------> Concrete modules
      |                         |
      v                         v
Persistence ------------> Abstractions
      |
      v
Application ------------> Core
      |                     |
      +---------------------+
                  |
                  v
             Abstractions

Standalone module app ---> Its concrete module ---> Abstractions
```

An arrow means that the project at the arrow's origin may reference the project
at its destination. References within a layer are permitted when a layer is
split into multiple projects.

| Referencing role | May reference SecondBrain roles |
| --- | --- |
| Abstractions | None |
| Core | Abstractions, Core |
| Application | Abstractions, Core, Application |
| Persistence | Abstractions, Core, Application, Persistence |
| App | Abstractions, Core, Application, Persistence, App, ConcreteModule |
| ConcreteModule | Abstractions, ConcreteModule |
| StandaloneApp | Abstractions, ConcreteModule, StandaloneApp |

These are permissions, not requirements. A project does not have to reference
every role listed in its row.

## Prohibited directions

- Abstractions never references Core, Application, Persistence, presentation,
  or a concrete module.
- Core never references Application, Persistence, App, or a concrete module.
- Application never references Persistence, App, or a concrete module.
- Persistence is an outer layer and is not referenced by an inward layer.
- Concrete modules depend on Abstractions and never become dependencies of an
  inward layer. Only an application composition root may assemble them with
  SecondBrain.
- Standalone applications keep their own composition roots. They reference
  their concrete module and shared abstractions directly and remain buildable,
  releasable, and usable without `SecondBrain.App`.

## Adding projects

The architecture tests recognize the current projects and conventional
`Brain.*`/`SecondBrain.*` names. A new production project with another name must
declare its role in the project file:

```xml
<PropertyGroup>
  <SecondBrainArchitectureLayer>ConcreteModule</SecondBrainArchitectureLayer>
</PropertyGroup>
```

Supported values are `Abstractions`, `Core`, `Application`, `Persistence`,
`App`, `ConcreteModule`, and `StandaloneApp`. Unclassified production projects
fail the architecture test so that their dependency direction is decided
explicitly.

The automated checks live in `tests/SecondBrain.Architecture.Tests` and run as
part of the root solution test command.
