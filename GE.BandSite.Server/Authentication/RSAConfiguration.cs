using System.Security.Cryptography;

namespace GE.BandSite.Server.Authentication;

public class RSAConfiguration
{
    public string D { get; set; } = null!;
    public string DP { get; set; } = null!;
    public string DQ { get; set; } = null!;
    public string Exponent { get; set; } = null!;
    public string InverseQ { get; set; } = null!;
    public string Modulus { get; set; } = null!;
    public string P { get; set; } = null!;
    public string Q { get; set; } = null!;

    public static RSAConfiguration FromParameters(RSAParameters parameters)
    {
        return new RSAConfiguration
        {
            D = Convert.ToBase64String(parameters.D ?? Array.Empty<byte>()),
            DP = Convert.ToBase64String(parameters.DP ?? Array.Empty<byte>()),
            DQ = Convert.ToBase64String(parameters.DQ ?? Array.Empty<byte>()),
            Exponent = Convert.ToBase64String(parameters.Exponent ?? Array.Empty<byte>()),
            InverseQ = Convert.ToBase64String(parameters.InverseQ ?? Array.Empty<byte>()),
            Modulus = Convert.ToBase64String(parameters.Modulus ?? Array.Empty<byte>()),
            P = Convert.ToBase64String(parameters.P ?? Array.Empty<byte>()),
            Q = Convert.ToBase64String(parameters.Q ?? Array.Empty<byte>())
        };
    }

    public RSAParameters ToParameters()
    {
        RSAParameters rsaParameters = new RSAParameters()
        {
            D = Convert.FromBase64String(D),
            DP = Convert.FromBase64String(DP),
            DQ = Convert.FromBase64String(DQ),
            Exponent = Convert.FromBase64String(Exponent),
            InverseQ = Convert.FromBase64String(InverseQ),
            Modulus = Convert.FromBase64String(Modulus),
            P = Convert.FromBase64String(P),
            Q = Convert.FromBase64String(Q)
        };

        return rsaParameters;
    }
}