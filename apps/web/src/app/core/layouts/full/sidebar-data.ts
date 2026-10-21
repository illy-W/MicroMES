import { NavItem } from '../models/nav-item.model';

export const sidebarItems: NavItem[] = [
  { id: 'dashboard', displayName: 'Dashboard', iconName: 'lucideHouse', route: '/dashboard' },
  { id: 'materials', displayName: 'Materiali', iconName: 'lucidePackage', route: '/materials' },
  { id: 'process-segments', displayName: 'Fasi di processo', iconName: 'lucideWorkflow', route: '/process-segments' },
  { id: 'production-orders', displayName: 'Ordini di produzione', iconName: 'lucideClipboardList', route: '/production-orders' },
  { id: 'work-orders', displayName: 'Ordini di lavoro', iconName: 'lucideListChecks', route: '/work-orders' },
];
