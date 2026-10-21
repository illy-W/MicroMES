# MicroMES - Glossary

Domain vocabulary, aligned with ISA-95 (IEC 62264) in reduced form (see `doc/product.md`).
Every feature spec that adds or renames a domain entity updates this file.

| Code name | ISA-95 term | UI label (it) | Description | Spec |
|---|---|---|---|---|
| `Material` | Material Definition | Materiale | Master data of anything handled in production: raw material, intermediate, finished product, consumable. Fields: `Code` (unique, uppercase), `Name`, `Description`, `Type` (`RawMaterial`, `Intermediate`, `FinishedGood`, `Consumable`), `UnitOfMeasure` (`Piece`, `Gram`, `Kilogram`, `Meter`, `Liter`), `Vendor`, `VendorMaterialId`, `IsActive` (soft-delete). | `specs/material` |
| `BomItem` | Product Definition (bill of materials, material specification) | Distinta base / Componente | One line of a bill of materials: `ParentMaterialId` (the produced material, `Intermediate` or `FinishedGood`), `ComponentMaterialId`, `Quantity` per 1 unit of the parent, in the component's unit of measure. The BOM of a material is the set of its lines; it is multilevel (a component can have its own BOM). `ConsumedInStepId` (nullable, a step of the parent's routing): the step that consumes the component; null = automatic = first start step (lowest operation number among steps without predecessors). One line = one component = one step (unique index parent+component). |  `specs/bom` |
| `ProcessSegment` | Process Segment | Fase di processo | Reusable production activity independent of the product (cutting, welding, painting...). Fields: `Code` (unique, uppercase), `Name`, `Description`, `SetupMinutes` (per order), `RunMinutesPerUnit`, `IsActive` (soft-delete). | `specs/routing` |
| `RoutingStep` | Operations Segment (product segment) | Passo | A step of the routing of an `Intermediate`/`FinishedGood` material: refers to a `ProcessSegment`, has `OperationNumber` (unique per material), optional `SetupMinutes`/`RunMinutesPerUnit` overriding the segment standard, `Notes`, and the diagram position `PositionX`/`PositionY` (nullable). | `specs/routing` |
| `RoutingStepDependency` | Segment Dependency | Dipendenza | Edge between two steps (predecessor -> successor, finish-to-start). The routing is a graph: parallel branches allowed, acyclic, connected, exactly one final step. | `specs/routing` |
| `ProductionOrder` | Production Request (Work Request) | Ordine di produzione | Request to produce a quantity of an `Intermediate`/`FinishedGood` material. Code `OP-{year}-{nnnn}` (children `{parent}-{n}`), `Quantity`, `Priority` (higher = more urgent), `RequestedStart`/`RequestedEnd`, `Notes`, `ParentOrderId`. Status: `Planned`, `Released`, `InProgress`, `Completed`, `Cancelled`, `Aborted`. Releasing it takes a snapshot of routing and BOM. | `specs/production-order` |
| `WorkOrder` | Job Order (Segment Requirement; equipment requirement = `AssignedEquipmentId`) | Ordine di lavoro | Unit of work of one routing step of a production order: segment snapshot, setup and run-per-unit minutes, planned quantity. Status: `Waiting`, `Ready`, `Running`, `Interrupted`, `Ended`, `Aborted`. Code `{order}/{operation}`. | `specs/production-order` |
| `WorkOrderDependency` | Segment Dependency | Dipendenza | Predecessor -> successor between work orders (copy of the routing edges, plus last step of a child order -> start steps of the parent). | `specs/production-order` |
| `WorkOrderMaterial` | Material Requirement (*material consumed*) | Materiale richiesto | Required quantity of a BOM component for a work order; links to the child order that produces it, if any. | `specs/production-order` |
| `WorkOrderResponse` | Job Response | Registrazione | One work session of a work order: start/end, good and scrap quantity, notes. A work order can have several; totals are the sums. | `specs/production-order` |
| `ScrapReason` | *(none: disposition/reason is left to the implementation)* | Motivo di scarto | Catalog entry: `Code` (unique, uppercase), `Name`, `Description`, `IsActive` (soft-delete). Linked to zero or more `ProcessSegment` (none = valid for every phase). | `specs/scrap-management` |
| `WorkOrderResponseScrap` | Job Response (material produced actual, scrapped quantity) | Riga di scarto | Row of a work session: reason (nullable = *senza motivo*), snapshot of reason code/name, quantity. `WorkOrderResponse.ScrapQuantity` is their sum. | `specs/scrap-management` |
| `WorkOrderResponseCorrection` | - | Correzione | Trace of a correction of a closed session: previous and new values, mandatory note, date. Not allowed when the production order is `Completed`/`Cancelled`. | `specs/scrap-management` |
| `Equipment` | Equipment (role-based, simplified) | Macchina / postazione | Machine or work station where a phase is performed. `Code` (unique, uppercase), `Name`, `Description`, `IsActive` (soft-delete). No hierarchy, class, capacity or calendar yet. | `specs/equipment` |
| `EquipmentProcessSegment` | Process Segment Equipment Specification | Fasi eseguibili | Link machine <-> process segment ("where a phase can be done"). A segment without linked machines has no constraint. Links are kept when either side is deactivated. `IsDefault`: at most one default machine per segment (filtered unique index). | `specs/equipment` |

## Planned (not adopted yet)

| ISA-95 term | Notes |
|---|---|
| Material Class | Grouping of materials with common properties. |
| Material Lot / Sublot | Traceable physical instance of a material. |
| Product Definition | How a material is produced. BOM adopted as `BomItem`, routing as `RoutingStep`/`RoutingStepDependency` with `ProcessSegment`; the BOM-step link is adopted as `BomItem.ConsumedInStepId`; resources still planned. |
| Equipment hierarchy/classes, capacity, calendar; Personnel | Machines adopted as `Equipment`; assignment to work orders, capacity, calendars, downtime and people still planned. |
| Equipment/Personnel requirements, lots, parameters | Resource requirements and actuals of work orders; material actuals (consumption), lots and traceability. |

### State mapping to ISA-95 Part 4 (OPC UA job order states)

| ISA-95 / OPC UA | `WorkOrderStatus` |
|---|---|
| *NotAllowedToStart* (stored, not released) | no equivalent: work orders are created at release |
| *AllowedToStart* / *Waiting* | `Waiting` (predecessors not ended) |
| *AllowedToStart* / *Ready* | `Ready` |
| *Running* | `Running` |
| *Interrupted* (*Suspended*/*Held*) | `Interrupted` |
| *Ended* | `Ended` |
| *Aborted* | `Aborted` |

Commands: `Start`, `Pause`, `Resume`, `Stop` (end before the planned quantity is allowed), `Abort`; `Cancel` applies to the production order only. Source: *OPC UA for ISA-95 Part 4: Job Control* v2.00.0.

## Deviations from ISA-95

- `Material`: vendor and vendor material id are plain fields on the material (single supplier) instead of a `Supplier` relation; introduce the relation when more than one supplier per material is needed.
- `Material.UnitOfMeasure` is a fixed enum, not an entity or a UN/ECE Rec. 20 code; map to Rec. 20 / UnitsNet when conversions are needed.
- `Material` is never physically deleted: `IsActive = false` (soft-delete).
- `BomItem`: no BOM header and no versioning (the BOM is the set of lines of a material); no cycle check (only component != parent, tree read capped at 10 levels); quantity per 1 unit without scrap; lines are physically deleted (they are relations, not master data).
- `RoutingStep`: no routing header and no versioning (the routing is the set of steps of a material); a single dependency type (finish-to-start) without lag; standard times live on the segment and can be overridden per step; no required resources (equipment, personnel); exactly one final step; diagram position stored on the step.
- Production orders: no Work Schedule container; a single request level (the order) with child orders for intermediates; work orders are born `Waiting`/`Ready` at release (no separate *NotAllowedToStart*); required materials go to the work order of the step that consumes them (`ConsumedInStepId`, default first start step) and a child order gates only the work order that consumes it (first start step if the component is not linked); one `WorkOrderResponse` per work session instead of a single Job Response per job order; no lots, resources, parameters or actual material consumption yet.

Record here every further deliberate difference from the standard, with the reason.

- `BomItem.ConsumedInStepId`: material consumption is declared on the BOM line, not as a separate *Material Specification* of the segment; only use `Consumed`; a component can be consumed in one step only (split not supported; extend the unique index to parent+component+step if needed).

- Scrap: a catalog of reasons linked to process segments and per-session scrap rows with a snapshot of the reason; no *disposition* (rework, repair), lots or scrapped raw material; corrections are separate trace rows, not a new Job Response; the operator who corrected is not tracked yet (needs operator identity).

- Equipment: no `Work Center`/`Work Unit` hierarchy and no `Equipment Class`; the link to the process segment is direct (per machine, not per class) and has no use/quantity attribute; no machine assignment on routing steps or work orders yet.

- Equipment assignment: the equipment requirement (`WorkOrder.AssignedEquipmentId`) and actual (`WorkOrderResponse.EquipmentId` with code/name snapshot) are fields, not separate `Equipment Requirement`/`Equipment Actual` entities; one machine per work order and per session; a machine in use by another running work order is a warning, not a block (no capacity yet).
