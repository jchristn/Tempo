/**
 * API client for communicating with the Tempo Server.
 */
class ApiError extends Error {
  constructor(status, body) {
    super('HTTP ' + status + ': ' + body);
    this.status = status;
    this.body = body;
  }
}

class ApiClient {
  constructor(baseUrl, token = null) {
    this.baseUrl = (baseUrl || '').replace(/\/$/, '');
    this.token = token;
  }

  _headers(extra = {}) {
    const headers = { 'Content-Type': 'application/json', ...extra };
    if (this.token) headers['Authorization'] = 'Bearer ' + this.token;
    return headers;
  }

  _authHeaders(extra = {}) {
    const headers = { ...extra };
    if (this.token) headers['Authorization'] = 'Bearer ' + this.token;
    return headers;
  }

  async _request(method, path, { query = null, body = null, headers = {} } = {}) {
    const url = new URL(this.baseUrl + path);
    if (query) {
      for (const [k, v] of Object.entries(query)) {
        if (v !== undefined && v !== null && v !== '') url.searchParams.append(k, v);
      }
    }

    const init = { method, headers: this._headers(headers) };
    if (body !== null && body !== undefined) init.body = typeof body === 'string' ? body : JSON.stringify(body);

    const response = await fetch(url.toString(), init);

    if (response.status === 401) {
      window.dispatchEvent(new CustomEvent('auth:unauthorized'));
    }

    if (!response.ok) {
      const errorBody = await response.text().catch(() => '');
      throw new ApiError(response.status, errorBody || response.statusText);
    }

    if (response.status === 204) return null;
    const ct = response.headers.get('content-type') || '';
    if (ct.includes('application/json')) return response.json();
    return response.text();
  }

  async healthCheck() { return this._request('GET', '/v1.0/api/health'); }

  async validateToken() {
    await this._request('GET', '/v1.0/api/health');
    return true;
  }

  async login(email, password, tenantId = null) {
    return this._request('POST', '/v1.0/token', { body: { email, password, tenantId } });
  }

  async me() { return this._request('GET', '/v1.0/me'); }

  async getOpenApiSpec() { return this._request('GET', '/openapi.json'); }

  async executeExplorer({ method, path, query, headers, body }) {
    const url = new URL(this.baseUrl + path);
    if (query) {
      for (const [k, v] of Object.entries(query)) {
        if (v !== undefined && v !== null && v !== '') url.searchParams.append(k, v);
      }
    }
    return fetch(url.toString(), {
      method,
      headers: this._headers(headers || {}),
      body: body !== null && body !== undefined ? body : undefined
    });
  }

  // Request history
  async getRequestHistory(filters = {}) { return this._request('GET', '/v1.0/api/request-history', { query: filters }); }
  async getRequestHistorySummary(filters = {}) { return this._request('GET', '/v1.0/api/request-history/summary', { query: filters }); }
  async getRequestHistoryEntry(id) { return this._request('GET', '/v1.0/api/request-history/' + encodeURIComponent(id)); }
  async deleteRequestHistoryEntry(id) { return this._request('DELETE', '/v1.0/api/request-history/' + encodeURIComponent(id)); }
  async deleteRequestHistoryBulk(filters = {}) { return this._request('DELETE', '/v1.0/api/request-history', { query: filters }); }

  // Runtime status
  async listRuntimes() { return this._request('GET', '/v1.0/runtimes'); }
  async readRuntime(runtimeKey) { return this._request('GET', '/v1.0/runtimes/' + encodeURIComponent(runtimeKey)); }
  async listTenantRuntimes(tenantId) { return this._request('GET', this._ten(tenantId, '/runtimes')); }
  async validateRuntime(tenantId, body) { return this._request('POST', this._ten(tenantId, '/runtimes/validate'), { body }); }
  async getExternalExecutionStatus() { return this._request('GET', '/v1.0/runtimes/external-execution'); }
  async getTenantExternalExecutionStatus(tenantId) { return this._request('GET', this._ten(tenantId, '/runtimes/external-execution')); }

  // Tenants
  async listTenants(filters = {}) { return this._request('GET', '/v1.0/tenants', { query: filters }); }
  async readTenant(id) { return this._request('GET', '/v1.0/tenants/' + encodeURIComponent(id)); }
  async createTenant(body) { return this._request('POST', '/v1.0/tenants', { body }); }
  async updateTenant(id, body) { return this._request('PUT', '/v1.0/tenants/' + encodeURIComponent(id), { body }); }
  async deleteTenant(id) { return this._request('DELETE', '/v1.0/tenants/' + encodeURIComponent(id)); }

  // Accounts
  async listAccounts(filters = {}) { return this._request('GET', '/v1.0/accounts', { query: filters }); }
  async createAccount(body) { return this._request('POST', '/v1.0/accounts', { body }); }
  async updateAccount(id, body) { return this._request('PUT', '/v1.0/accounts/' + encodeURIComponent(id), { body }); }
  async deleteAccount(id) { return this._request('DELETE', '/v1.0/accounts/' + encodeURIComponent(id)); }

  // Administrators
  async listAdmins(filters = {}) { return this._request('GET', '/v1.0/admins', { query: filters }); }
  async createAdmin(body) { return this._request('POST', '/v1.0/admins', { body }); }
  async updateAdmin(id, body) { return this._request('PUT', '/v1.0/admins/' + encodeURIComponent(id), { body }); }
  async deleteAdmin(id) { return this._request('DELETE', '/v1.0/admins/' + encodeURIComponent(id)); }

  // Tenant-scoped helpers
  _ten(tenantId, path) { return '/v1.0/tenants/' + encodeURIComponent(tenantId) + path; }

  async listUsers(tenantId, filters = {}) { return this._request('GET', this._ten(tenantId, '/users'), { query: filters }); }
  async createUser(tenantId, body) { return this._request('POST', this._ten(tenantId, '/users'), { body }); }
  async updateUser(tenantId, id, body) { return this._request('PUT', this._ten(tenantId, '/users/' + encodeURIComponent(id)), { body }); }
  async deleteUser(tenantId, id) { return this._request('DELETE', this._ten(tenantId, '/users/' + encodeURIComponent(id))); }

  async listCredentials(tenantId, filters = {}) { return this._request('GET', this._ten(tenantId, '/credentials'), { query: filters }); }
  async createCredential(tenantId, body) { return this._request('POST', this._ten(tenantId, '/credentials'), { body }); }
  async updateCredential(tenantId, id, body) { return this._request('PUT', this._ten(tenantId, '/credentials/' + encodeURIComponent(id)), { body }); }
  async deleteCredential(tenantId, id) { return this._request('DELETE', this._ten(tenantId, '/credentials/' + encodeURIComponent(id))); }

  async listRoles(tenantId, filters = {}) { return this._request('GET', this._ten(tenantId, '/roles'), { query: filters }); }
  async createRole(tenantId, body) { return this._request('POST', this._ten(tenantId, '/roles'), { body }); }
  async updateRole(tenantId, id, body) { return this._request('PUT', this._ten(tenantId, '/roles/' + encodeURIComponent(id)), { body }); }
  async deleteRole(tenantId, id) { return this._request('DELETE', this._ten(tenantId, '/roles/' + encodeURIComponent(id))); }

  async listUserRoles(tenantId, userId) { return this._request('GET', this._ten(tenantId, '/users/' + encodeURIComponent(userId) + '/roles')); }
  async createUserRoleMap(tenantId, body) { return this._request('POST', this._ten(tenantId, '/user-role-maps'), { body }); }
  async deleteUserRoleMap(tenantId, id) { return this._request('DELETE', this._ten(tenantId, '/user-role-maps/' + encodeURIComponent(id))); }

  async listPermissions(tenantId, filters = {}) { return this._request('GET', this._ten(tenantId, '/permissions'), { query: filters }); }
  async createPermission(tenantId, body) { return this._request('POST', this._ten(tenantId, '/permissions'), { body }); }
  async updatePermission(tenantId, id, body) { return this._request('PUT', this._ten(tenantId, '/permissions/' + encodeURIComponent(id)), { body }); }
  async deletePermission(tenantId, id) { return this._request('DELETE', this._ten(tenantId, '/permissions/' + encodeURIComponent(id))); }

  async listFlows(tenantId, filters = {}) { return this._request('GET', this._ten(tenantId, '/flows'), { query: filters }); }
  async readFlow(tenantId, id) { return this._request('GET', this._ten(tenantId, '/flows/' + encodeURIComponent(id))); }
  async createFlow(tenantId, body) { return this._request('POST', this._ten(tenantId, '/flows'), { body }); }
  async updateFlow(tenantId, id, body) { return this._request('PUT', this._ten(tenantId, '/flows/' + encodeURIComponent(id)), { body }); }
  async deleteFlow(tenantId, id) { return this._request('DELETE', this._ten(tenantId, '/flows/' + encodeURIComponent(id))); }
  async runFlow(tenantId, id, body = {}) { return this._request('POST', this._ten(tenantId, '/flows/' + encodeURIComponent(id) + '/runs'), { body }); }
  async ensureFlowSteps(tenantId, id) { return this._request('POST', this._ten(tenantId, '/flows/' + encodeURIComponent(id) + '/ensure-steps')); }
  async bulkDeleteFlows(tenantId, ids) { return this._request('POST', this._ten(tenantId, '/flows/bulk-delete'), { body: { ids } }); }
  async bulkDeleteSteps(tenantId, ids) { return this._request('POST', this._ten(tenantId, '/steps/bulk-delete'), { body: { ids } }); }
  async bulkDeleteTriggers(tenantId, ids) { return this._request('POST', this._ten(tenantId, '/triggers/bulk-delete'), { body: { ids } }); }
  async bulkDeleteRuns(tenantId, ids) { return this._request('POST', this._ten(tenantId, '/runs/bulk-delete'), { body: { ids } }); }
  async getSettings() { return this._request('GET', '/v1.0/settings'); }
  async saveSettings(settings) { return this._request('PUT', '/v1.0/settings', { body: settings }); }
  async migrateInlineRest(scope = {}) { return this._request('POST', '/v1.0/migrations/inline-rest', { body: scope || {} }); }

  async listSteps(tenantId, filters = {}) { return this._request('GET', this._ten(tenantId, '/steps'), { query: filters }); }
  async createStep(tenantId, body) { return this._request('POST', this._ten(tenantId, '/steps'), { body }); }
  async createSourceStep(tenantId, body) { return this._request('POST', this._ten(tenantId, '/steps/source'), { body }); }
  async updateStep(tenantId, id, body) { return this._request('PUT', this._ten(tenantId, '/steps/' + encodeURIComponent(id)), { body }); }
  async deleteStep(tenantId, id) { return this._request('DELETE', this._ten(tenantId, '/steps/' + encodeURIComponent(id))); }
  async listRegisteredSteps() { return this._request('GET', '/v1.0/steps/registered'); }

  async listArtifacts(tenantId, filters = {}) { return this._request('GET', this._ten(tenantId, '/artifacts'), { query: filters }); }
  async readArtifact(tenantId, id) { return this._request('GET', this._ten(tenantId, '/artifacts/' + encodeURIComponent(id))); }
  async createArtifact(tenantId, body) { return this._request('POST', this._ten(tenantId, '/artifacts'), { body }); }
  async updateArtifact(tenantId, id, body) { return this._request('PUT', this._ten(tenantId, '/artifacts/' + encodeURIComponent(id)), { body }); }
  async deleteArtifact(tenantId, id) { return this._request('DELETE', this._ten(tenantId, '/artifacts/' + encodeURIComponent(id))); }
  async listArtifactFiles(tenantId, artifactId) { return this._request('GET', this._ten(tenantId, '/artifacts/' + encodeURIComponent(artifactId) + '/files')); }
  async readArtifactFile(tenantId, artifactId, path) { return this._request('GET', this._ten(tenantId, '/artifacts/' + encodeURIComponent(artifactId) + '/files/content'), { query: { path } }); }
  async saveArtifactFile(tenantId, artifactId, path, body) { return this._request('PUT', this._ten(tenantId, '/artifacts/' + encodeURIComponent(artifactId) + '/files/content'), { query: { path }, body }); }
  async deleteArtifactFile(tenantId, artifactId, path) { return this._request('DELETE', this._ten(tenantId, '/artifacts/' + encodeURIComponent(artifactId) + '/files/content'), { query: { path } }); }
  async listArtifactVersions(tenantId, artifactId, filters = {}) { return this._request('GET', this._ten(tenantId, '/artifacts/' + encodeURIComponent(artifactId) + '/versions'), { query: filters }); }
  async readArtifactVersion(tenantId, artifactId, version) { return this._request('GET', this._ten(tenantId, '/artifacts/' + encodeURIComponent(artifactId) + '/versions/' + encodeURIComponent(version))); }
  async deleteArtifactVersion(tenantId, artifactId, version) { return this._request('DELETE', this._ten(tenantId, '/artifacts/' + encodeURIComponent(artifactId) + '/versions/' + encodeURIComponent(version))); }
  async uploadArtifactVersion(tenantId, artifactId, body, metadata = {}) {
    const query = {
      version: metadata.version,
      sha256: metadata.sha256,
      originalFileName: metadata.originalFileName,
      contentType: metadata.contentType
    };
    const url = new URL(this.baseUrl + this._ten(tenantId, '/artifacts/' + encodeURIComponent(artifactId) + '/versions'));
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== '') url.searchParams.append(k, v);
    }
    const headers = {};
    if (metadata.contentType) headers['Content-Type'] = metadata.contentType;
    if (metadata.manifestJson) headers['x-artifact-manifest'] = metadata.manifestJson;
    const response = await fetch(url.toString(), { method: 'POST', headers: this._authHeaders(headers), body });
    if (!response.ok) {
      const errorBody = await response.text().catch(() => '');
      throw new ApiError(response.status, errorBody || response.statusText);
    }
    return response.json();
  }
  async downloadArtifactVersion(tenantId, artifactId, version) {
    const response = await fetch(this.baseUrl + this._ten(tenantId, '/artifacts/' + encodeURIComponent(artifactId) + '/versions/' + encodeURIComponent(version) + '/download'), {
      method: 'GET',
      headers: this._authHeaders()
    });
    if (!response.ok) {
      const errorBody = await response.text().catch(() => '');
      throw new ApiError(response.status, errorBody || response.statusText);
    }
    return response.blob();
  }

  async listTriggers(tenantId, filters = {}) { return this._request('GET', this._ten(tenantId, '/triggers'), { query: filters }); }
  async createTrigger(tenantId, body) { return this._request('POST', this._ten(tenantId, '/triggers'), { body }); }
  async updateTrigger(tenantId, id, body) { return this._request('PUT', this._ten(tenantId, '/triggers/' + encodeURIComponent(id)), { body }); }
  async deleteTrigger(tenantId, id) { return this._request('DELETE', this._ten(tenantId, '/triggers/' + encodeURIComponent(id))); }
  async fireHttpTrigger(id, body = null, method = 'POST') { return this._request(method, '/v1.0/triggers/http/' + encodeURIComponent(id), { body }); }
  async fireHttpTriggerDetailed(id, body = null, method = 'POST') {
    const init = { method, headers: body === null || body === undefined ? this._authHeaders() : this._headers() };
    if (body !== null && body !== undefined) init.body = typeof body === 'string' ? body : JSON.stringify(body);
    const response = await fetch(this.baseUrl + '/v1.0/triggers/http/' + encodeURIComponent(id), init);
    const responseBody = response.status === 204 ? '' : await response.text();
    const headers = {};
    response.headers.forEach((value, key) => { headers[key] = value; });
    if (!response.ok) throw new ApiError(response.status, responseBody || response.statusText);
    return { status: response.status, body: responseBody, headers };
  }

  async listRuns(tenantId, filters = {}) { return this._request('GET', this._ten(tenantId, '/runs'), { query: filters }); }
  async readRun(tenantId, id) { return this._request('GET', this._ten(tenantId, '/runs/' + encodeURIComponent(id))); }
  async readRunSteps(tenantId, id) { return this._request('GET', this._ten(tenantId, '/runs/' + encodeURIComponent(id) + '/steps')); }
  async cancelRun(tenantId, id) { return this._request('POST', this._ten(tenantId, '/runs/' + encodeURIComponent(id) + '/cancel')); }
  async deleteRun(tenantId, id) { return this._request('DELETE', this._ten(tenantId, '/runs/' + encodeURIComponent(id))); }
}

export default ApiClient;
export { ApiError };
