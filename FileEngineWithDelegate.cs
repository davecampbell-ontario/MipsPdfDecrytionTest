using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.InformationProtection;
using Microsoft.InformationProtection.File;
using Microsoft.InformationProtection.Policy;
using System.Collections.ObjectModel;


namespace MipsTestApp.Services.Protection
{
    public sealed class FileEngineWithDelegate(IFileProfile fileProfile, IFileEngine innerEngine, IAuthDelegate authDelegate, Identity id, TelemetryClient LoggingClient) : IFileEngine, IDisposable
    {
        private bool disposed = false;
        private readonly IFileEngine _innerEngine = innerEngine ?? throw new ArgumentNullException(nameof(innerEngine));        // Hold strong reference to avoid GC
        private readonly IAuthDelegate _authDelegate = authDelegate ?? throw new ArgumentNullException(nameof(authDelegate));   // Hold strong reference to avoid GC
        public Identity Id { get; init; } = id ?? throw new ArgumentNullException(nameof(id));

        ~FileEngineWithDelegate()
        {
            LoggingClient.TrackTrace("~FileEngineWithDelegate finalized (GC collected)", SeverityLevel.Verbose);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                string engineName = Id?.Name ?? string.Empty;

                LoggingClient.TrackTrace($"Delete FileEngine from FileProfile for user {engineName}", SeverityLevel.Verbose);
                try
                {
                    fileProfile?.DeleteEngineAsync(engineName).GetAwaiter().GetResult();
                    try
                    {
                        LoggingClient.TrackTrace($"Disposing FileEngineWithDelegate for user {engineName}", SeverityLevel.Verbose);
                        _innerEngine?.Dispose(); // Release native SDK delegate and buffers first
                    }
                    catch (Exception ex)
                    {
                        LoggingClient.TrackException(ex, properties: new Dictionary<string, string>
                        {
                            { "Event", "FileEngine.Dispose" },
                            { "EngineName", engineName }
                        });
                    }
                }
                catch (Exception ex)
                {
                    LoggingClient.TrackException(ex, properties: new Dictionary<string, string>
                        {
                            { "Event", "FileProfile.DeleteEngineAsync" },
                            { "EngineName", engineName }
                        });
                }
                disposed = true;
            }
        }

        // Properties
        FileEngineSettings IFileEngine.Settings => _innerEngine.Settings;
        ReadOnlyCollection<KeyValuePair<string, string>> IFileEngine.CustomSettings => _innerEngine.CustomSettings;
        Label IFileEngine.DefaultSensitivityLabel => _innerEngine.DefaultSensitivityLabel;
        ReadOnlyCollection<Label> IFileEngine.SensitivityLabels => _innerEngine.SensitivityLabels;
        ReadOnlyCollection<SensitivityTypesRulePackage> IFileEngine.SensitivityTypes => _innerEngine.SensitivityTypes;
        string IFileEngine.MoreInfoUrl => _innerEngine.MoreInfoUrl;
        public bool IsLabelingRequired => _innerEngine.IsLabelingRequired;
        public bool HasClassificationRules => _innerEngine.HasClassificationRules;
        public string PolicyFileId => _innerEngine.PolicyFileId;
        public string SensitivityTypesFileId => _innerEngine.SensitivityTypesFileId;

        DateTime IFileEngine.LastPolicyFetchTime => _innerEngine.LastPolicyFetchTime;

        public DateTimeOffset LastPolicyFetchTime => _innerEngine.LastPolicyFetchTime;
        public string PolicyDataXml => _innerEngine.PolicyDataXml;

        // Methods
        Label IFileEngine.GetLabelById(string id) => _innerEngine.GetLabelById(id);

        public bool HasWorkloadConsent(Workload workload) => _innerEngine.HasWorkloadConsent(workload);

        public void SendApplicationAuditEvent(string eventType, string dataType, string dataValue) => _innerEngine.SendApplicationAuditEvent(eventType, dataType, dataValue);

        public Task<IFileHandler> CreateFileHandlerAsync(Stream inputStream, string fileName, bool isAuditDiscoveryEnabled, FileExecutionState executionState, bool isLabelingMetadataApplicationSupported)
            => _innerEngine.CreateFileHandlerAsync(inputStream, fileName, isAuditDiscoveryEnabled, executionState, isLabelingMetadataApplicationSupported);

        public Task<IFileHandler> CreateFileHandlerAsync(string filePath, string actualFileName, bool isAuditDiscoveryEnabled, FileExecutionState executionState, bool isLabelingMetadataApplicationSupported)
            => _innerEngine.CreateFileHandlerAsync(filePath, actualFileName, isAuditDiscoveryEnabled, executionState, isLabelingMetadataApplicationSupported);


    }
}
