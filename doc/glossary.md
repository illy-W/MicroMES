# MicroMES - Glossary

Domain vocabulary, aligned with ISA-95 (IEC 62264) in reduced form (see `doc/product.md`).
Every feature spec that adds or renames a domain entity updates this file.

| Code name | ISA-95 term | UI label (it) | Description | Spec |
|---|---|---|---|---|
| `Material` | Material Definition | Materiale | Master data of anything handled in production: raw material, intermediate, finished product, consumable. Fields: `Code` (unique, uppercase), `Name`, `Description`, `Type` (`RawMaterial`, `Intermediate`, `FinishedGood`, `Consumable`), `UnitOfMeasure` (`Piece`, `Gram`, `Kilogram`, `Meter`, `Liter`), `Vendor`, `VendorMaterialId`, `IsActive` (soft-delete). | `specs/material` |

## Planned (not adopted yet)

| ISA-95 term | Notes |
|---|---|
| Material Class | Grouping of materials with common properties. |
| Material Lot / Sublot | Traceable physical instance of a material. |
| Product Definition | How a material is produced (BOM, routing, segments). |
| Equipment, Personnel | Resources. |
| Process Segment, Work Order | Production activities and orders. |

## Deviations from ISA-95

- `Material`: vendor and vendor material id are plain fields on the material (single supplier) instead of a `Supplier` relation; introduce the relation when more than one supplier per material is needed.
- `Material.UnitOfMeasure` is a fixed enum, not an entity or a UN/ECE Rec. 20 code; map to Rec. 20 / UnitsNet when conversions are needed.
- `Material` is never physically deleted: `IsActive = false` (soft-delete).

Record here every further deliberate difference from the standard, with the reason.
