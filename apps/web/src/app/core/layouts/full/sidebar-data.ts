import { NavSection } from '../models/nav-item.model';

export const sidebarSections: NavSection[] = [
  {
    id: 'main',
    items: [{ id: 'dashboard', displayName: 'Dashboard', iconName: 'lucideHouse', route: '/dashboard' }],
  },
  {
    id: 'production',
    label: 'Produzione',
    items: [
      { id: 'production-orders', displayName: 'Ordini di produzione', iconName: 'lucideClipboardList', route: '/production-orders' },
      { id: 'work-orders', displayName: 'Ordini di lavoro', iconName: 'lucideListChecks', route: '/work-orders' },
      { id: 'scrap', displayName: 'Scarti', iconName: 'lucideChartNoAxesColumn', route: '/scrap' },
    ],
  },
  {
    id: 'master-data',
    label: 'Anagrafiche',
    items: [
      { id: 'materials', displayName: 'Materiali', iconName: 'lucidePackage', route: '/materials' },
      { id: 'process-segments', displayName: 'Fasi di processo', iconName: 'lucideWorkflow', route: '/process-segments' },
      { id: 'equipment', displayName: 'Macchine e postazioni', iconName: 'lucideCog', route: '/equipment' },
      { id: 'scrap-reasons', displayName: 'Motivi di scarto', iconName: 'lucideTags', route: '/scrap-reasons' },
    ],
  },
];
