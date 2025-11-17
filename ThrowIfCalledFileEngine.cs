using Microsoft.InformationProtection;
using Microsoft.InformationProtection.File;
using Microsoft.InformationProtection.Policy;
using System.Collections.ObjectModel;

namespace MipsTestApp.Services.Protection
{
    public class ThrowIfCalledFileEngine : IFileEngine, IDisposable
    {
        private bool disposedValue;

        public string Id => "DefaultFileEngine";

        FileEngineSettings IFileEngine.Settings => throw new NotImplementedException();

        ReadOnlyCollection<KeyValuePair<string, string>> IFileEngine.CustomSettings => throw new NotImplementedException();

        Label IFileEngine.DefaultSensitivityLabel => throw new NotImplementedException();

        ReadOnlyCollection<Label> IFileEngine.SensitivityLabels => throw new NotImplementedException();

        ReadOnlyCollection<SensitivityTypesRulePackage> IFileEngine.SensitivityTypes => throw new NotImplementedException();

        string IFileEngine.MoreInfoUrl => throw new NotImplementedException();

        bool IFileEngine.IsLabelingRequired => throw new NotImplementedException();

        bool IFileEngine.HasClassificationRules => throw new NotImplementedException();

        string IFileEngine.PolicyFileId => throw new NotImplementedException();

        string IFileEngine.SensitivityTypesFileId => throw new NotImplementedException();

        DateTime IFileEngine.LastPolicyFetchTime => throw new NotImplementedException();

        string IFileEngine.PolicyDataXml => throw new NotImplementedException();



        public Task<IFileHandler> CreateFileHandlerAsync(Stream fileStream, string fileName, bool isEditable = false)
        {
            // Return a dummy or throw NotImplementedException as appropriate
            throw new NotImplementedException("DefaultFileEngine does not support file handling.");
        }

        public Task<IFileHandler> CreateFileHandlerAsync(string filePath, bool isEditable = false)
        {
            // Return a dummy or throw NotImplementedException as appropriate
            throw new NotImplementedException("DefaultFileEngine does not support file handling.");
        }

        public Task<IFileHandler> CreateFileHandlerAsync(byte[] fileBuffer, string fileName, bool isEditable = false)
        {
            // Return a dummy or throw NotImplementedException as appropriate
            throw new NotImplementedException("DefaultFileEngine does not support file handling.");
        }

        Task<IFileHandler> IFileEngine.CreateFileHandlerAsync(string inputFilePath, string actualFilePath, bool isAuditDiscoveryEnabled, FileExecutionState fileExecutionState, bool isGetSensitivityLabelAuditDiscoveryEnabled) => throw new NotImplementedException();
        Task<IFileHandler> IFileEngine.CreateFileHandlerAsync(Stream inputStream, string inputFilePath, bool isAuditDiscoveryEnabled, FileExecutionState fileExecutionState, bool isGetSensitivityLabelAuditDiscoveryEnabled) => throw new NotImplementedException();
        bool IFileEngine.HasWorkloadConsent(Workload workload) => throw new NotImplementedException();
        Label IFileEngine.GetLabelById(string id) => throw new NotImplementedException();
        void IFileEngine.SendApplicationAuditEvent(string level, string eventType, string eventData) => throw new NotImplementedException();

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DefaultFileEngine()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
