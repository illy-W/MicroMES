import type { EquipmentNowState } from '@micromes/api-client/models';

export const LOAD_HIGH = 90;

/** Durata trascorsa: `da 1 h 20 min`, `da 45 min`, `da 2 g 3 h`. */
export function formatElapsed(minutes: number): string {
  const min = Math.max(0, Math.round(minutes));
  if (min < 60) return `da ${min} min`;
  const h = Math.floor(min / 60);
  const m = min % 60;
  if (h < 24) return m ? `da ${h} h ${m} min` : `da ${h} h`;
  const d = Math.floor(h / 24);
  const rh = h % 24;
  return rh ? `da ${d} g ${rh} h` : `da ${d} g`;
}

/** Etichetta dello stato "adesso". */
export function stateLabel(state: EquipmentNowState, closedReason?: string | null): string {
  switch (state) {
    case 'Free':
      return 'Libera';
    case 'Working':
      return 'In lavorazione';
    case 'ClosedNow':
      return closedReason ? `Chiusa: ${closedReason}` : 'Chiusa';
    default:
      return 'Disattivata';
  }
}

export const STATE_ICON: Record<EquipmentNowState, string> = {
  Free: 'lucideCircleCheck',
  Working: 'lucidePlay',
  ClosedNow: 'lucideMoon',
  Inactive: 'lucideCircleOff',
};

export type LoadLevel = 'unknown' | 'normal' | 'high';

export function loadLevel(value: number | null | undefined): LoadLevel {
  if (value == null) return 'unknown';
  return value >= LOAD_HIGH ? 'high' : 'normal';
}

export function loadText(value: number | null | undefined): string {
  return value == null ? 'n/d' : `${Math.round(value)}%`;
}

/** Larghezza barra (0-100). */
export function loadWidth(value: number | null | undefined): number {
  return value == null ? 0 : Math.max(0, Math.min(100, value));
}
