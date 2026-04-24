import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { resources } from './resources.js';
import generatedLocaleResources from './generatedResources.js';
import { SUPPORTED_LOCALES } from './localeRegistry.js';
import { extractTranslatableStrings } from './extractTranslatableStrings.js';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SRC_ROOT = path.resolve(HERE, '..');

const IGNORED_TEXT_LINE_PATTERNS = [
  'JSON.stringify(',
  'console.log(',
  'LogInfo(',
  'code:',
  'className=',
  'style=',
  'onClick=',
  'onChange=',
  'onClose=',
  'import ',
  'export ',
  '<svg',
  '<path',
  '<circle',
  '<line',
  '<polyline',
  '<CopyableId',
  '<CopyButton',
  '<TenantPicker',
  '<TableFrame',
  '<PageHeader',
  '<Modal',
  '<ConfirmModal',
  '<JsonViewerModal'
];

const ALLOWED_TEXT_NODES = new Set([
  'POST',
  'GET'
]);

function walkJsxFiles(dir) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...walkJsxFiles(fullPath));
      continue;
    }
    if (entry.isFile() && fullPath.endsWith('.jsx') && !fullPath.endsWith('.test.jsx')) {
      files.push(fullPath);
    }
  }
  return files;
}

function isTechnicalPlaceholder(value) {
  const text = String(value || '').trim();
  if (!text) return true;
  if (/^(https?:\/\/.+|\.\/.+|\/.+|[A-Za-z0-9_.-]+@[A-Za-z0-9_.-]+|[A-Za-z0-9_.-]+\.[A-Za-z]{2,}|[A-Za-z0-9_.-]+_[A-Za-z0-9_.-]+|[A-Za-z0-9_.-]+\.\.\.|[A-Za-z0-9_.-]+\.[A-Za-z0-9_.-]+|[A-Za-z0-9_.{}:-]+)$/.test(text)) {
    return true;
  }
  return false;
}

function collectRawTextNodes() {
  const textNodePattern = /<[^>]+>\s*([^<{][^<>]{0,200}?)\s*<\/[^>]+>/g;
  const findings = [];

  for (const file of walkJsxFiles(SRC_ROOT)) {
    const rel = path.relative(SRC_ROOT, file);
    const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
    lines.forEach((line, index) => {
      const stripped = line.trim();
      if (!stripped || stripped.includes('tl(') || stripped.includes("t('") || stripped.includes('t("')) return;
      if (IGNORED_TEXT_LINE_PATTERNS.some((pattern) => stripped.includes(pattern))) return;

      let match = null;
      while ((match = textNodePattern.exec(stripped)) !== null) {
        const text = match[1].replace(/\s+/g, ' ').trim();
        if (!text) continue;
        if (!/[A-Za-z]{3,}/.test(text)) continue;
        if (ALLOWED_TEXT_NODES.has(text)) continue;
        findings.push(`${rel}:${index + 1} -> ${text}`);
      }
      textNodePattern.lastIndex = 0;
    });
  }

  return findings;
}

function collectRawAttributes() {
  const findings = [];
  const titlePattern = /(?:title|aria-label)="([^"]*[A-Za-z][^"]*)"/g;
  const placeholderPattern = /placeholder="([^"]*[A-Za-z][^"]*)"/g;

  for (const file of walkJsxFiles(SRC_ROOT)) {
    const rel = path.relative(SRC_ROOT, file);
    const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
    lines.forEach((line, index) => {
      const stripped = line.trim();

      let match = null;
      while ((match = titlePattern.exec(stripped)) !== null) {
        if (stripped.includes('tl(') || stripped.includes("t('") || stripped.includes('t("')) continue;
        findings.push(`${rel}:${index + 1} -> ${match[1]}`);
      }
      titlePattern.lastIndex = 0;

      while ((match = placeholderPattern.exec(stripped)) !== null) {
        if (stripped.includes('tl(') || stripped.includes("t('") || stripped.includes('t("')) continue;
        if (isTechnicalPlaceholder(match[1])) continue;
        findings.push(`${rel}:${index + 1} -> ${match[1]}`);
      }
      placeholderPattern.lastIndex = 0;
    });
  }

  return findings;
}

function collectUnlocalizedRuntimeErrors() {
  const findings = [];
  const badPatterns = [
    /\.catch\(\(err\)\s*=>\s*[^)]*err\.message/,
    /setError\(err\.message\)/,
    /setFormError\(err\.message\)/,
    /alert\(err\.message\)/,
    /throw new Error\('.*[A-Za-z].*'\)/
  ];

  for (const file of walkJsxFiles(SRC_ROOT)) {
    const rel = path.relative(SRC_ROOT, file);
    const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
    lines.forEach((line, index) => {
      const stripped = line.trim();
      if (stripped.includes('normalizeApiError(') || stripped.includes('translateLiteral(') || stripped.includes('i18n.t(') || stripped.includes('tl(')) {
        return;
      }
      if (badPatterns.some((pattern) => pattern.test(stripped))) {
        findings.push(`${rel}:${index + 1} -> ${stripped}`);
      }
    });
  }

  return findings;
}

function collectImplicitSharedComponentStringProps() {
  const findings = [];
  const propPattern = /<(PageHeader|ModalRecordId|CopyButton|JsonViewerModal|ConfirmModal)\b[^>]*\b(title|subtitle|label|message|confirmLabel|cancelLabel)="([^"]*[A-Za-z][^"]*)"/g;

  for (const file of walkJsxFiles(SRC_ROOT)) {
    const rel = path.relative(SRC_ROOT, file);
    const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
    lines.forEach((line, index) => {
      const stripped = line.trim();
      let match = null;
      while ((match = propPattern.exec(stripped)) !== null) {
        findings.push(`${rel}:${index + 1} -> ${match[1]}.${match[2]}=${match[3]}`);
      }
      propPattern.lastIndex = 0;
    });
  }

  return findings;
}

function flattenStructuredKeys(obj, prefix = '', out = []) {
  for (const [key, value] of Object.entries(obj || {})) {
    const nextKey = prefix ? prefix + '.' + key : key;
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      flattenStructuredKeys(value, nextKey, out);
    } else {
      out.push(nextKey);
    }
  }
  return out;
}

function getByPath(obj, key) {
  return key.split('.').reduce((cursor, part) => (cursor == null ? undefined : cursor[part]), obj);
}

const ALLOWED_UNRESOLVED_ENGLISH = {
  es: {
    keyed: new Set([
      'views.requestHistory.columns.principal',
      'views.roles.form.namePlaceholder',
      'views.tenants.form.namePlaceholder'
    ]),
    literals: new Set([
      '(end)',
      'Alice',
      'Anderson',
      'C#',
      'Error',
      'ID',
      'JavaScript',
      'Principal',
      'Python',
      'RabbitMq',
      'SHA-256',
      'SSL / TLS',
      'Tenant',
      'Token',
      'URL',
      'Windows cmd.exe',
      'macOS/Linux shell'
    ])
  },
  'zh-Hans': {
    keyed: new Set(),
    literals: new Set()
  },
  'yue-Hant-HK': {
    keyed: new Set(),
    literals: new Set(['SHA-256', 'SSL / TLS'])
  },
  ja: {
    keyed: new Set(),
    literals: new Set()
  },
  de: {
    keyed: new Set([
      'views.credentials.columns.name',
      'views.credentials.form.nameTitle',
      'views.requestHistory.columns.status',
      'views.requestHistory.filters.status',
      'views.roles.columns.name',
      'views.tenants.columns.name',
      'views.tenants.columns.region',
      'views.tenants.form.namePlaceholder'
    ]),
    literals: new Set([
      'Alice',
      'Anderson',
      'Hostname',
      'ID',
      'JavaScript',
      'Lease',
      'Name',
      'Native',
      'Offline',
      'Online',
      'Python',
      'RabbitMq',
      'SHA-256',
      'SSL / TLS',
      'Schema',
      'Server',
      'Token',
      'URL',
      'Version',
      'Windows cmd.exe'
    ])
  },
  fr: {
    keyed: new Set(['views.roles.form.namePlaceholder']),
    literals: new Set([
      'Admission',
      'Alice',
      'Arguments',
      'C#',
      'Config DTO',
      'Invocation',
      'JavaScript',
      'Max',
      'Message',
      'Module',
      'Placement',
      'Port',
      'Public',
      'Python',
      'SHA-256',
      'SSL / TLS',
      'Source',
      'Transitions (JSON)',
      'Type',
      'URL',
      'Version',
      'Windows cmd.exe'
    ])
  },
  it: {
    keyed: new Set([
      'views.roles.form.namePlaceholder',
      'views.tenants.form.namePlaceholder'
    ]),
    literals: new Set([
      'Alice',
      'Anderson',
      'Database',
      'Email',
      'File',
      'Host',
      'ID',
      'Input',
      'JavaScript',
      'Offline',
      'Online',
      'Password',
      'Python',
      'Role',
      'SHA-256',
      'SSL / TLS',
      'Schema',
      'Server',
      'Timeout',
      'Timeout (ms)',
      'Token',
      'Trigger',
      'Triggers',
      'Windows cmd.exe',
      'macOS/Linux shell',
      'password'
    ])
  },
  'zh-Hant-TW': {
    keyed: new Set(),
    literals: new Set(['SHA-256', 'SSL / TLS'])
  }
};

describe('dashboard i18n audit', () => {
  it('does not leave raw localizable JSX text nodes in source files', () => {
    expect(collectRawTextNodes()).toEqual([]);
  });

  it('does not leave raw human-readable titles, aria labels, or placeholders in source files', () => {
    expect(collectRawAttributes()).toEqual([]);
  });

  it('does not bypass i18n for runtime error and alert messages', () => {
    expect(collectUnlocalizedRuntimeErrors()).toEqual([]);
  });

  it('does not rely on implicit translation of shared-component string props', () => {
    expect(collectImplicitSharedComponentStringProps()).toEqual([]);
  });

  it('ships generated translations for every extracted dashboard string in every supported non-English locale', () => {
    const extracted = extractTranslatableStrings();
    const structuredKeys = flattenStructuredKeys(resources.en.translation);
    const defaultKeys = Object.keys(extracted.keyedDefaults);
    const failures = [];

    for (const locale of SUPPORTED_LOCALES.filter((value) => value !== 'en')) {
      const translation = generatedLocaleResources[locale]?.translation;
      if (!translation) {
        failures.push(`${locale}: missing generated locale bundle`);
        continue;
      }

      for (const key of structuredKeys) {
        if (getByPath(translation, key) === undefined) {
          failures.push(`${locale}: missing structured key ${key}`);
        }
      }

      for (const key of defaultKeys) {
        if (getByPath(translation, key) === undefined) {
          failures.push(`${locale}: missing defaultValue key ${key}`);
        }
      }

      for (const text of extracted.literals) {
        if (!Object.prototype.hasOwnProperty.call(translation, text)) {
          failures.push(`${locale}: missing literal ${text}`);
        }
      }
    }

    expect(failures).toEqual([]);
  });

  it('does not leave unresolved English dashboard UI text in supported non-English locales outside approved technical exceptions', () => {
    const extracted = extractTranslatableStrings();
    const failures = [];

    for (const locale of SUPPORTED_LOCALES.filter((value) => value !== 'en')) {
      const translation = resources[locale]?.translation;
      const allow = ALLOWED_UNRESOLVED_ENGLISH[locale] || { keyed: new Set(), literals: new Set() };

      for (const [key, english] of Object.entries(extracted.keyedDefaults)) {
        if (getByPath(translation, key) === english && !allow.keyed.has(key)) {
          failures.push(`${locale}: unresolved keyed text ${key} -> ${english}`);
        }
      }

      for (const literal of extracted.literals) {
        if (translation[literal] === literal && !allow.literals.has(literal)) {
          failures.push(`${locale}: unresolved literal ${literal}`);
        }
      }
    }

    expect(failures).toEqual([]);
  });

  it('keeps critical Spanish dashboard UI strings out of English fallback', () => {
    const es = resources.es.translation;
    const normalizeSpanishCheck = (value) => String(value || '')
      .replace(/Ã¡/g, 'a')
      .replace(/Ã©/g, 'e')
      .replace(/Ã­/g, 'i')
      .replace(/Ã³/g, 'o')
      .replace(/Ãº/g, 'u')
      .replace(/Ã±/g, 'n')
      .replace(/Ã/g, '')
      .normalize('NFKD')
      .replace(/[^\x00-\x7F]/g, '')
      .replace(/\s+/g, ' ')
      .trim()
      .toLowerCase();
    const checks = [
      ['common.actions.refresh', 'Actualizar'],
      ['common.actions.skipSetup', 'Omitir configuración'],
      ['navigation.items.logs.label', 'Registros'],
      ['navigation.items.explorer.label', 'Explorador de API'],
      ['navigation.items.artifacts.label', 'Artefactos'],
      ['views.logs.title', 'Registros'],
      ['views.apiExplorer.title', 'Explorador de API'],
      ['views.apiExplorer.resources', 'Recursos'],
      ['Online', 'En línea'],
      ['Enabled', 'Habilitado'],
      ['Host', 'Anfitrión'],
      ['Drain worker', 'Drenar trabajador'],
      ['Viewer', 'Visor'],
      ['Re-read', 'Volver a leer'],
      ['REST listener', 'Escucha REST'],
      ['Console logging', 'Registro en consola'],
      ['Admin API key (bypass)', 'Clave API de administrador (omisión)'],
      ['Artifacts', 'Artefactos'],
      ['Succeeded', 'Completado'],
      ['Global admin', 'Administrador global'],
      ['Tenant admin', 'Administrador del tenant']
    ];

    const failures = checks
      .map(([key, expected]) => {
        const actual = key.includes('.') ? getByPath(es, key) : es[key];
        return normalizeSpanishCheck(actual) === normalizeSpanishCheck(expected)
          ? null
          : `${key}: expected "${expected}", got "${actual}"`;
      })
      .filter(Boolean);

    expect(failures).toEqual([]);
  });
});
