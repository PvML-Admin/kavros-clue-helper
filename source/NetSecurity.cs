using System;
using System.Net;
using System.Runtime.Versioning;

// csc does not emit this. Without it the 4.8 runtime treats the exe as a
// .NET Framework 4.0 app and leaves ServicePointManager on SSL 3.0 / TLS 1.0.
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace KavrosClueHelper
{
    internal static class NetSecurity
    {
        internal static void EnableModernTls()
        {
            try
            {
                AppContext.SetSwitch("Switch.System.Net.DontEnableSystemDefaultTlsVersions", false);
            }
            catch (InvalidOperationException) { }
            catch (NotSupportedException) { }

            // |= Tls12 keeps any stronger protocol already selected, then drop
            // SSL 3.0. Leaving SSL 3.0 in the mask makes Schannel fail the
            // handshake on Windows 11 even when TLS 1.2 is also listed.
            try
            {
                SecurityProtocolType protocols = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls12;
                protocols &= ~SecurityProtocolType.Ssl3;
                ServicePointManager.SecurityProtocol = protocols;
            }
            catch (NotSupportedException)
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }

            // TLS 1.3 is SecurityProtocolType value 12288. The name is not on
            // every Framework reference assembly, and setting it throws when
            // the runtime cannot offer it.
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288;
            }
            catch (NotSupportedException) { }
        }
    }
}
