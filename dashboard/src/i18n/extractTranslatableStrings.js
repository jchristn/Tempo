import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const DASHBOARD_SRC_ROOT = path.resolve(HERE, '..');

const IGNORED_FILES = new Set([
  path.resolve(DASHBOARD_SRC_ROOT, 'i18n', 'resources.js'),
  path.resolve(DASHBOARD_SRC_ROOT, 'i18n', 'generatedResources.js')
]);

const LITERAL_PATTERNS = [
  { pattern: /\btl\(\s*'((?:\\'|[^'])*)'/g, quote: "'" },
  { pattern: /\btl\(\s*"((?:\\"|[^"])*)"/g, quote: '"' },
  { pattern: /translateLiteral\([^,]+,\s*'((?:\\'|[^'])*)'/g, quote: "'" },
  { pattern: /translateLiteral\([^,]+,\s*"((?:\\"|[^"])*)"/g, quote: '"' },
  { pattern: /\bi18n\.t\(\s*'((?:\\'|[^'])*)'\s*,\s*\{[^}]*keySeparator:\s*false/gs, quote: "'" },
  { pattern: /\bi18n\.t\(\s*"((?:\\"|[^"])*)"\s*,\s*\{[^}]*keySeparator:\s*false/gs, quote: '"' },
  { pattern: /\bt\(\s*'((?:\\'|[^'])*)'\s*,\s*\{[^}]*keySeparator:\s*false/gs, quote: "'" },
  { pattern: /\bt\(\s*"((?:\\"|[^"])*)"\s*,\s*\{[^}]*keySeparator:\s*false/gs, quote: '"' }
];

const KEYED_DEFAULT_PATTERNS = [
  { pattern: /\bt\(\s*'([^']+)'\s*,\s*\{[\s\S]{0,800}?defaultValue:\s*'((?:\\'|[^'])*)'[\s\S]{0,800}?\}\s*\)/g, quote: "'" },
  { pattern: /\bt\(\s*"([^"]+)"\s*,\s*\{[\s\S]{0,800}?defaultValue:\s*"((?:\\"|[^"])*)"[\s\S]{0,800}?\}\s*\)/g, quote: '"' }
];

const IMPLICIT_LITERAL_PROP_NAMES = [
  'label',
  'title',
  'subtitle',
  'message',
  'confirmLabel',
  'cancelLabel',
  'recordIdLabel',
  'emptyMessage',
  'tip',
  'keyPlaceholder',
  'valuePlaceholder',
  'addLabel'
];

const SHARED_LITERAL_PATTERNS = [
  {
    pattern: new RegExp(`\\b(?:${IMPLICIT_LITERAL_PROP_NAMES.join('|')})\\s*:\\s*'((?:\\\\'|[^'])*)'`, 'g'),
    quote: "'"
  },
  {
    pattern: new RegExp(`\\b(?:${IMPLICIT_LITERAL_PROP_NAMES.join('|')})\\s*:\\s*"((?:\\\\"|[^"])*)"`, 'g'),
    quote: '"'
  },
  {
    pattern: new RegExp(`\\b(?:${IMPLICIT_LITERAL_PROP_NAMES.join('|')})\\s*=\\s*\\{\\s*'((?:\\\\'|[^'])*)'\\s*\\}`, 'g'),
    quote: "'"
  },
  {
    pattern: new RegExp(`\\b(?:${IMPLICIT_LITERAL_PROP_NAMES.join('|')})\\s*=\\s*\\{\\s*"((?:\\\\"|[^"])*)"\\s*\\}`, 'g'),
    quote: '"'
  },
  {
    pattern: new RegExp(`\\b(?:${IMPLICIT_LITERAL_PROP_NAMES.join('|')})\\s*=\\s*"((?:\\\\"|[^"])*)"`, 'g'),
    quote: '"'
  }
];

function decodeJsStringLiteral(value, quote) {
  if (!value) return '';
  return Function(`"use strict"; return ${quote}${value}${quote};`)();
}

function walkSourceFiles(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walkSourceFiles(fullPath, out);
      continue;
    }
    if (!entry.isFile()) continue;
    if (!(fullPath.endsWith('.js') || fullPath.endsWith('.jsx'))) continue;
    if (fullPath.endsWith('.test.js') || fullPath.endsWith('.test.jsx')) continue;
    if (IGNORED_FILES.has(fullPath)) continue;
    out.push(fullPath);
  }
  return out;
}

function addPatternMatches(text, patterns, out) {
  for (const { pattern, quote } of patterns) {
    let match;
    while ((match = pattern.exec(text)) !== null) {
      const value = decodeJsStringLiteral(match[1], quote).trim();
      if (!value || !/[A-Za-z]/.test(value)) continue;
      out.add(value);
    }
    pattern.lastIndex = 0;
  }
}

export function extractTranslatableStrings(root = DASHBOARD_SRC_ROOT) {
  const literals = new Set();
  const keyedDefaults = new Map();

  for (const file of walkSourceFiles(root)) {
    const text = fs.readFileSync(file, 'utf8');
    addPatternMatches(text, LITERAL_PATTERNS, literals);
    addPatternMatches(text, SHARED_LITERAL_PATTERNS, literals);

    for (const { pattern, quote } of KEYED_DEFAULT_PATTERNS) {
      let match;
      while ((match = pattern.exec(text)) !== null) {
        const key = match[1];
        const value = decodeJsStringLiteral(match[2], quote).trim();
        if (!key || !value || !/[A-Za-z]/.test(value)) continue;
        keyedDefaults.set(key, value);
      }
      pattern.lastIndex = 0;
    }
  }

  return {
    literals: Array.from(literals).sort(),
    keyedDefaults: Object.fromEntries([...keyedDefaults.entries()].sort(([a], [b]) => a.localeCompare(b)))
  };
}

export default extractTranslatableStrings;
