import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { expect, test, vi } from 'vitest';
import RunsView from './RunsView';

function createRunsApiClient() {
  return {
    listTenants: vi.fn().mockResolvedValue({
      items: [{ id: 'ten_1', name: 'Tenant One' }]
    }),
    listRuns: vi.fn().mockResolvedValue({
      items: [{
        id: 'run_1',
        dataFlowId: 'flow_1',
        state: 'Succeeded',
        dispatchState: 'Completed',
        sourceIp: '198.51.100.10',
        assignedWorkerId: 'wrk_1',
        executionNodeKind: 'Worker',
        createdUtc: '2026-04-22T01:00:00Z',
        startedUtc: '2026-04-22T01:00:01Z',
        completedUtc: '2026-04-22T01:00:03Z'
      }],
      totalCount: 1
    }),
    readRun: vi.fn().mockResolvedValue({
      id: 'run_1',
      dataFlowId: 'flow_1',
      state: 'Succeeded',
      dispatchState: 'Completed',
      sourceIp: '198.51.100.10',
      assignedWorkerId: 'wrk_1',
      executionNodeKind: 'Worker',
      runAssignmentId: 'ras_1',
      createdUtc: '2026-04-22T01:00:00Z',
      assignedUtc: '2026-04-22T01:00:01Z',
      startedUtc: '2026-04-22T01:00:01Z',
      completedUtc: '2026-04-22T01:00:03Z',
      outputData: '{"ok":true}'
    }),
    readRunSteps: vi.fn().mockResolvedValue([
      {
        id: 'sru_1',
        sequence: 1,
        stepId: 'step_1',
        result: 'Success',
        artifactId: null,
        artifactVersionId: null,
        artifactVersion: null,
        nextStepId: null,
        startedUtc: '2026-04-22T01:00:01Z',
        completedUtc: '2026-04-22T01:00:02Z'
      }
    ]),
    getRunActivity: vi.fn().mockResolvedValue({
      run: { id: 'run_1' },
      assignments: [{
        id: 'ras_1',
        attemptNumber: 1,
        workerId: 'wrk_1',
        state: 'Succeeded',
        leaseExpiresUtc: '2026-04-22T01:05:01Z',
        assignedUtc: '2026-04-22T01:00:01Z',
        completedUtc: '2026-04-22T01:00:03Z'
      }],
      activity: [{
        id: 'wac_1',
        createdUtc: '2026-04-22T01:00:01Z',
        eventType: 'execution_completed',
        severity: 'Info',
        workerId: 'wrk_1',
        runAssignmentId: 'ras_1',
        message: 'Assignment completed.'
      }]
    }),
    listRunLogs: vi.fn().mockResolvedValue([
      {
        flowRunId: 'run_1',
        path: 'run.log',
        fileName: 'run.log',
        kind: 'Run',
        byteLength: 120,
        lastModifiedUtc: '2026-04-22T01:00:03Z',
        active: false,
        deleteAllowed: true,
        downloadAllowed: true,
        deleteMode: 'Delete'
      },
      {
        flowRunId: 'run_1',
        path: 'attempt-001-ras_1/worker.log',
        fileName: 'worker.log',
        kind: 'Worker',
        attemptNumber: 1,
        runAssignmentId: 'ras_1',
        workerId: 'wrk_1',
        byteLength: 240,
        lastModifiedUtc: '2026-04-22T01:00:03Z',
        active: false,
        deleteAllowed: true,
        downloadAllowed: true,
        deleteMode: 'Delete'
      }
    ]),
    readRunLog: vi.fn().mockResolvedValue({
      flowRunId: 'run_1',
      path: 'run.log',
      fileName: 'run.log',
      kind: 'Run',
      byteLength: 120,
      lastModifiedUtc: '2026-04-22T01:00:03Z',
      active: false,
      deleteAllowed: true,
      downloadAllowed: true,
      deleteMode: 'Delete',
      content: 'flow run started\nflow run completed',
      truncated: false,
      tailLines: 400,
      maxBytes: 262144,
      returnedByteLength: 32
    }),
    downloadRunLog: vi.fn(),
    deleteRunLog: vi.fn().mockResolvedValue({ action: 'Deleted', success: true }),
    deleteRunLogs: vi.fn().mockResolvedValue(null),
    bulkDeleteRuns: vi.fn().mockResolvedValue({ deletedCount: 0 }),
    cancelRun: vi.fn().mockResolvedValue({ cancelled: true }),
    deleteRun: vi.fn().mockResolvedValue(null)
  };
}

test('RunsView opens a run and loads assignment history plus run logs', async () => {
  const apiClient = createRunsApiClient();

  render(<RunsView apiClient={apiClient} principal={{ tenantId: 'ten_1', type: 'user' }} />);

  fireEvent.click(await screen.findByTitle('run_1'));

  expect(await screen.findByText('Assignment History')).toBeInTheDocument();
  expect(await screen.findByText('Worker Activity')).toBeInTheDocument();
  expect(await screen.findByText('Run Logs')).toBeInTheDocument();

  await waitFor(() => expect(apiClient.getRunActivity).toHaveBeenCalledWith('ten_1', 'run_1'));
  await waitFor(() => expect(apiClient.listRunLogs).toHaveBeenCalledWith('ten_1', 'run_1'));
  expect(apiClient.readRunLog).toHaveBeenCalledWith('ten_1', 'run_1', 'run.log', { tailLines: 400, maxBytes: 262144 });
  await waitFor(() => expect(screen.getByTitle('Bounded tail text from the selected run log file')).toHaveTextContent('flow run started'));
});

test('RunsView deletes the selected archived run log and refreshes the log list', async () => {
  const apiClient = createRunsApiClient();

  render(<RunsView apiClient={apiClient} principal={{ tenantId: 'ten_1', type: 'user' }} />);

  fireEvent.click(await screen.findByTitle('run_1'));
  await waitFor(() => expect(screen.getByTitle('Bounded tail text from the selected run log file')).toHaveTextContent('flow run started'));

  fireEvent.click(screen.getByTitle('Delete this archived run log file'));
  fireEvent.click(await screen.findByText('Delete'));

  await waitFor(() => expect(apiClient.deleteRunLog).toHaveBeenCalledWith('ten_1', 'run_1', 'run.log'));
  await waitFor(() => expect(apiClient.listRunLogs).toHaveBeenCalledTimes(2));
});
