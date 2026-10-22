import { Route } from '@angular/router';
import { routingCanDeactivate } from './features/routing/routing-can-deactivate';

export const appRoutes: Route[] = [
  {
    path: '',
    loadComponent: () =>
      import('./core/layouts/full/full.component').then((m) => m.FullLayoutComponent),
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'materials/:id/bom',
        loadComponent: () => import('./features/bom/bom.component').then((m) => m.BomComponent),
      },
      {
        path: 'materials/:id/routing',
        loadComponent: () => import('./features/routing/routing.component').then((m) => m.RoutingComponent),
        canDeactivate: [routingCanDeactivate],
      },
      {
        path: 'process-segments',
        loadComponent: () =>
          import('./features/process-segments/process-segments.component').then((m) => m.ProcessSegmentsComponent),
      },
      {
        path: 'equipment',
        loadComponent: () => import('./features/equipment/equipment.component').then((m) => m.EquipmentComponent),
      },
      {
        path: 'equipment/:id',
        loadComponent: () => import('./features/equipment/equipment-page.component').then((m) => m.EquipmentPageComponent),
      },
      {
        path: 'tooling',
        loadComponent: () => import('./features/tooling/tooling.component').then((m) => m.ToolingComponent),
      },
      {
        path: 'setup-attributes',
        loadComponent: () =>
          import('./features/setup-attributes/setup-attributes.component').then((m) => m.SetupAttributesComponent),
      },
      {
        path: 'calendars',
        loadComponent: () => import('./features/calendars/calendars.component').then((m) => m.CalendarsComponent),
      },
      {
        path: 'scrap-reasons',
        loadComponent: () =>
          import('./features/scrap-reasons/scrap-reasons.component').then((m) => m.ScrapReasonsComponent),
      },
      {
        path: 'scrap',
        loadComponent: () => import('./features/scrap/scrap.component').then((m) => m.ScrapComponent),
      },
      {
        path: 'production-orders/:id',
        loadComponent: () =>
          import('./features/production-orders/production-order-detail.component').then((m) => m.ProductionOrderDetailComponent),
      },
      {
        path: 'production-orders',
        loadComponent: () =>
          import('./features/production-orders/production-orders.component').then((m) => m.ProductionOrdersComponent),
      },
      {
        path: 'work-orders',
        loadComponent: () => import('./features/work-orders/work-orders.component').then((m) => m.WorkOrdersComponent),
      },
      {
        path: 'planning',
        loadComponent: () => import('./features/planning/planning.component').then((m) => m.PlanningComponent),
      },
      {
        path: 'materials',
        loadComponent: () =>
          import('./features/materials/materials.component').then((m) => m.MaterialsComponent),
      },
    ],
  },
];
