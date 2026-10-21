import { Injectable, inject, signal } from '@angular/core';
import type {
  CalendarDayModel,
  CalendarExceptionModel,
  CalendarPreviewModel,
  CreateWorkCalendarRequest,
  DuplicateWorkCalendarRequest,
  ImportItalianHolidaysRequest,
  ImportItalianHolidaysResultModel,
  ItalianHolidaysPreviewModel,
  SaveCalendarExceptionRequest,
  UpdateWorkCalendarRequest,
  WorkCalendarModel,
} from '@micromes/api-client/models';
import { CalendarsService } from '@micromes/api-client/services/calendars';
import { firstValueFrom } from 'rxjs';
import { toFailure } from '../materials/materials.service';

@Injectable({ providedIn: 'root' })
export class CalendarsStore {
  private readonly api = inject(CalendarsService);

  readonly items = signal<WorkCalendarModel[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly timeZone = signal('Europe/Rome');
  private tzLoaded = false;

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.items.set(await firstValueFrom(this.api.getCalendars({ includeInactive: true })));
    } catch (e) {
      this.loadError.set(toFailure(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  async loadTimeZone(): Promise<void> {
    if (this.tzLoaded) return;
    try {
      this.timeZone.set((await firstValueFrom(this.api.getCompanyTimeZone())).timeZoneId);
      this.tzLoaded = true;
    } catch {
      /* resta il valore di default */
    }
  }

  create(req: CreateWorkCalendarRequest): Promise<WorkCalendarModel> {
    return firstValueFrom(this.api.createCalendar(req));
  }
  update(id: number, req: UpdateWorkCalendarRequest): Promise<WorkCalendarModel> {
    return firstValueFrom(this.api.updateCalendar(id, req));
  }
  duplicate(id: number, req: DuplicateWorkCalendarRequest): Promise<WorkCalendarModel> {
    return firstValueFrom(this.api.duplicateCalendar(id, req));
  }
  deactivate(id: number): Promise<unknown> {
    return firstValueFrom(this.api.deactivateCalendar(id));
  }
  setDefault(id: number): Promise<WorkCalendarModel> {
    return firstValueFrom(this.api.setDefaultCalendar(id));
  }
  preview(id: number, startUtcIso: string, minutes: number): Promise<CalendarPreviewModel> {
    return firstValueFrom(this.api.previewCalendar(id, { start: startUtcIso, minutes }));
  }

  exceptions(id: number, year?: number): Promise<CalendarExceptionModel[]> {
    return firstValueFrom(this.api.getCalendarExceptions(id, year ? { year } : undefined));
  }
  createException(id: number, req: SaveCalendarExceptionRequest): Promise<CalendarExceptionModel> {
    return firstValueFrom(this.api.createCalendarException(id, req));
  }
  updateException(id: number, exceptionId: number, req: SaveCalendarExceptionRequest): Promise<CalendarExceptionModel> {
    return firstValueFrom(this.api.updateCalendarException(id, exceptionId, req));
  }
  deleteException(id: number, exceptionId: number): Promise<unknown> {
    return firstValueFrom(this.api.deleteCalendarException(id, exceptionId));
  }
  days(id: number, from: string, to: string): Promise<CalendarDayModel[]> {
    return firstValueFrom(this.api.getCalendarDays(id, { from, to }));
  }
  equipmentDays(id: number, from: string, to: string): Promise<CalendarDayModel[]> {
    return firstValueFrom(this.api.getEquipmentCalendarDays(id, { from, to }));
  }

  equipmentExceptions(id: number, year?: number): Promise<CalendarExceptionModel[]> {
    return firstValueFrom(this.api.getEquipmentExceptions(id, year ? { year } : undefined));
  }
  createEquipmentException(id: number, req: SaveCalendarExceptionRequest): Promise<CalendarExceptionModel> {
    return firstValueFrom(this.api.createEquipmentException(id, req));
  }
  updateEquipmentException(id: number, exceptionId: number, req: SaveCalendarExceptionRequest): Promise<CalendarExceptionModel> {
    return firstValueFrom(this.api.updateEquipmentException(id, exceptionId, req));
  }
  deleteEquipmentException(id: number, exceptionId: number): Promise<unknown> {
    return firstValueFrom(this.api.deleteEquipmentException(id, exceptionId));
  }

  holidaysPreview(id: number, year: number): Promise<ItalianHolidaysPreviewModel> {
    return firstValueFrom(this.api.previewItalianHolidays(id, { year }));
  }
  importHolidays(id: number, req: ImportItalianHolidaysRequest): Promise<ImportItalianHolidaysResultModel> {
    return firstValueFrom(this.api.importItalianHolidays(id, req));
  }
}
