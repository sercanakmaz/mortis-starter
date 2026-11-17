# Layers and hosts

Four layers and three kinds of host. Each layer has one job. A rule that does not fit its layer's job is in the wrong place.

| Layer | Its single job | May depend on | Must never contain |
|---|---|---|---|
| **Shared** | The base types everything builds on: entity, aggregate root, event base, command contracts, pipeline, dispatcher, repository and unit-of-work contracts, the exception family | nothing of ours | a database library, a business rule, a framework type |
| **Domain** | The business decisions: what is allowed, what is forbidden, what changed | Shared | storage code, HTTP, a message broker, the clock read directly, a random id read directly |
| **Application** | Coordination of one use case: load the aggregates it needs, call their operations or a domain service, save, map the result; one event in, one command out per handler (an event may have several handlers) | Domain, Shared | a business rule, a query, a loop over aggregates that decides something |
| **Infrastructure** | Implementations of what Shared and Domain declare: repositories, unit of work, bus, outbox, clock, id source | Domain, Shared | a business rule, a decision about whether something is allowed |
| **Hosts** (Api, Console, Worker) | Turn the outside world into a command and fill its context; turn the result or the failure back into the outside world's language | Application, Infrastructure | a business rule, a transaction, a direct call to a repository |

## Shared

Holds the building blocks that have no storage knowledge, so Domain and Application can use them without learning how data is saved. Everything in Shared is a contract or a small base type. Nothing in Shared knows what an order or a customer is.

## Domain

- An **aggregate** enforces its own rules. Its operations check what must hold, change its own state, and raise events. Nobody else changes its state.
- A **domain service** holds business logic that does not fit inside one aggregate: a decision that needs several entities, or an answer from another system through an anti-corruption layer. It may load the entities it needs through repositories. It is optional: most use cases need none.
- An **anti-corruption layer** is a port declared here, in the domain's own words, for another system (a tax service, a credit bureau, a legacy database). Infrastructure implements it and translates the other system's model, so that model never leaks into Domain.
- A **repository interface** is declared here, named after what the domain asks ("find the open orders of a customer"), not after how the answer is stored.
- A **domain event** is a fact in the past tense, raised by an aggregate, carrying the ids and the context of the change that raised it.

## Application

- A **command handler** (the use case, also called an application service) runs one user action: it loads the aggregates the action needs, calls their operations or a domain service, saves them, and maps the result to a view. It may load and change more than one aggregate when the action itself is about all of them (see the cheat sheet in [placement-guide.md](placement-guide.md)); a change that is a *consequence* of the action goes to an event handler instead. It holds no business rule.
- An **event handler** receives one event and sends one command. The reaction then runs through the same pipeline as a request. Several reactions to one event are several handlers, each in the folder of the aggregate that reacts.
- A **view** (DTO) is a flat description of a result, built by a mapping function that names every field it reads.

If a handler needs an `if` that is about the business, the aggregate is missing an operation.

## Infrastructure

Everything that touches storage, a broker, the clock or the id source lives here and implements something declared in Shared or Domain. Swapping a database or a broker means changing only this layer and the registration that wires it.

## Hosts

A host does three things: authenticate, fill the command's context (who, which correlation, which tenant, which language), and translate the outcome. A host never opens a transaction and never calls a repository. A host differs from another only in how the outside world reaches it:

| Host | Reaches it through | Fills the context from |
|---|---|---|
| Api | requests | the credential, the request headers |
| Console | a name and a JSON argument | a fixed console actor |
| Worker | a timer or a message | the message's own context, or a system actor |

## Direction of dependencies

Hosts → Application → Domain → Shared. Infrastructure → Domain and Shared. Nothing points at a host. Application never references Infrastructure; the host's registration connects the two.
