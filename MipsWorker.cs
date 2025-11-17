using Microsoft.ApplicationInsights;
using Microsoft.InformationProtection;
using Microsoft.InformationProtection.Exceptions;
using Microsoft.InformationProtection.File;
using Microsoft.InformationProtection.Protection;
using MipsTestApp.Extensions;
using MipsTestApp.Models;
using MipsTestApp.Models.Protection.File.Content;
using System.Buffers;

namespace MipsTestApp.Services.Protection
{
    public class MipsWorker(IFileEngine FileEngine, TelemetryClient LoggingClient)
    {
        internal async Task<MipResult> GetFileMipResult(FileUpload upload, bool republishUnprotected = false, LabelSensitivityValue maxSupportedSensitivity = LabelSensitivityValue.Medium)
        {
            ArgumentNullException.ThrowIfNull(upload?.File, nameof(upload));
            var fileName = Path.GetFileName(upload.File.FileName);
            using Stream readStream = upload.File.OpenReadStream();
            readStream.Position = 0;
            return await GetFileMipResult(readStream, fileName, upload.GetExtension(), republishUnprotected, maxSupportedSensitivity);
        }

        /// <summary>
        /// Processes a file to determine its MIP (Microsoft Information Protection) result.
        /// This includes validating the file, checking its protection status, and optionally republishing it as unprotected.
        /// If the file is protected and republishing is requested, the function attempts to remove protection and apply a default sensitivity label.
        /// The function also validates the file's MIME type and ensures it meets the required sensitivity level.
        /// </summary>
        /// <param name="incomingStream"></param>
        /// <param name="fileName"></param>
        /// <param name="extension"></param>
        /// <param name="republishUnprotected">Indicates whether to republish the file as unprotected if it is protected.</param>
        /// <param name="maxSupportedSensitivity">The maximum sensitivity level supported for the input file.</param>
        /// <exception cref="ArgumentNullException">Thrown if the model is null.</exception>
        /// <exception cref="ArgumentException">Thrown if republishing unprotected is requested while protection is not supported.</exception>
        /// <returns>
        /// A MipResult object containing details about the file, including its protection status, any invalid reasons, and republished file details if applicable.
        /// </returns>
        internal async Task<MipResult> GetFileMipResult(Stream incomingStream, string fileName, string extension, bool republishUnprotected = false, LabelSensitivityValue maxSupportedSensitivity = LabelSensitivityValue.Medium)
        {
            ArgumentNullException.ThrowIfNull(incomingStream, nameof(incomingStream));
            ArgumentNullException.ThrowIfNull(extension, nameof(extension));
            ArgumentNullException.ThrowIfNull(fileName, nameof(fileName));

            string tempPath = null;
            LoggingClient.TrackTrace($"Get Mip Result for {fileName}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
            MipRepublishResult RepublishResult = null;

            // Read the original bytes from the incomingStream, as that stream is owned and managed by the container
            // It needs to be copied otherwise the container messes with it before it should.

            byte[] OriginalBytes;

            //The Container usually owns the incomingStream and can dispose it before we're done with it.. so copy to our own stream.
            using MemoryStream memoryStream = new();
            await incomingStream.CopyToAsync(memoryStream);
            OriginalBytes = memoryStream.ToArray();
            memoryStream.Position = 0;// Reset the stream position for future processes

            MipResult Result = null;
            IFileHandler handler = null; // Declare the handler outside the try block for disposal in finally
            IFileHandler RepublishHandler = null; // Declare the handler outside the try block for disposal in finally
            string ReprotectedFilePath = null;
            try
            {
                // Create the file handler / This will throw if the user does not have at least Read access to the file 
                handler = await FileEngine.CreateFileHandlerAsync(memoryStream, fileName, true);
                LoggingClient.TrackTrace($"IFileHandler created", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);

                ContentLabel FileContentLabel = handler.Label;
                var (InvalidReasons, DecryptedBytes) = await TryToProcessFile(handler, LoggingClient, OriginalBytes, extension, FileContentLabel, maxSupportedSensitivity);

                if (republishUnprotected && InvalidReasons.Count == 0 && handler.Protection is not null) // original is protected, there are no errors for this user.
                {
                    IProtectionHandler OriginalProtectionHandler = handler.Protection;
                    string ReprotectedFileName = $"{Guid.NewGuid()}{extension}";

                    LoggingClient.TrackTrace($"ReprotectedFileName: {ReprotectedFileName}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);

                    //create a new memory stream from the decrypted bytes.
                    using MemoryStream decryptedMemoryStream = new(DecryptedBytes);

                    RepublishHandler = await FileEngine.CreateFileHandlerAsync(decryptedMemoryStream, ReprotectedFileName, false);
                    LoggingClient.TrackTrace($"RepublishHandler created", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);

                    LabelingOptions labelingOptions = new()
                    {
                        AssignmentMethod = AssignmentMethod.Privileged,
                        IsDowngradeJustified = true,
                        JustificationMessage = "Pdfs require a version that is Unclassified"
                    };

                    Label UnclassifiedLabel = FileEngine.SensitivityLabels?.FirstOrDefault(l => l.Sensitivity == (int)LabelSensitivityValue.UnClassified);
                    try
                    {
                        RepublishHandler.SetLabel(FileEngine.DefaultSensitivityLabel ?? UnclassifiedLabel, labelingOptions, new ProtectionSettings());
                    }
                    catch (Exception ex) when (ex is JustificationRequiredException or PrivilegedRequiredException)
                    { //if it throws.. just catch and try again .. this is more for standard and no justification.
                        labelingOptions.AssignmentMethod = AssignmentMethod.Privileged;
                        labelingOptions.IsDowngradeJustified = true;
                        labelingOptions.JustificationMessage = "Pdfs require a version that is Unclassified";
                        RepublishHandler.SetLabel(FileEngine.DefaultSensitivityLabel ?? UnclassifiedLabel, labelingOptions, new ProtectionSettings());
                    }

                    byte[] UnprotectedBytes = null;
                    int Mb = 100;
                    //Our MAX file size is < 100 mb and the average is much smaller < 10mb so using a memory stream should be okay.
                    //But if we start to support largr - switch to a FileStream
                    if (decryptedMemoryStream.Length > Mb * 1024 * 1024) // MB variable in bytes
                    {
                        ReprotectedFilePath = Path.Combine(Path.GetTempPath(), ReprotectedFileName);
                        LoggingClient.TrackTrace($"Calling RepublishHandler.CommitAsync for {ReprotectedFilePath}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
                        var unprotectedResult = await RepublishHandler.CommitAsync(ReprotectedFilePath); // Write changes to the new path
                        if (unprotectedResult)
                        {
                            using FileStream NewFileStream = new(ReprotectedFilePath, FileMode.Open, FileAccess.Read);
                            UnprotectedBytes = await NewFileStream.ReadAllBytesAsync();
                            RepublishResult = new MipRepublishResult(UnprotectedBytes, true);
                        }
                    }
                    else
                    {
                        using MemoryStream outputStream = new();
                        var unprotectedResult = await RepublishHandler.CommitAsync(outputStream);// Write changes to the output stream
                        if (unprotectedResult)
                        {
                            outputStream.Position = 0; // Reset the stream position to read the written bytes
                            UnprotectedBytes = outputStream.ToArray(); // Read all bytes from the MemoryStream
                            RepublishResult = new MipRepublishResult(UnprotectedBytes, true);
                        }
                    }
                }
                Result = new(fileName, OriginalBytes, InvalidReasons.Count == 0, handler.Protection is not null, DecryptedBytes, FileContentLabel, InvalidReasons, RepublishResult);
            }
            catch (AggregateException ae) when (ae.InnerExceptions.Any(ex => ex is AccessDeniedException))
            {
                Result = new(fileName, OriginalBytes, false, false, null, null, [InvalidReason.AccessDenied]);
                LoggingClient.TrackTrace("User does not have edit access to the file being uploaded",
                                                Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error,
                                                new Dictionary<string, string>() {
                                                            { "Task", "MipService.GetFileMipResult" } });
            }
            catch (NoPermissionsException noPermissions)
            {
                LoggingClient.TrackTrace("User does not have read access to the file being uploaded",
                                                Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error,
                                                new Dictionary<string, string>() {
                                                            { "Task", "MipService.GetFileMipResult" },
                                                            { "Details", noPermissions.Message } });
                Result = new(fileName, OriginalBytes, false, false, null, null, [InvalidReason.AccessDenied]);
            }
            catch (NotSupportedException notSupported) // this happens when the file type is not supported by MIP.. like a .txt file
            {
                LoggingClient.TrackTrace("The file has been protected using non RMS technologies",
                                                Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error,
                                                new Dictionary<string, string>() {
                                                            { "Task", "MipService.GetFileMipResult" },
                                                            { "Details", notSupported.Message } });
                Result = new(fileName, OriginalBytes, false, false, null, null, [InvalidReason.ProtectionIsNotSupported]);
            }
            catch (Exception e)
            {
                LoggingClient.TrackException(e,
                                                new Dictionary<string, string>() {
                                                            { "Task", "MipService.GetFileMipResult" } });
                Result = new(fileName, OriginalBytes, false, false, null, null, [InvalidReason.UnKnown]);
            }
            finally
            {
                // Dispose the handler to release any locks
                try
                {
                    LoggingClient.TrackTrace($"Start cleanup", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
                    handler?.Dispose();
                    RepublishHandler?.Dispose();
                }
                catch { } // swallow 

                // Delete the temp files after the handlers are disposed
                if (tempPath is not null)
                {
                    try
                    {
                        File.Delete(tempPath);
                        LoggingClient.TrackTrace($"Finished Deleting file with path {tempPath}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
                    }
                    catch { } // swallow delete failures
                    try // if tempPath was set then ReprotectedFilePath was also created.. so delete it as well.
                    {
                        File.Delete(ReprotectedFilePath);
                        LoggingClient.TrackTrace($"Finished Deleting file with path {ReprotectedFilePath}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
                    }
                    catch { } //swallow delete failures
                }
                LoggingClient?.TrackTrace($"Finish cleanup", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
            }
            return Result;
        }

        private static async Task<(List<InvalidReason> InvalidReasons, byte[] DecryptedBytes)> TryToProcessFile(IFileHandler handler,
                                                                                                                TelemetryClient loggingClient,
                                                                                                                byte[] unencryptedBytes,
                                                                                                                string extension,
                                                                                                                ContentLabel fileContentLabel,
                                                                                                                LabelSensitivityValue? MaxLevel = LabelSensitivityValue.Medium,
                                                                                                                bool toEdit = false,
                                                                                                                bool toUnprotect = false)
        {
            IProtectionHandler ProtectionHandler = handler.Protection;
            List<InvalidReason> InvalidReasons = [];
            byte[] DecryptedBytes = null;
            if (ProtectionHandler is null)
            {
                if (toUnprotect)
                {
                    InvalidReasons.Add(InvalidReason.AlreadyUnprotected);
                }
                if (!ContentValidation.IsMimeTypeContentMatching(unencryptedBytes, extension))
                {
                    InvalidReasons.Add(InvalidReason.ContentType);
                }
            }
            else
            {
                if (!ProtectionType.TemplateBased.Equals(ProtectionHandler.ProtectionDescriptor.ProtectionType))
                {
                    InvalidReasons.Add(InvalidReason.ProtectionType);
                }
                if (MaxLevel.HasValue && fileContentLabel.Label.Sensitivity > (int)MaxLevel.Value)
                {
                    InvalidReasons.Add(InvalidReason.SensitivityLevel);
                }
                if (toEdit && !ProtectionHandler.AccessCheck("Edit")) //Do we need edit.. or just check if allowed to extract
                {
                    InvalidReasons.Add(InvalidReason.NotEditor);
                }
                if (toEdit && !ProtectionHandler.AccessCheck("Extract"))
                {
                    InvalidReasons.Add(InvalidReason.NotExtractor);
                }
                if (InvalidReasons.Count == 0)
                {
                    loggingClient.TrackTrace($"No invalid reasons found, file is protected, attempting to decrypt", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);

                    //Add a timer so I can track how long it takes to decrypt the file
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    //best option
                    // DecryptedBytes = await File.ReadAllBytesAsync(await handler.GetDecryptedTemporaryFileAsync());
                    #region old attempts took too long!

                    using Stream decryptedStream = await handler.GetDecryptedTemporaryStreamAsync();
                    decryptedStream.Position = 0;

                    // Optimize for large files by avoiding MemoryStream and using preallocated buffers
                    if (decryptedStream.CanSeek && decryptedStream.Length > 0) //This appears to always be true in our case
                    {
                        DecryptedBytes = new byte[decryptedStream.Length];
                        await decryptedStream.ReadAsync(DecryptedBytes.AsMemory(0, DecryptedBytes.Length)).ConfigureAwait(false);
                    }
                    else
                    {
                        // Use ArrayPool for efficient memory usage
                        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192); // 8 KB chunk size
                        try
                        {
                            using MemoryStream memoryStream = new();
                            int bytesRead;
                            while ((bytesRead = await decryptedStream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false)) > 0)
                            {
                                memoryStream.Write(buffer, 0, bytesRead);
                            }
                            DecryptedBytes = memoryStream.ToArray();
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }
                    #endregion
                    stopwatch.Stop();
                    loggingClient.TrackTrace($"DecryptedBytes read into byte array, and it took {stopwatch.ElapsedMilliseconds} milliseconds", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
                    if (!ContentValidation.IsMimeTypeContentMatching(DecryptedBytes, extension))
                    {
                        InvalidReasons.Add(InvalidReason.ContentType);
                    }
                }
            }
            return (InvalidReasons, DecryptedBytes);
        }

       
    }
}
