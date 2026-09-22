using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Idempotency.Http;

public sealed class IdempotencyKeyHeaderTests
{
    private const string Uuid = "8e03978e-40d5-43e8-bc93-6894a57f9324";

    [Theory]
    [InlineData(Uuid)]
    [InlineData("\"" + Uuid + "\"")]
    [InlineData("  " + Uuid + "  ")]
    [InlineData("8E03978E-40D5-43E8-BC93-6894A57F9324")]
    public void Parse_BareOrQuotedUuid_IsValid(string value)
    {
        var result = IdempotencyKeyHeader.Parse(new HeaderDictionary { [IdempotencyKeyHeader.Name] = value });

        Assert.Equal(KeyParseState.Valid, result.State);
        Assert.Equal(new Guid(Uuid), result.Key);
    }

    [Fact]
    public void Parse_NoHeader_IsMissing()
    {
        var result = IdempotencyKeyHeader.Parse(new HeaderDictionary());

        Assert.Equal(KeyParseResult.Missing, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("8e03978e40d543e8bc936894a57f9324")]
    [InlineData("{8e03978e-40d5-43e8-bc93-6894a57f9324}")]
    [InlineData("\"8e03978e-40d5-43e8-bc93-6894a57f9324")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("\"" + Uuid + "\";v=1")]
    public void Parse_MalformedValue_IsInvalid(string value)
    {
        var result = IdempotencyKeyHeader.Parse(new HeaderDictionary { [IdempotencyKeyHeader.Name] = value });

        Assert.Equal(KeyParseResult.Invalid, result);
    }

    [Fact]
    public void Parse_TwoValues_IsInvalid()
    {
        var result = IdempotencyKeyHeader.Parse(new HeaderDictionary { [IdempotencyKeyHeader.Name] = new[] { Uuid, Uuid } });

        Assert.Equal(KeyParseResult.Invalid, result);
    }
}
