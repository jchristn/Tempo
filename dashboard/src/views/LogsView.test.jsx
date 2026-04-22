import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { expect, test, vi } from 'vitest';
import LogsView from './LogsView';
import WorkersView from './WorkersView';

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location-probe">{location.pathname + location.search}</div>;
}

function renderAt(path, element) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/dashboard/:section" element={element} />
      </Routes>
      <LocationProbe />
    </MemoryRouter>
  );
}

function createLogsApiClient() {
  return {
    listLogSources: vi.fn().mockResolvedValue([
      { sourceKind: 'server', sourceId: 'server', displayName: 'Tempo Server', fileCount: 1, active: true, state: 'Online' },
      { sourceKind: 'worker', sourceId: 'wrk_test_1', displayName: 'Worker Test', fileCount: 1, active: true, state: 'Online' }
    ]),
    listLogFiles: vi.fn().mockImplementation(async (sourceKind, sourceId) => {
      if (sourceKind === 'worker') {
        return [{
          sourceKind,
          sourceId,
          path: 'tempo-worker.log',
          fileName: 'tempo-worker.log',
          byteLength: 24,
          lastModifiedUtc: '2026-04-21T00:00:00Z',
          isCurrent: true,
          sourceActive: true,
          deleteAllowed: true,
          downloadAllowed: true,
          deleteMode: 'Truncate'
        }];
      }
      return [{
        sourceKind,
        sourceId,
        path: 'tempo.log',
        fileName: 'tempo.log',
        byteLength: 32,
        lastModifiedUtc: '2026-04-21T00:00:00Z',
        isCurrent: true,
        sourceActive: true,
        deleteAllowed: true,
        downloadAllowed: true,
        deleteMode: 'Truncate'
      }];
    }),
    readLogFile: vi.fn().mockImplementation(async (sourceKind, sourceId, path) => ({
      sourceKind,
      sourceId,
      path,
      fileName: path,
      byteLength: 32,
      lastModifiedUtc: '2026-04-21T00:00:00Z',
      isCurrent: true,
      sourceActive: true,
      deleteAllowed: true,
      downloadAllowed: true,
      deleteMode: 'Truncate',
      contentType: 'text/plain',
      content: sourceKind === 'worker' ? 'worker log output' : 'server log output',
      truncated: false,
      tailLines: 200,
      maxBytes: 131072,
      returnedByteLength: sourceKind === 'worker' ? 17 : 17
    })),
    deleteLogFile: vi.fn().mockResolvedValue({ action: 'Truncated', success: true }),
    downloadLogFile: vi.fn().mockResolvedValue({ blob: new Blob(['log text'], { type: 'text/plain' }), fileName: 'tempo.log' })
  };
}

test('LogsView defaults to server logs and reads the current file', async () => {
  const apiClient = createLogsApiClient();

  renderAt('/dashboard/logs', <LogsView apiClient={apiClient} principal={{ isAdmin: true, type: 'administrator' }} />);

  expect(await screen.findByText('server log output')).toBeInTheDocument();
  await waitFor(() => expect(apiClient.listLogFiles).toHaveBeenCalledWith('server', 'server'));
  expect(apiClient.readLogFile).toHaveBeenCalledWith('server', 'server', 'tempo.log', { tailLines: 200, maxBytes: 131072 });
});

test('LogsView respects worker deep links and loads worker content', async () => {
  const apiClient = createLogsApiClient();

  renderAt('/dashboard/logs?sourceKind=worker&sourceId=wrk_test_1', <LogsView apiClient={apiClient} principal={{ isAdmin: true, type: 'administrator' }} />);

  expect(await screen.findByText('worker log output')).toBeInTheDocument();
  expect(apiClient.listLogFiles).toHaveBeenCalledWith('worker', 'wrk_test_1');
  expect(apiClient.readLogFile).toHaveBeenCalledWith('worker', 'wrk_test_1', 'tempo-worker.log', { tailLines: 200, maxBytes: 131072 });
});

test('WorkersView deep-links to the log viewer for a selected worker', async () => {
  const apiClient = {
    listWorkers: vi.fn().mockResolvedValue({
      items: [{
        id: 'wrk_test_1',
        name: 'Worker Test',
        kind: 'Worker',
        state: 'Online',
        enabled: true,
        drainMode: false,
        labels: ['default'],
        capabilities: [],
        activeAssignmentCount: 0,
        maxConcurrentRuns: 2,
        maxTaskTimeoutMs: 240000,
        lastHeartbeatUtc: '2026-04-21T00:00:00Z',
        createdUtc: '2026-04-21T00:00:00Z'
      }],
      totalCount: 1
    }),
    readWorker: vi.fn().mockResolvedValue({
      id: 'wrk_test_1',
      name: 'Worker Test',
      kind: 'Worker',
      state: 'Online',
      enabled: true,
      drainMode: false,
      labels: ['default'],
      capabilities: [],
      activeAssignmentCount: 0,
      maxConcurrentRuns: 2,
      maxTaskTimeoutMs: 240000,
      lastHeartbeatUtc: '2026-04-21T00:00:00Z',
      createdUtc: '2026-04-21T00:00:00Z'
    })
  };

  renderAt('/dashboard/workers', <WorkersView apiClient={apiClient} principal={{ isAdmin: true, type: 'administrator' }} />);

  fireEvent.click(await screen.findByText('Worker Test'));
  fireEvent.click(await screen.findByTitle('Open the dedicated log viewer for this worker'));

  expect(await screen.findByTestId('location-probe')).toHaveTextContent('/dashboard/logs?sourceKind=worker&sourceId=wrk_test_1');
});
