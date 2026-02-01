# Action Preconditions

Actions can define built-in validation logic using the `_IsExecutable` method. This provides an alternative to Abort reactions for validating action preconditions.

## The `_IsExecutable` Method

Every action inherits a virtual `_IsExecutable` method (see `DARAction.cs:158`) that you can override. This method is called during `IsExecutable()` after Prepare and Abort reactions run. If it returns `false`, the action will not execute.

The default implementation always returns `true`, allowing all actions to execute unless explicitly prevented.

## When to Use `_IsExecutable` vs Abort Reactions

**Use `_IsExecutable` when:**
- The validation logic is intrinsic to the action itself
- You want validation to be part of the action's definition
- The precondition is always required for this action type

**Use Abort reactions when:**
- Validation logic depends on game rules that may change
- Multiple domains need to validate the same action differently
- You want to add validation without modifying the action class
- Validation logic should be reusable across multiple action types

## Example: Resource Cost Validation

Override `_IsExecutable` to check if a player has enough resources before casting a spell. The method receives the typed domain, allowing direct access to properties like `CurrentMana` without casting.

If the player lacks sufficient mana, return `false` to prevent execution. The action's `ExecuteProcess` will only run after this validation passes.

## Execution Order

The complete validation flow is:

1. **Prepare reactions** - Modify action parameters
2. **Abort reactions** - External validation (game rules)
3. **`_IsExecutable`** - Built-in action validation
4. **Before reactions** - Pre-execution logic
5. **ExecuteProcess** - Main action logic
6. **After reactions** - Post-execution logic

If any step returns `false` or aborts, execution stops.

## Combining Both Approaches

You can use both `_IsExecutable` and Abort reactions together for layered validation:

- **Action's `_IsExecutable`** defines base requirements (e.g., unit must be alive)
- **Abort reactions** add contextual game rules (e.g., stunned units can't attack)

This separation keeps invariant preconditions in the action class while allowing flexible, external rule enforcement through reactions. The action remains reusable across different game modes with different rules.

## Benefits

- **Type safety** - Access typed domain properties directly
- **Encapsulation** - Validation logic lives with the action
- **Performance** - No need to search for reactions if validation is simple
- **Clarity** - Clear what preconditions are required for an action

## Network Actions

For network actions, `_IsExecutable` runs on both client and server, enabling client-side validation and server-side security. See the [Network Actions](../network/02-network-actions.md) article for more details on client-server validation patterns.

## See Also

- [Reactions](../getting-started/02-reactions.md) - Using Abort reactions for validation
- [Action Injection](01-action-injection.md) - Accessing domains in actions
