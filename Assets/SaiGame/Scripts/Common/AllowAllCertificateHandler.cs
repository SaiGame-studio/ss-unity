using UnityEngine.Networking;

namespace SaiGame.Services
{
    public class AllowAllCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }
}