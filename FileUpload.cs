using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace MipsTestApp.Models
{
    //   [ModelBinder(typeof(JsonWithFilesFormDataModelBinder), Name = "json")]
    public class FileUpload
    {
        protected string Extension = null;
        public virtual string GetExtension()
        {
            if (Extension is null && File?.FileName is not null)
            {
                var fileName = Path.GetFileName(File.FileName);
                Extension = Path.GetExtension(fileName);
            }
            return Extension ?? string.Empty;
        }

        public bool IsPdfExtension()
        {
            var ext = GetExtension();
            return ext is not null && ".pdf".Equals(ext, StringComparison.InvariantCultureIgnoreCase);
        }

        [DataMember]
        [Required]
        [FromForm(Name = "file")]
        public IFormFile File { get; set; }
    }
}
