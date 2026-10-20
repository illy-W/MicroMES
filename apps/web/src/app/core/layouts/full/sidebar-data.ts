import { NavItem } from '../models/nav-item.model';

export const sidebarItems: NavItem[] = [
  { id: 'dashboard', displayName: 'Dashboard', iconName: 'lucideHouse', route: '/dashboard' },
  { id: 'materials', displayName: 'Materiali', iconName: 'lucidePackage', route: '/materials' },
  { id: 'process-segments', displayName: 'Fasi di processo', iconName: 'lucideWorkflow', route: '/process-segments' },
];
