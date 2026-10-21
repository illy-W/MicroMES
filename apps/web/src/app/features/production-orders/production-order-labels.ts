import { ProductionOrderStatus, WorkOrderStatus } from '@micromes/api-client/models';

export interface StatusStyle {
  label: string;
  icon: string;
  /** Classi semantiche del testo/icona del badge (solo token). */
  tone: string;
}

export const ORDER_STATUS: Record<ProductionOrderStatus, StatusStyle> = {
  Planned: { label: 'Pianificato', icon: 'lucideCalendarClock', tone: 'text-muted-foreground' },
  Released: { label: 'Rilasciato', icon: 'lucideSend', tone: 'text-primary' },
  InProgress: { label: 'In corso', icon: 'lucidePlay', tone: 'text-primary' },
  Completed: { label: 'Completato', icon: 'lucideCircleCheck', tone: 'text-foreground' },
  Cancelled: { label: 'Annullato', icon: 'lucideCircleOff', tone: 'text-muted-foreground' },
  Aborted: { label: 'Interrotto', icon: 'lucideOctagonX', tone: 'text-destructive' },
};

export const ORDER_STATUS_OPTIONS = (Object.keys(ORDER_STATUS) as ProductionOrderStatus[]).map((value) => ({
  value,
  label: ORDER_STATUS[value].label,
}));

export const WORK_STATUS: Record<WorkOrderStatus, StatusStyle> = {
  Waiting: { label: 'In attesa', icon: 'lucideClock', tone: 'text-muted-foreground' },
  Ready: { label: 'Pronto', icon: 'lucideCircleDot', tone: 'text-primary' },
  Running: { label: 'In esecuzione', icon: 'lucidePlay', tone: 'text-primary' },
  Interrupted: { label: 'In pausa', icon: 'lucidePause', tone: 'text-muted-foreground' },
  Ended: { label: 'Terminato', icon: 'lucideCircleCheck', tone: 'text-foreground' },
  Aborted: { label: 'Interrotto', icon: 'lucideOctagonX', tone: 'text-destructive' },
};

export const orderStatus = (s: string): StatusStyle =>
  ORDER_STATUS[s as ProductionOrderStatus] ?? { label: s, icon: 'lucideCircleDot', tone: 'text-muted-foreground' };
export const workStatus = (s: string): StatusStyle =>
  WORK_STATUS[s as WorkOrderStatus] ?? { label: s, icon: 'lucideCircleDot', tone: 'text-muted-foreground' };

export const ACTIONS = { release: 'Release', cancel: 'Cancel', abort: 'Abort' } as const;

/** "Modifica" non è in allowedActions: solo ordini Planned non figli. */
export const canEditOrder = (o: { status: string; parentOrderId?: number | null }): boolean =>
  o.status === ProductionOrderStatus.Planned && o.parentOrderId == null;

const dateFmt = new Intl.DateTimeFormat('it-IT', { dateStyle: 'short', timeStyle: 'short' });
export const formatDate = (iso?: string | null): string => (iso ? dateFmt.format(new Date(iso)) : '—');

const qtyFmt = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 4 });
export const formatQty = (n: number): string => qtyFmt.format(n);

/** ISO UTC -> valore per <input type="datetime-local"> (ora locale). */
export function toLocalInput(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

/** Valore di datetime-local (ora locale) -> ISO UTC; null se vuoto. */
export const fromLocalInput = (v: string): string | null => (v ? new Date(v).toISOString() : null);
