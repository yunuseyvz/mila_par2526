using UnityEngine.Networking;

namespace LanguageTutor.Utilities
{
    /// <summary>
    /// Certificate handler that accepts any certificate. Use only for local/dev endpoints.
    /// </summary>
    public class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }
}
