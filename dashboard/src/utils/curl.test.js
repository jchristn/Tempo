import { describe, expect, it } from 'vitest';
import { authTokenPlaceholder, buildCurlCommand } from './curl';

describe('buildCurlCommand', () => {
  it('formats Windows commands with caret continuations', () => {
    const command = buildCurlCommand({
      url: 'http://localhost:8901/v1.0/tenants/ten_1/flows/flow_1/runs',
      method: 'POST',
      headers: {
        Authorization: 'Bearer %TEMPO_BEARER%',
        'Content-Type': 'application/json'
      },
      body: { data: { value: 'hello' } },
      platform: 'Win32'
    });

    expect(command.label).toBe('Windows cmd.exe');
    expect(command.lineSeparator).toBe('^');
    expect(command.command).toContain('curl.exe ^\n  -X POST ^\n  "http://localhost:8901/v1.0/tenants/ten_1/flows/flow_1/runs"');
    expect(command.command).toContain('-H "Authorization: Bearer %TEMPO_BEARER%"');
    expect(command.command).toContain('--data-raw "{\\"data\\":{\\"value\\":\\"hello\\"}}"');
  });

  it('formats macOS/Linux commands with backslash continuations and expandable auth headers', () => {
    const command = buildCurlCommand({
      url: 'http://localhost:8901/v1.0/tenants/ten_1/flows/flow_1/runs',
      method: 'POST',
      headers: {
        Authorization: 'Bearer $TEMPO_BEARER',
        'Content-Type': 'application/json'
      },
      body: { data: { value: 'hello' } },
      platform: 'MacIntel',
      shellExpandableHeaders: ['Authorization']
    });

    expect(command.label).toBe('macOS/Linux shell');
    expect(command.lineSeparator).toBe('\\');
    expect(command.command).toContain('curl \\\n  -X POST \\\n  \'http://localhost:8901/v1.0/tenants/ten_1/flows/flow_1/runs\'');
    expect(command.command).toContain('-H "Authorization: Bearer $TEMPO_BEARER"');
    expect(command.command).toContain('-H \'Content-Type: application/json\'');
    expect(command.command).toContain('--data-raw \'{"data":{"value":"hello"}}\'');
  });
});

describe('authTokenPlaceholder', () => {
  it('matches the caller shell convention', () => {
    expect(authTokenPlaceholder('Win32')).toBe('%TEMPO_BEARER%');
    expect(authTokenPlaceholder('MacIntel')).toBe('$TEMPO_BEARER');
  });
});
