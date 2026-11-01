using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Domain;

public sealed class StoredErrorTests
{
    [Fact]
    public void From_ShortAsciiCode_KeepsTheCodeAndTheMessage()
    {
        var stored = StoredError.From(Error.Unavailable("directory.unavailable", "Microsoft Graph did not answer."));

        Assert.Equal(new StoredError("directory.unavailable", "Microsoft Graph did not answer."), stored);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("café")]
    [InlineData("tab\tcode")]
    public void From_CodeNotOfPrintableAscii_IsKeptAsTheUnnamedCode(string code)
    {
        Assert.Equal(StoredError.UnnamedCode, StoredError.From(new Error(code, "Failed.", ErrorKind.Failure)).Code);
    }

    [Fact]
    public void From_CodeLongerThanTheColumn_IsKeptAsTheUnnamedCode()
    {
        Assert.Equal(new string('c', JobRun.ErrorCodeMaxLength), StoredError.From(Error.Failure(new string('c', JobRun.ErrorCodeMaxLength), "Failed.")).Code);
        Assert.Equal(StoredError.UnnamedCode, StoredError.From(Error.Failure(new string('c', JobRun.ErrorCodeMaxLength + 1), "Failed.")).Code);
    }

    [Fact]
    public void From_MessageLongerThanTheColumn_IsCutToFit()
    {
        var stored = StoredError.From(Error.Failure("job.failed", new string('m', JobRun.ErrorMessageMaxLength + 40)));

        Assert.Equal(new string('m', JobRun.ErrorMessageMaxLength), stored.Message);
    }
}
