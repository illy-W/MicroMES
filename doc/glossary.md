# MicroMES - Glossary

Domain vocabulary, aligned with ISA-95 (IEC 62264) in reduced form (see `doc/product.md`).
Every feature spec that adds or renames a domain entity updates this file.

| Code name | ISA-95 term | UI label (it) | Description | Spec |
|---|---|---|---|---|
| `Material` | Material Definition | Materiale | Master data of anything handled in production: raw material, intermediate, finished product, consumable. Fields: `Code` (unique, uppercase), `Name`, `Description`, `Type` (`RawMaterial`, `Intermediate`, `FinishedGood`, `Consumable`), `UnitOfMeasure` (`Piece`, `Gram`, `Kilogram`, `Meter`, `Liter`), `Vendor`, `VendorMaterialId`, `IsActive` (soft-delete). | `specs/material` |
| `BomItem` | Product Definition (bill of materials, material specification) | Distinta base / Componente | One line of a bill of materials: `ParentMaterialId` (the produced material, `Intermediate` or `FinishedGood`), `ComponentMaterialId`, `Quantity` per 1 unit of the parent, in the component's unit of measure. The BOM of a material is the set of its lines; it is multilevel (a component can have its own BOM). | `specs/bom` |
| `ProcessSegment` | Process Segment | Fase di processo | Reusable production activity independent of the product (cutting, welding, painting...). Fields: `Code` (unique, uppercase), `Name`, `Description`, `SetupMinutes` (per order), `RunMinutesPerUnit`, `IsActive` (soft-delete). | `specs/routing` |
| `RoutingStep` | Operations Segment (product segment) | Passo | A step of the routing of an `Intermediate`/`FinishedGood` material: refers to a `ProcessSegment`, has `OperationNumber` (unique per material), optional `SetupMinutes`/`RunMinutesPerUnit` overriding the segment standard, `Notes`, and the diagram position `PositionX`/`PositionY` (nullable). | `specs/routing` |
| `RoutingStepDependency` | Segment Dependency | Dipendenza | Edge between two steps (predecessor -> successor, finish-to-start). The routing is a graph: parallel branches allowed, acyclic, connected, exactly one final step. | `specs/routing` |

## Planned (not adopted yet)

| ISA-95 term | Notes |
|---|---|
| Material Class | Grouping of materials with common properties. |
| Material Lot / Sublot | Traceable physical instance of a material. |
| Product Definition | How a material is produced. BOM adopted as `BomItem`, routing as `RoutingStep`/`RoutingStepDependency` with `ProcessSegment`; BOM-step link (consumption phase) and resources still planned. |
| Equipment, Personnel | Resources. |
| Process Segment, Work Order | Production activities and orders. |

## Deviations from ISA-95

- `Material`: vendor and vendor material id are plain fields on the material (single supplier) instead of a `Supplier` relation; introduce the relation when more than one supplier per material is needed.
- `Material.UnitOfMeasure` is a fixed enum, not an entity or a UN/ECE Rec. 20 code; map to Rec. 20 / UnitsNet when conversions are needed.
- `Material` is never physically deleted: `IsActive = false` (soft-delete).
- `BomItem`: no BOM header and no versioning (the BOM is the set of lines of a material); no cycle check (only component != parent, tree read capped at 10 levels); quantity per 1 unit without scrap; lines are physically deleted (they are relations, not master data).
- `RoutingStep`: no routing header and no versioning (the routing is the set of steps of a material); a single dependency type (finish-to-start) without lag; standard times live on the segment and can be overridden per step; no required resources (equipment, personnel); exactly one final step; diagram position stored on the step.

Record here every further deliberate difference from the standard, with the reason.
