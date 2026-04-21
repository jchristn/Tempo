/**
 * OpenAPI helpers used by the API Explorer.
 */

export function flattenOpenApiSpec(spec) {
  if (!spec || !spec.paths) return [];
  const out = [];
  for (const [path, methods] of Object.entries(spec.paths)) {
    for (const [method, op] of Object.entries(methods)) {
      if (!['get', 'post', 'put', 'delete', 'patch', 'head'].includes(method)) continue;
      out.push({
        id: method + ' ' + path,
        method: method.toUpperCase(),
        path,
        tag: (op.tags && op.tags[0]) || 'default',
        summary: op.summary || '',
        description: op.description || '',
        parameters: op.parameters || [],
        requestBody: op.requestBody || null,
        responses: op.responses || {}
      });
    }
  }
  return out.sort((a, b) => (a.tag + a.path).localeCompare(b.tag + b.path));
}

export function groupByTag(operations) {
  const groups = {};
  for (const op of operations) {
    if (!groups[op.tag]) groups[op.tag] = [];
    groups[op.tag].push(op);
  }
  return groups;
}

export function substitutePathParams(path, params) {
  return path.replace(/\{([^}]+)\}/g, (_, name) => {
    const v = params && params[name];
    return v !== undefined && v !== null ? encodeURIComponent(v) : '{' + name + '}';
  });
}

export function buildCurlSnippet(baseUrl, method, path, query, headers, body) {
  const url = new URL(baseUrl + path);
  if (query) {
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== '') url.searchParams.append(k, v);
    }
  }
  const parts = ['curl -X ' + method, JSON.stringify(url.toString())];
  if (headers) {
    for (const [k, v] of Object.entries(headers)) {
      parts.push('-H ' + JSON.stringify(k + ': ' + v));
    }
  }
  if (body) parts.push('-d ' + JSON.stringify(body));
  return parts.join(' \\\n  ');
}

export function buildFetchSnippet(baseUrl, method, path, query, headers, body) {
  const url = new URL(baseUrl + path);
  if (query) {
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== '') url.searchParams.append(k, v);
    }
  }
  const init = { method, headers: headers || {} };
  if (body) init.body = body;
  return 'await fetch(' + JSON.stringify(url.toString()) + ', ' + JSON.stringify(init, null, 2) + ');';
}
