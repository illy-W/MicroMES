import type { RoutingStepSetupValueModel, RoutingStepToolingModel, UpdateRoutingSetupValueRequest } from '@micromes/api-client/models';

/** Riga dell'editor dei valori di cambio: `attributeId` null = riga appena aggiunta, senza attributo scelto. */
export interface SetupValueRow {
  attributeId: number | null;
  value: string;
}

export interface ToolingOption {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
}

export interface ToolingChoice extends ToolingOption {
  /** Disattivata ma già collegata al passo: non si può aggiungere, solo rimuovere. */
  linkedInactive: boolean;
}

/** Attrezzature attive più quelle già collegate (anche disattivate, in coda e marcate). */
export function toolingChoices(all: readonly ToolingOption[], linked: readonly RoutingStepToolingModel[]): ToolingChoice[] {
  const byId = new Map(all.map((t) => [t.id, t]));
  const choices: ToolingChoice[] = all.filter((t) => t.isActive).map((t) => ({ ...t, linkedInactive: false }));
  for (const l of linked) {
    const current = byId.get(l.id) ?? l;
    if (!current.isActive) choices.push({ id: l.id, code: current.code, name: current.name, isActive: false, linkedInactive: true });
  }
  return choices;
}

export function toggleId(ids: readonly number[], id: number, on: boolean): number[] {
  const without = ids.filter((i) => i !== id);
  return on ? [...without, id] : without;
}

export function toRows(values: readonly RoutingStepSetupValueModel[]): SetupValueRow[] {
  return values.map((v) => ({ attributeId: v.attributeId, value: v.value }));
}

/** Attributi scelti in più righe: la richiesta ne terrebbe una sola, quindi si blocca il salvataggio. */
export function duplicateAttributeIds(rows: readonly SetupValueRow[]): number[] {
  const seen = new Set<number>();
  const dup = new Set<number>();
  for (const r of rows) {
    if (r.attributeId === null) continue;
    if (seen.has(r.attributeId)) dup.add(r.attributeId);
    seen.add(r.attributeId);
  }
  return [...dup];
}

/** Righe con valore ma senza attributo (incompleto). */
export function rowsMissingAttribute(rows: readonly SetupValueRow[]): number {
  return rows.filter((r) => r.attributeId === null && r.value.trim() !== '').length;
}

/**
 * Richiesta `setupValues`: i valori compilati; gli attributi già salvati e ora rimossi o vuoti
 * vanno inviati con valore vuoto, che li cancella (omettere un attributo lo lascia invariato).
 */
export function buildSetupValuesRequest(rows: readonly SetupValueRow[], savedAttributeIds: readonly number[]): UpdateRoutingSetupValueRequest[] {
  const result = new Map<number, string>();
  for (const r of rows) {
    if (r.attributeId === null) continue;
    result.set(r.attributeId, r.value.trim());
  }
  for (const id of savedAttributeIds) if (!result.has(id)) result.set(id, '');
  return [...result.entries()].map(([attributeId, value]) => ({ attributeId, value }));
}
