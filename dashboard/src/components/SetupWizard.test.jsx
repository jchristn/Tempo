import { describe, expect, it } from 'vitest';
import { codeTemplate } from './SetupWizard';

describe('codeTemplate', () => {
  it.each([
    ['JavaScript', 'echo', 'console.log("Echo step received input:", req.data);'],
    ['JavaScript', 'random', 'console.log("Random number step generated value:", value);'],
    ['JavaScript', 'double', 'console.log("Double number step received value:", value);'],
    ['Python', 'echo', 'print("Echo step received input:", req.get("data"))'],
    ['Python', 'random', 'print(f"Random number step generated value: {value}")'],
    ['Python', 'double', 'print(f"Double number step received value: {value}")'],
    ['CSharp', 'echo', 'LogInfo("Echo step received input: " + request.Data);'],
    ['CSharp', 'random', 'LogInfo("Random number step generated value: " + value);'],
    ['CSharp', 'double', 'LogInfo("Double number step received value: " + input);']
  ])('includes an in-step log line for %s %s templates', (language, kind, expected) => {
    expect(codeTemplate(language, kind).code).toContain(expected);
  });

  it('uses the Tempo C# base handler for C# templates', () => {
    expect(codeTemplate('CSharp', 'echo').code).toContain('TempoStepHandlerBase');
    expect(codeTemplate('CSharp', 'echo').code).toContain('public override Task<StepResult> RunAsync');
  });
});
