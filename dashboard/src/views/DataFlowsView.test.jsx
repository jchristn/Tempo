import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { expect, test, vi } from 'vitest';
import DataFlowsView from './DataFlowsView';

function createFlowsApiClient() {
  return {
    baseUrl: 'http://localhost:8901',
    listTenants: vi.fn().mockResolvedValue({
      items: [{ id: 'ten_1', name: 'Tenant One' }]
    }),
    listFlows: vi.fn().mockResolvedValue({
      items: [{
        id: 'flow_1',
        tenantId: 'ten_1',
        name: 'Protected flow',
        description: 'Requires API authentication',
        startStepId: 'start',
        invocationAuthMode: 'ApiAuthenticated',
        maxRuntimeMs: 30000,
        transitions: {
          start: { OnSuccess: null, OnFailure: null, OnException: null }
        },
        active: true,
        createdUtc: '2026-04-22T01:00:00Z'
      }],
      totalCount: 1
    }),
    updateFlow: vi.fn().mockImplementation(async (_tenantId, _id, body) => ({ ...body, id: 'flow_1' })),
    createFlow: vi.fn(),
    ensureFlowSteps: vi.fn().mockResolvedValue({}),
    listTriggers: vi.fn().mockResolvedValue({
      items: [{
        id: 'trg_1',
        tenantId: 'ten_1',
        name: 'Protected flow trigger',
        triggerType: 'Http',
        dataFlowId: 'flow_1',
        active: true,
        configuration: '{"allowedMethods":["POST"]}'
      }]
    }),
    bulkDeleteFlows: vi.fn(),
    deleteFlow: vi.fn(),
    runFlow: vi.fn()
  };
}

test('flow invocation auth is visible and editable', async () => {
  const apiClient = createFlowsApiClient();
  render(<DataFlowsView apiClient={apiClient} principal={{ tenantId: 'ten_1' }} />);

  expect(await screen.findByText('API auth')).toBeInTheDocument();
  fireEvent.click(screen.getByText('Protected flow'));

  expect(await screen.findByText('API-authenticated trigger')).toBeInTheDocument();
  expect(screen.getByText('Example cURL')).toBeInTheDocument();
  expect(screen.getByText((content) => content.includes('/v1.0/triggers/http/trg_1'))).toBeInTheDocument();
  expect(screen.getByText((content) => content.includes('Authorization: Bearer'))).toBeInTheDocument();
  fireEvent.click(screen.getByText('Public trigger'));
  await waitFor(() => expect(screen.queryByText((content) => content.includes('Authorization: Bearer'))).not.toBeInTheDocument());
  fireEvent.click(screen.getByText('Save'));

  await waitFor(() => expect(apiClient.updateFlow).toHaveBeenCalled());
  const savedBody = apiClient.updateFlow.mock.calls[0][2];
  expect(savedBody.invocationAuthMode).toBe('Public');
});

test('new flow modal shows a placeholder cURL example before save', async () => {
  const apiClient = createFlowsApiClient();
  render(<DataFlowsView apiClient={apiClient} principal={{ tenantId: 'ten_1' }} />);

  await screen.findByText('Protected flow');
  fireEvent.click(screen.getByText('+ New flow'));

  expect(await screen.findByText('Example cURL')).toBeInTheDocument();
  expect(screen.getByText(/Save the flow and attach an HTTP trigger/)).toBeInTheDocument();
});
