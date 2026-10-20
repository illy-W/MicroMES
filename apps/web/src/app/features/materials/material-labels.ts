import { MaterialType, UnitOfMeasure } from '@micromes/api-client/models';

export const MATERIAL_TYPE_OPTIONS: { value: MaterialType; label: string }[] = [
  { value: MaterialType.RawMaterial, label: 'Materia prima' },
  { value: MaterialType.Intermediate, label: 'Semilavorato' },
  { value: MaterialType.FinishedGood, label: 'Prodotto finito' },
  { value: MaterialType.Consumable, label: 'Materiale di consumo' },
];

export const UNIT_OPTIONS: { value: UnitOfMeasure; label: string }[] = [
  { value: UnitOfMeasure.Piece, label: 'pz' },
  { value: UnitOfMeasure.Gram, label: 'g' },
  { value: UnitOfMeasure.Kilogram, label: 'kg' },
  { value: UnitOfMeasure.Meter, label: 'm' },
  { value: UnitOfMeasure.Liter, label: 'l' },
];

export const typeLabel = (v: string): string =>
  MATERIAL_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;
export const unitLabel = (v: string): string => UNIT_OPTIONS.find((o) => o.value === v)?.label ?? v;
