using Microsoft.AspNetCore.Mvc;
using MipsTestApp.Services.Protection;

namespace MipsTestApp.Exceptions
{
    [Serializable]
    public class MipsInvalidException : Exception
    {
        public IEnumerable<InvalidReason> Reasons { get; init; }
        public const string baseMessage = "Invalid MIP Result";
        public string FileName { get; init; }

        public MipsInvalidException(string fileName, IEnumerable<InvalidReason> reasons) : base(baseMessage)
        {
            FileName = fileName ?? string.Empty;
            Reasons = reasons ?? [];
        }

        public UnprocessableEntityObjectResult GetUnprocessableEntityResult()
        {
            object result = new
            {
                FileName,
                Message,
                Reasons = Reasons?.Select(ir => ir.ToString())
            };
            return new UnprocessableEntityObjectResult(result);
        }
    }
}
