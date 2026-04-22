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
    ['CSharp', 'echo', 'Console.Error.WriteLine("Echo step received input: " + request.Data);'],
    ['CSharp', 'random', 'Console.Error.WriteLine("Random number step generated value: " + value);'],
    ['CSharp', 'double', 'Console.Error.WriteLine("Double number step received value: " + input);']
  ])('includes an in-step log line for %s %s templates', (language, kind, expected) => {
    expect(codeTemplate(language, kind).code).toContain(expected);
  });
});
