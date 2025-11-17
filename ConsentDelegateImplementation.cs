using Microsoft.InformationProtection;

namespace MipsTestApp.Models.Protection.File
{
    class ConsentDelegateImplementation : IConsentDelegate
    {
        public Consent GetUserConsent(string url)
        {
            return Consent.Accept;
        }
    }
}
