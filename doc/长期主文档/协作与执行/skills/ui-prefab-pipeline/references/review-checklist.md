# Pipeline Review Checklist

## Spec Review

- Is the page tree reasonable?
- Are names stable and explicit?
- Are states, bindings, asset slots, and event exits complete?
- Are unresolved items clearly surfaced?

## Generation Review

- Does the prefab draft match the approved spec?
- Are only allowed components used?
- Does the manifest explain generated nodes and manual follow-up work?
- Are all runtime-touched nodes exported as static `UiReferenceCollector` / manifest bindings?
- Is there no runtime `Transform.Find`, `GameObject.Find`, or recursive `GetComponentsInChildren` node acquisition left in View/Controller logic?

## Validation Review

- Can invalid nodes or components be blocked early?
- Can missing asset slots be reported clearly?
- Can repeated generation stay deterministic?
- Can missing static bindings fail validation before the prefab is promoted?
