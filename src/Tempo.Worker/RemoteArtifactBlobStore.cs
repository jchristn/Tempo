namespace Tempo.Worker
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core;
    using Tempo.Core.Artifacts;

    /// <summary>
    /// Read-only artifact blob store that downloads artifact packages from Tempo.Server for one active assignment.
    /// </summary>
    public sealed class RemoteArtifactBlobStore : IArtifactBlobStore, IDisposable
    {
        private readonly HttpClient _Client;
        private readonly string _WorkerId;
        private readonly string _WorkerToken;
        private readonly string _RunAssignmentId;
        private readonly string _LeaseToken;

        /// <summary>Instantiate.</summary>
        public RemoteArtifactBlobStore(
            string serverEndpoint,
            string workerId,
            string workerToken,
            string runAssignmentId,
            string leaseToken,
            int timeoutMs)
        {
            if (string.IsNullOrWhiteSpace(serverEndpoint)) throw new ArgumentNullException(nameof(serverEndpoint));
            if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentNullException(nameof(workerId));
            if (string.IsNullOrWhiteSpace(workerToken)) throw new ArgumentNullException(nameof(workerToken));
            if (string.IsNullOrWhiteSpace(runAssignmentId)) throw new ArgumentNullException(nameof(runAssignmentId));
            if (string.IsNullOrWhiteSpace(leaseToken)) throw new ArgumentNullException(nameof(leaseToken));

            _Client = new HttpClient
            {
                BaseAddress = new Uri(serverEndpoint.TrimEnd('/') + "/", UriKind.Absolute),
                Timeout = TimeSpan.FromMilliseconds(Math.Max(1000, timeoutMs))
            };
            _WorkerId = workerId;
            _WorkerToken = workerToken;
            _RunAssignmentId = runAssignmentId;
            _LeaseToken = leaseToken;
        }

        /// <inheritdoc />
        public string GetStorageKey(string tenantId, string sha256)
        {
            return tenantId + "/" + sha256;
        }

        /// <inheritdoc />
        public Task<ArtifactBlobWriteResult> PutAsync(string tenantId, string sha256, Stream content, long contentLength, CancellationToken token = default)
        {
            throw new NotSupportedException("RemoteArtifactBlobStore is read-only.");
        }

        /// <inheritdoc />
        public async Task<Stream> OpenReadAsync(string tenantId, string sha256, CancellationToken token = default)
        {
            string path = "v1.0/workers/artifacts/" + Uri.EscapeDataString(tenantId) + "/blobs/" + Uri.EscapeDataString(sha256) +
                "/download?runAssignmentId=" + Uri.EscapeDataString(_RunAssignmentId) +
                "&leaseToken=" + Uri.EscapeDataString(_LeaseToken);

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.TryAddWithoutValidation(Constants.HeaderWorkerId, _WorkerId);
            request.Headers.TryAddWithoutValidation(Constants.HeaderWorkerToken, _WorkerToken);

            HttpResponseMessage response = await _Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                response.Dispose();
                throw new FileNotFoundException("Artifact blob was not found for tenant '" + tenantId + "' sha '" + sha256 + "'.");
            }
            if (!response.IsSuccessStatusCode)
            {
                string body = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                response.Dispose();
                throw new InvalidOperationException("Artifact download failed with status " + (int)response.StatusCode + ": " + body);
            }

            Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            return new ResponseDisposingStream(stream, response);
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string tenantId, string sha256, CancellationToken token = default)
        {
            throw new NotSupportedException("RemoteArtifactBlobStore does not support existence probes.");
        }

        /// <inheritdoc />
        public Task<bool> DeleteAsync(string tenantId, string sha256, CancellationToken token = default)
        {
            throw new NotSupportedException("RemoteArtifactBlobStore is read-only.");
        }

        /// <inheritdoc />
        public Task<long> TenantBytesAsync(string tenantId, CancellationToken token = default)
        {
            throw new NotSupportedException("RemoteArtifactBlobStore does not report tenant byte counts.");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _Client.Dispose();
        }

        private sealed class ResponseDisposingStream : Stream
        {
            private readonly Stream _Inner;
            private readonly HttpResponseMessage _Response;

            public ResponseDisposingStream(Stream inner, HttpResponseMessage response)
            {
                _Inner = inner;
                _Response = response;
            }

            public override bool CanRead => _Inner.CanRead;
            public override bool CanSeek => _Inner.CanSeek;
            public override bool CanWrite => _Inner.CanWrite;
            public override long Length => _Inner.Length;
            public override long Position { get => _Inner.Position; set => _Inner.Position = value; }

            public override void Flush() => _Inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _Inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _Inner.Seek(offset, origin);
            public override void SetLength(long value) => _Inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _Inner.Write(buffer, offset, count);
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _Inner.ReadAsync(buffer, offset, count, cancellationToken);
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _Inner.ReadAsync(buffer, cancellationToken);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _Inner.Dispose();
                    _Response.Dispose();
                }
                base.Dispose(disposing);
            }

            public override async ValueTask DisposeAsync()
            {
                await _Inner.DisposeAsync().ConfigureAwait(false);
                _Response.Dispose();
                await base.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
