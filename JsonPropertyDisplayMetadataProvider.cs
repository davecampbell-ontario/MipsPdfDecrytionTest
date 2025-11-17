using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Newtonsoft.Json;

namespace MipsTestApp.Models
{
    public class JsonPropertyDisplayMetadataProvider : IDisplayMetadataProvider
    {
        public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        {
            var attributes = context.Attributes;
            var jsonPropertyAttribute = attributes.OfType<JsonPropertyAttribute>().FirstOrDefault();
            var displayMetadata = context.DisplayMetadata;
            displayMetadata.DisplayName = jsonPropertyAttribute is null
                ? (() => "MISSING PROPERTY NAME")
                : (() => jsonPropertyAttribute.PropertyName);
        }
    }
}
