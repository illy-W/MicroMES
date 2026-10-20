import { Route } from '@angular/router';

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
        path: 'materials',
        loadComponent: () =>
          import('./features/materials/materials.component').then((m) => m.MaterialsComponent),
      },
    ],
  },
];
