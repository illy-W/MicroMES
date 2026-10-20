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
        path: 'materials',
        loadComponent: () =>
          import('./features/materials/materials.component').then((m) => m.MaterialsComponent),
      },
    ],
  },
];
