using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure;

public sealed class DependencyInjectionPrivateTests
{
    private static byte[] InvokeDecodeJwtSecret(string secret, string? encoding)
    {
        var m = typeof(DependencyInjection).GetMethod("DecodeJwtSecret", BindingFlags.NonPublic | BindingFlags.Static);
        m.Should().NotBeNull();
        return (byte[])m!.Invoke(null, new object?[] { secret, encoding })!;
    }

    [Fact]
    public void DecodeJwtSecret_Raw_ReturnsUtf8Bytes()
    {
        var bytes = InvokeDecodeJwtSecret("abc", "raw");
        bytes.Should().Equal(System.Text.Encoding.UTF8.GetBytes("abc"));
    }

    [Fact]
    public void DecodeJwtSecret_Hex_Decodes()
    {
        var bytes = InvokeDecodeJwtSecret("616263", "hex"); // "abc"
        bytes.Should().Equal(System.Text.Encoding.UTF8.GetBytes("abc"));
    }

    [Fact]
    public void DecodeJwtSecret_Base64_Invalid_FallsBackToRaw()
    {
        var bytes = InvokeDecodeJwtSecret("not-base64", "base64");
        bytes.Should().Equal(System.Text.Encoding.UTF8.GetBytes("not-base64"));
    }
}

