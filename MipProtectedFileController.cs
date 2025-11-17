using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MipsTestApp.Exceptions;
using MipsTestApp.Models;
using MipsTestApp.Services.Protection;

namespace MipsTestApp.Controllers
{
    [ApiController]
    [Produces("application/json")]
    [Route("[controller]")]
    public class MipProtectedFileController(TelemetryClient logger, MipsWorker mipsWorker) : ControllerBase
    {

        [DisableRequestSizeLimit] // Still need to set the MaziRequestBodySize in IIS in web.config
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> UploadFile([FromForm] FileUpload upload)
        {

            if (upload?.File == null || upload?.File.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }
            bool isPDF = upload.IsPdfExtension();
            logger?.TrackTrace($"Start Mips logic");
           
            MipResult mipResult = await mipsWorker.GetFileMipResult(upload, isPDF);
            if (!mipResult.IsValid
                ||
                (mipResult.ContentLabel?.Label?.Sensitivity ?? 0) > (int)LabelSensitivityValue.Low
                    && isPDF
                    && !(mipResult.RepublishResult?.IsValid ?? false)
               )
            {
                return BadRequest(new MipsInvalidException(mipResult.FileName, mipResult.InvalidReasons).GetUnprocessableEntityResult());
            }

            // Example: just return the file label
            return Ok(new { mipResult.ContentLabel?.Label });
        }
    }
}
