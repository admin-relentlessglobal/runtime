// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Internal.Cryptography;

namespace System.Security.Cryptography.X509Certificates
{
    /// <summary>
    /// A singleton class that encapsulates the native implementation of various X509 services. (Implementing this as a singleton makes it
    /// easier to split the class into abstract and implementation classes if desired.)
    /// </summary>
    internal sealed partial class X509Pal : IX509Pal
    {
        public bool SupportsLegacyBasicConstraintsExtension
        {
            get { return true; }
        }

        public byte[] EncodeX509BasicConstraints2Extension(bool certificateAuthority, bool hasPathLengthConstraint, int pathLengthConstraint)
        {
            unsafe
            {
                CERT_BASIC_CONSTRAINTS2_INFO constraintsInfo = new CERT_BASIC_CONSTRAINTS2_INFO()
                {
                    fCA = certificateAuthority ? 1 : 0,
                    fPathLenConstraint = hasPathLengthConstraint ? 1 : 0,
                    dwPathLenConstraint = pathLengthConstraint,
                };

                return Interop.crypt32.EncodeObject(Oids.BasicConstraints2, &constraintsInfo);
            }
        }

        public void DecodeX509BasicConstraintsExtension(byte[] encoded, out bool certificateAuthority, out bool hasPathLengthConstraint, out int pathLengthConstraint)
        {
            unsafe
            {
                (certificateAuthority, hasPathLengthConstraint, pathLengthConstraint) = encoded.DecodeObject(
                    CryptDecodeObjectStructType.X509_BASIC_CONSTRAINTS,
                    static delegate (void* pvDecoded, int cbDecoded)
                    {
                        Debug.Assert(cbDecoded >= sizeof(CERT_BASIC_CONSTRAINTS_INFO));
                        CERT_BASIC_CONSTRAINTS_INFO* pBasicConstraints = (CERT_BASIC_CONSTRAINTS_INFO*)pvDecoded;
                        return ((Marshal.ReadByte(pBasicConstraints->SubjectType.pbData) & CERT_BASIC_CONSTRAINTS_INFO.CERT_CA_SUBJECT_FLAG) != 0,
                                pBasicConstraints->fPathLenConstraint != 0,
                                pBasicConstraints->dwPathLenConstraint);
                    });
            }
        }

        public void DecodeX509BasicConstraints2Extension(byte[] encoded, out bool certificateAuthority, out bool hasPathLengthConstraint, out int pathLengthConstraint)
        {
            unsafe
            {
                (certificateAuthority, hasPathLengthConstraint, pathLengthConstraint) = encoded.DecodeObject(
                    CryptDecodeObjectStructType.X509_BASIC_CONSTRAINTS2,
                    static delegate (void* pvDecoded, int cbDecoded)
                    {
                        Debug.Assert(cbDecoded >= sizeof(CERT_BASIC_CONSTRAINTS2_INFO));
                        CERT_BASIC_CONSTRAINTS2_INFO* pBasicConstraints2 = (CERT_BASIC_CONSTRAINTS2_INFO*)pvDecoded;
                        return (pBasicConstraints2->fCA != 0,
                                pBasicConstraints2->fPathLenConstraint != 0,
                                pBasicConstraints2->dwPathLenConstraint);
                    });
            }
        }

        public byte[] EncodeX509EnhancedKeyUsageExtension(OidCollection usages)
        {
            int numUsages;
            using (SafeHandle usagesSafeHandle = usages.ToLpstrArray(out numUsages))
            {
                unsafe
                {
                    CERT_ENHKEY_USAGE enhKeyUsage = new CERT_ENHKEY_USAGE()
                    {
                        cUsageIdentifier = numUsages,
                        rgpszUsageIdentifier = (IntPtr*)(usagesSafeHandle.DangerousGetHandle()),
                    };

                    return Interop.crypt32.EncodeObject(Oids.EnhancedKeyUsage, &enhKeyUsage);
                }
            }
        }

        public void DecodeX509EnhancedKeyUsageExtension(byte[] encoded, out OidCollection usages)
        {
            unsafe
            {
                usages = encoded.DecodeObject(
                    CryptDecodeObjectStructType.X509_ENHANCED_KEY_USAGE,
                    static delegate (void* pvDecoded, int cbDecoded)
                    {

                        Debug.Assert(cbDecoded >= sizeof(CERT_ENHKEY_USAGE));
                        CERT_ENHKEY_USAGE* pEnhKeyUsage = (CERT_ENHKEY_USAGE*)pvDecoded;
                        int count = pEnhKeyUsage->cUsageIdentifier;
                        var localUsages = new OidCollection(count);
                        for (int i = 0; i < count; i++)
                        {
                            IntPtr oidValuePointer = pEnhKeyUsage->rgpszUsageIdentifier[i];
                            string oidValue = Marshal.PtrToStringAnsi(oidValuePointer)!;
                            Oid oid = new Oid(oidValue);
                            localUsages.Add(oid);
                        }

                        return localUsages;
                    });
            }
        }
    }
}
