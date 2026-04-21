import { Link } from 'react-router-dom';

const SECTIONS = [
  {
    heading: 'Observability',
    items: [
      { id: 'home', label: 'Home', icon: 'chart', tip: 'Activity and health at a glance' },
      { id: 'requests', label: 'Request History', icon: 'list', tip: 'Every captured HTTP request, with bucketed activity chart' },
      { id: 'explorer', label: 'API Explorer', icon: 'play', tip: 'Browse and exercise every endpoint discovered from /openapi.json' }
    ]
  },
  {
    heading: 'Flows',
    items: [
      { id: 'steps', label: 'Steps', icon: 'steps', tip: 'Create reusable units of work first' },
      { id: 'flows', label: 'Data Flows', icon: 'flow', tip: 'Connect steps into an executable graph next' },
      { id: 'triggers', label: 'Triggers', icon: 'trigger', tip: 'Create inbound entry points after a flow exists' },
      { id: 'runs', label: 'Runs', icon: 'runs', tip: 'Review each execution and per-step timeline' },
      { id: 'artifacts', label: 'Artifacts', icon: 'artifact', tip: 'Manage uploaded packages used by artifact-backed steps' },
      { id: 'runtimes', label: 'Runtimes', icon: 'runtime', tip: 'Review runtime providers and tenant availability' }
    ]
  },
  {
    heading: 'Administration',
    items: [
      { id: 'tenants', label: 'Tenants', icon: 'tenant', tip: 'Top-level isolation boundary; owns users, flows, runs, etc.' },
      { id: 'users', label: 'Users', icon: 'user', tip: 'Tenant-scoped login accounts and their role assignments' },
      { id: 'credentials', label: 'Credentials', icon: 'key', tip: 'API access key / secret key pairs used by integrations' },
      { id: 'roles', label: 'Roles', icon: 'shield', tip: 'Containers for granular permission grants' },
      { id: 'permissions', label: 'Permissions', icon: 'lock', tip: 'Granular permit/deny rules over resource + operation pairs' }
    ]
  },
  {
    heading: 'System',
    items: [{ id: 'settings', label: 'Settings', icon: 'gear', tip: 'Server configuration and dashboard preferences' }]
  }
];

const APP_VERSION = import.meta.env.VITE_APP_VERSION || 'dev';

function Icon({ name }) {
  const props = { width: 18, height: 18, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round', className: 'icon' };
  switch (name) {
    case 'chart': return <svg {...props}><path d="M4 20V10m6 10V4m6 16v-8m4 8H2" /></svg>;
    case 'list': return <svg {...props}><path d="M8 6h13M8 12h13M3 6h.01M3 12h.01M8 18h13M3 18h.01" /></svg>;
    case 'play': return <svg {...props}><polygon points="5 3 19 12 5 21 5 3" /></svg>;
    case 'flow': return <svg {...props}><circle cx="6" cy="6" r="3" /><circle cx="18" cy="6" r="3" /><circle cx="12" cy="18" r="3" /><path d="M9 7.5l6 0M7.5 8.5l3 7M16.5 8.5l-3 7" /></svg>;
    case 'runs': return <svg {...props}><circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" /></svg>;
    case 'steps': return <svg {...props}><path d="M3 17h6v4H3zM9 11h6v10H9zM15 5h6v16h-6z" /></svg>;
    case 'runtime': return <svg {...props}><path d="M4 17l4-10 4 10 4-10 4 10" /><path d="M4 17h16" /></svg>;
    case 'artifact': return <svg {...props}><path d="M21 8l-9-5-9 5 9 5 9-5z" /><path d="M3 8v8l9 5 9-5V8" /><path d="M12 13v8" /></svg>;
    case 'trigger': return <svg {...props}><path d="M13 2L3 14h9l-1 8 10-12h-9z" /></svg>;
    case 'tenant': return <svg {...props}><rect x="3" y="3" width="7" height="7" /><rect x="14" y="3" width="7" height="7" /><rect x="3" y="14" width="7" height="7" /><rect x="14" y="14" width="7" height="7" /></svg>;
    case 'user': return <svg {...props}><circle cx="12" cy="7" r="4" /><path d="M5 21v-2a7 7 0 0114 0v2" /></svg>;
    case 'key': return <svg {...props}><circle cx="8" cy="12" r="4" /><path d="M12 12h10M19 15l3-3-3-3" /></svg>;
    case 'shield': return <svg {...props}><path d="M12 2l8 4v6c0 5-3.5 9-8 10-4.5-1-8-5-8-10V6z" /></svg>;
    case 'lock': return <svg {...props}><rect x="4" y="11" width="16" height="10" rx="2" /><path d="M8 11V7a4 4 0 018 0v4" /></svg>;
    case 'gear': return <svg {...props}><circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 01-2.83 2.83l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09a1.65 1.65 0 00-1-1.51 1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06a1.65 1.65 0 00.33-1.82 1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09a1.65 1.65 0 001.51-1 1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06a1.65 1.65 0 001.82.33 1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06a1.65 1.65 0 00-.33 1.82 1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z" /></svg>;
    default: return <svg {...props}><circle cx="12" cy="12" r="4" /></svg>;
  }
}

function Sidebar({ activeSection, collapsed, onOpenSetupWizard }) {
  return (
    <aside className="dashboard-sidebar">
      <div className="sidebar-brand">
        <img src="/logo.png" alt="Tempo" />
        <span>Tempo</span>
      </div>
      <nav className="sidebar-nav">
        {SECTIONS.map((section) => (
          <div key={section.heading}>
            {!collapsed && <div className="sidebar-section">{section.heading}</div>}
            {section.items.map((item) => (
              <Link
                key={item.id}
                to={'/dashboard/' + item.id}
                className={'sidebar-item' + (activeSection === item.id ? ' active' : '')}
                title={item.tip || item.label}
              >
                <Icon name={item.icon} />
                <span className="label">{item.label}</span>
              </Link>
            ))}
          </div>
        ))}
      </nav>
      <div className="sidebar-footer">
        <button type="button" className="sidebar-setup" onClick={onOpenSetupWizard} title="Open setup wizard">
          <Icon name="play" />
          <span className="label">Setup wizard</span>
        </button>
        <div className="sidebar-version" title={'Tempo version ' + APP_VERSION}>
          <span className="label">v{APP_VERSION}</span>
        </div>
      </div>
    </aside>
  );
}

export default Sidebar;
