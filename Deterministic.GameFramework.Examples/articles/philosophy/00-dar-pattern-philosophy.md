# DAR Pattern Philosophy

## Composition Over Inheritance in Game Architecture

In game programming, the principle of **composition over inheritance** is preferred due to the high variability of game entities that lack hierarchical dependencies.

Consider a game design requirement:

> "A Wolf enemy, when approaching the player, performs an attack, reducing the player's health by X and with probability Y applying a bleeding status"

Abstracting from specifics, we get the pattern:

**"An object, under a condition, performs an action, changing the state of another object and conditionally performing an additional action"**

We identify three base components:

- **Domain** — an area of responsibility with clear boundaries, encapsulating state and logic for its modification ("Wolf", "Player", "Inventory"). The term emphasizes that this is not a passive data container (like Entity in ECS) and not a behaviorless component, but an active area of game logic directly reflecting game design concepts.
- **Action** — an operation executed within a domain, often expressing state changes defined by the domain ("deal damage", "apply effect")
- **Reaction** — conditional logic determining when and how an action is executed ("on approach", "with Y% probability")

The DAR pattern **actively uses inheritance** to define types of domains and actions, creating a clear hierarchy of base entities (`CharacterDomain`, `DamageAction`, `InventoryDomain`). However, **behavioral variability** is achieved through composition of reactions, not through creating subclasses for each combination of capabilities. Instead of a hierarchy `WolfEnemy → WolfWithBleedEnemy → FastWolfWithBleedEnemy`, a single `WolfEnemy` class is created with attached reactions. The domain acts as a container for reactions, providing a compositional approach to building variable game logic while preserving the advantages of a typed hierarchy through inheritance.

This architecture ensures that **complex game design concepts have a clear place in the codebase**. Conditional damage modifiers, probability-based effects, quest restrictions, character-specific abilities — each has a predictable location: domains represent game entities, actions represent state changes, and reactions represent conditional logic. The architecture directly reflects game design intent, making it easy to locate where any game mechanic is implemented and where new mechanics should be added.

## Action Validation

When designing an action system, there's a need to verify executability before application. The `_IsExecutable()` method solves this task, preventing attempts to execute impossible actions.

Consider an example: an action to set item quantity in inventory. Before adding items, `_IsExecutable()` checks if there's enough free space, calculating the difference between desired and current quantity. Without such validation, the system could attempt to add items to a full inventory, leading to incorrect state.

Separating validation logic (`_IsExecutable()`) and execution (`ExecuteProcess()`) makes code more readable: the first method answers "is it possible?", the second — "how to execute?". Values calculated during validation (e.g., number of items to add) can be stored in class fields and reused during execution, avoiding duplicate calculations.

## Actions Execute Through Domains

In DAR architecture, actions don't execute independently but are passed to the domain for execution. The action's `Execute()` method accepts a domain and delegates actual execution to that domain's `Execute()` method.

This solution provides **centralized control**: the domain becomes the single entry point for all state modifications. Instead of directly changing internal data (which would violate encapsulation), the domain itself decides how to apply the action.

The key advantage is **integration with the reaction system**. Since all actions pass through the domain's execution pipeline, this is where all reactions are uniformly invoked: checking abort conditions, pre-processing logic (before), and post-processing (after). If actions executed themselves, reaction logic would need to be duplicated in each action class.

This architecture creates a **unified pipeline** for executing all operations, allowing the domain to log actions, build history for rollback, and collect statistics. For debugging, it's sufficient to set a breakpoint in the domain's execution method to see all operations on that domain.

Finally, this corresponds to the **inversion of control** principle: the action describes "what needs to be done", the domain decides "how". This allows different domain types to interpret the same actions differently, making the system more flexible and extensible.

## Reaction Type Separation

When designing the reaction system, there's a need to react to actions at different moments in their lifecycle. Separation into four reaction types — `Prepare`, `Abort`, `Before`, and `After` — is driven by different tasks that need to be solved when processing game events.

It's important to note the **philosophical difference** between the action's `_IsExecutable()` method and `Abort` reactions. Both mechanisms serve to validate action executability but are responsible for fundamentally different validation aspects.

The `_IsExecutable()` method encapsulates **internal validation** of the action — checking whether execution is possible based on the action's own logic and target domain state. This is the question "Can **I** (the action) be executed in principle?". For example, an action to add an item to inventory checks if there's enough physical space — this is knowledge the action possesses about inventory structure. An action to set health checks if the new value is within acceptable bounds. This validation is part of the action's contract and doesn't depend on external context.

`Abort` reactions, conversely, represent **external validation** — checking contextual conditions that the action itself shouldn't know about. This is the question "Do external systems **allow me** (the action) to execute?". For example, a player death action might be aborted by a reaction checking for an immortality effect. The death action itself shouldn't know about the effect system, quest system requiring player invulnerability, or debug mode where death might be disabled. All these checks are implemented through `Abort` reactions, allowing different systems to independently impose their restrictions.

This separation corresponds to the **separation of concerns principle**: the action is responsible for its internal consistency, while reactions handle compliance with external rules and restrictions. This makes the system modular: you can add a new protective effect system that will abort certain actions through `Abort` reactions without modifying the action classes themselves.

`Prepare` reactions execute **before actual application** of the action and serve to prepare data and modify parameters. Consider an example with granting experience for killing an enemy: a `Prepare` reaction on the enemy death action can read the enemy type, calculate the experience amount, and write this value to an action field. Also at this moment, quest progress and raid statistics can be updated. Critically, these operations occur before the enemy actually dies and its data may become unavailable. `Prepare` reactions allow actions to be more flexible: instead of hardcoding experience granting in death logic, we delegate this to reactions that can vary depending on context.

`Before` reactions execute **after validation passes but before the main action logic**. They can perform preparatory operations that require the action to be valid but need to happen before state changes.

`After` reactions trigger **after successful execution** and are intended for handling side effects. When a player dies, the death action is already applied, game state changed, and now we can safely perform accompanying operations: send an event to Firebase analytics, check conditions for showing ads, update statistics. Importantly, these operations don't affect the death action itself — they are consequences of an already occurred event. Using `After` guarantees that side logic executes only if the action passed all checks and was successfully applied.

This separation creates a **clear processing pipeline** for actions: first internal validity is checked (`_IsExecutable`), then external restrictions (`Abort`), after which data is prepared (`Prepare`), pre-processing occurs (`Before`), the action itself executes, and finally consequences are handled (`After`). This allows different game systems to independently react to one event at the appropriate moment, without creating dependencies between each other.

## Tree Composition Advantage

The decision to organize domains in a tree structure is a key architectural choice ensuring flexibility and scalability. Instead of creating a flat collection of independent domains, hierarchical composition is used where each domain can contain subdomains, forming a tree of game entities.

The first and most important advantage is **automatic action routing**. When an action executes on a domain, the system checks if the domain type matches the action's required type. If the current domain isn't the target, the action is automatically redirected to the appropriate subdomain. For example, if an add-item action executes and the current domain is the player, the system automatically finds the inventory domain among the player's subdomains and executes the action there. The developer doesn't need to explicitly specify the path to the needed domain — it's sufficient to call the action on the root domain, and the system determines the correct recipient.

The second critically important advantage is **reaction propagation through hierarchy**. When an action executes on a domain, not only its own reactions are checked but also reactions of all parent domains up the tree. This creates a powerful mechanism for encapsulating logic at different abstraction levels. For example, you can create a reaction at the root game domain level that logs all actions to an analytics system, regardless of which specific subdomain they occur in. More specific reactions can be placed at concrete domain levels. A reaction to adding an item to a quick slot will be at the quick slot system level, while a global reaction to any inventory changes — at the player level.

This **scope system** for reactions solves the problem of logic duplication. Instead of each inventory domain (main inventory, bank, trade window) registering identical reactions for common rules, these rules are registered once on the parent domain and automatically apply to all descendants. This significantly simplifies maintenance: changing a global rule requires modifying only one reaction at the top level, not multiple duplicating reactions in different places.

The tree structure naturally **reflects game entity composition**. A player contains inventory, inventory contains slots, slots contain items. An enemy contains a health system, attack system, loot system. Each of these components is an independent domain that can be reused. For example, the same "Health" domain can be a subdomain of player, enemy, or destructible object. Such composition corresponds to the separation of concerns principle: each domain is responsible only for its part of functionality.

An important aspect is **navigation simplification** through the data structure. The `GetFirst<T>()` and `GetAll<T>()` methods allow finding needed subdomains by type or arbitrary condition, both recursively (through the entire tree) and only among direct descendants. This eliminates the need to store explicit references between domains: instead of the player holding an `inventory` field, you can get the inventory at any time by calling `player.GetFirst<InventoryDomain>()`. This makes the system more flexible — subdomains can be added and removed dynamically without needing to update multiple references.

Hierarchical structure also provides **natural lifecycle** for domains. When a parent domain is removed, all its subdomains are automatically removed along with their reactions. This prevents memory leaks and "dangling" reactions trying to process already non-existent entities. For example, when an enemy dies, its root domain is removed, automatically cleaning all health systems, attack, AI, and associated reactions.

Finally, tree composition allows creating **reusable modules** of game logic. You can develop a complex composite domain (e.g., "Equipment with Enchantments") and then use it as a subdomain in different contexts — for player, companions, bosses. All internal logic of equipment subdomain interaction remains encapsulated, while external systems work with it through a unified action interface.

## Upward Reaction Propagation

The decision to propagate reactions **upward through domain hierarchy** rather than downward is a fundamental architectural choice based on several core design principles.

First, upward movement corresponds to the principle of **adding context**. When an action executes on a specific domain, it occurs in a certain context — this domain is part of a larger system. Checking parent domain reactions means sequentially expanding context from specific to general. For example, adding an item to a specific inventory slot is first checked at the slot level (can this item type be in this slot), then at inventory level (is total weight exceeded), then at player level (are there quest restrictions), and finally at root game domain level (are there global cheats or debug mode). Each level adds its layer of rules without knowing details of lower levels.

Downward movement, conversely, would create an **encapsulation violation problem**. If reactions propagated to subdomains, it would mean an action on the parent domain automatically affects all child domains, even if they're unrelated to this action. The parent domain would be forced to know about the internal structure of all its subdomains and account for their reactions. For example, a player-level action would trigger reaction checks in inventory, health system, quests, achievements, and all other subdomains simultaneously, creating unpredictable cascading effects.

The most important aspect is **natural authority hierarchy**. In real systems, the right to restrict actions belongs to higher levels. A parent domain represents a more general system and has the right to veto an action through an `Abort` reaction based on global rules. Subdomains, conversely, shouldn't have power to forbid actions at the parent level — they are its part and subject to its rules. This is analogous to organizational structure: an employee's decision is checked by manager, then director — upward through management hierarchy. A director's decision doesn't require approval from all subordinates.

This approach prevents **excessive computation**. Moving upward checks a limited number of reactions — only those on the path from target domain to tree root. Moving downward would require checking reactions of all subdomains, which could be very numerous. For example, inventory might contain hundreds of item-domains, and each inventory action would trigger checking reactions of all these items, even if they're unrelated to the action.

Additionally, upward movement corresponds to the **functionality composition principle**. Parent domains extend child capabilities through adding additional logic without interfering with their internal structure. A subdomain can exist independently, and when embedded in a larger system, it gains additional functionality through parent reactions. For example, the same "Health" domain works identically regardless of whether it's part of player, enemy, or destructible object, but the parent context adds behavior specific to each case.

## Conclusion

The DAR pattern represents an architectural solution for managing complex variable game logic through reaction composition while preserving typed hierarchy of domains and actions.

Key architecture advantages:

**Clear design-to-code mapping** — complex game design concepts have predictable locations in the architecture. Conditional effects, probability-based mechanics, contextual restrictions, and character abilities map directly to domains, actions, and reactions, making the codebase reflect game design intent.

**Separation of concerns** — domains encapsulate state and are the single entry point for modifications, actions describe operations on domains, reactions define conditional logic without changing base classes.

**Behavioral flexibility** — instead of creating subclasses for each capability combination, reaction composition is used, attached to base domain types.

**Centralized control** — all actions pass through the domain's execution pipeline, ensuring unified execution with reaction system integration, logging, validation, and rollback history capability.

**Hierarchical composition** — tree domain structure provides automatic action routing to target subdomains, upward reaction propagation for context addition, natural reflection of game entity composition, and automatic lifecycle management.

**Type safety** — generic constraints in C# ensure compile-time type checking, preventing runtime errors from type mismatches.

The result is a scalable, maintainable, and debuggable system where game logic complexity is managed through composition of independent reactions rather than through growing class hierarchies, which is critically important for long-term development of game projects with high mechanical variability.
