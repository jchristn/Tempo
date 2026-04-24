import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import Topbar from './Topbar';
import Sidebar from './Sidebar';
import SetupWizard from './SetupWizard';
import HomeView from '../views/HomeView';
import RequestHistoryView from '../views/RequestHistoryView';
import ApiExplorerView from '../views/ApiExplorerView';
import DataFlowsView from '../views/DataFlowsView';
import RunsView from '../views/RunsView';
import StepsView from '../views/StepsView';
import RuntimesView from '../views/RuntimesView';
import ArtifactsView from '../views/ArtifactsView';
import TriggersView from '../views/TriggersView';
import TenantsView from '../views/TenantsView';
import UsersView from '../views/UsersView';
import CredentialsView from '../views/CredentialsView';
import RolesView from '../views/RolesView';
import PermissionsView from '../views/PermissionsView';
import SettingsView from '../views/SettingsView';
import WorkersView from '../views/WorkersView';
import LogsView from '../views/LogsView';
import useAutoTitles from '../utils/useAutoTitles';

const LEGACY_SETUP_WIZARD_DISMISSED_KEY = 'tempo.setupWizard.dismissed';

function setupWizardDismissedKey(serverUrl, principal, tenantId) {
  const server = (serverUrl || 'server').trim().toLowerCase();
  const scope = tenantId || principal?.tenantId || principal?.id || 'default';
  return LEGACY_SETUP_WIZARD_DISMISSED_KEY + '.' + server + '.' + scope;
}

function totalCount(result) {
  return Number(result?.totalCount ?? result?.TotalCount ?? 0);
}

async function tenantLooksFresh(apiClient, tenantId) {
  const [flows, triggers, runs] = await Promise.all([
    apiClient.listFlows(tenantId, { pageNumber: 1, pageSize: 1, includeInactive: true }),
    apiClient.listTriggers(tenantId, { pageNumber: 1, pageSize: 1, includeInactive: true }),
    apiClient.listRuns(tenantId, { pageNumber: 1, pageSize: 1, includeInactive: true })
  ]);
  return totalCount(flows) === 0 && totalCount(triggers) === 0 && totalCount(runs) === 0;
}

function Dashboard() {
  const { section = 'home' } = useParams();
  const { apiClient, serverUrl, logout, theme, toggleTheme, principal } = useAuth();
  const [collapsed, setCollapsed] = useState(false);
  const [setupWizardOpen, setSetupWizardOpen] = useState(false);
  const [workspaceRefreshKey, setWorkspaceRefreshKey] = useState(0);

  useAutoTitles(true);

  useEffect(() => {
    if (!apiClient || !principal) return;
    let cancelled = false;

    const decide = async () => {
      const tenantId = principal?.tenantId || '';
      const key = setupWizardDismissedKey(serverUrl || apiClient.baseUrl, principal, tenantId);
      if (window.localStorage.getItem(key)) return;

      const legacyDismissed = window.localStorage.getItem(LEGACY_SETUP_WIZARD_DISMISSED_KEY);
      if (!legacyDismissed) {
        if (!cancelled) setSetupWizardOpen(true);
        return;
      }

      let isFresh = false;
      if (tenantId) {
        try {
          isFresh = await tenantLooksFresh(apiClient, tenantId);
        } catch {
          isFresh = false;
        }
      }

      if (cancelled) return;
      if (isFresh) setSetupWizardOpen(true);
      else window.localStorage.setItem(key, 'true');
    };

    decide();
    return () => { cancelled = true; };
  }, [apiClient, serverUrl, principal]);

  const closeSetupWizard = () => {
    const key = setupWizardDismissedKey(serverUrl || apiClient?.baseUrl, principal, principal?.tenantId || '');
    window.localStorage.setItem(LEGACY_SETUP_WIZARD_DISMISSED_KEY, 'true');
    window.localStorage.setItem(key, 'true');
    setSetupWizardOpen(false);
    setWorkspaceRefreshKey((current) => current + 1);
  };

  const renderView = () => {
    switch (section) {
      case 'home': return <HomeView apiClient={apiClient} />;
      case 'requests': return <RequestHistoryView apiClient={apiClient} principal={principal} />;
      case 'explorer': return <ApiExplorerView apiClient={apiClient} principal={principal} />;
      case 'flows': return <DataFlowsView apiClient={apiClient} principal={principal} />;
      case 'runs': return <RunsView apiClient={apiClient} principal={principal} />;
      case 'steps': return <StepsView apiClient={apiClient} principal={principal} />;
      case 'runtimes': return <RuntimesView apiClient={apiClient} principal={principal} />;
      case 'artifacts': return <ArtifactsView apiClient={apiClient} principal={principal} />;
      case 'triggers': return <TriggersView apiClient={apiClient} principal={principal} />;
      case 'tenants': return <TenantsView apiClient={apiClient} principal={principal} />;
      case 'users': return <UsersView apiClient={apiClient} principal={principal} />;
      case 'credentials': return <CredentialsView apiClient={apiClient} principal={principal} />;
      case 'roles': return <RolesView apiClient={apiClient} principal={principal} />;
      case 'permissions': return <PermissionsView apiClient={apiClient} principal={principal} />;
      case 'workers': return <WorkersView apiClient={apiClient} principal={principal} />;
      case 'logs': return <LogsView apiClient={apiClient} principal={principal} />;
      case 'settings': return <SettingsView apiClient={apiClient} principal={principal} />;
      default: return <HomeView apiClient={apiClient} />;
    }
  };

  return (
    <div className={'dashboard' + (collapsed ? ' sidebar-collapsed' : '')}>
      <Sidebar activeSection={section} collapsed={collapsed} onOpenSetupWizard={() => setSetupWizardOpen(true)} />
      <Topbar
        theme={theme}
        onToggleTheme={toggleTheme}
        onToggleSidebar={() => setCollapsed((c) => !c)}
        onLogout={logout}
        principal={principal}
      />
      <main className="dashboard-content">
        <div key={section + ':' + workspaceRefreshKey}>{renderView()}</div>
      </main>
      <SetupWizard open={setupWizardOpen} apiClient={apiClient} principal={principal} onClose={closeSetupWizard} />
    </div>
  );
}

export default Dashboard;
