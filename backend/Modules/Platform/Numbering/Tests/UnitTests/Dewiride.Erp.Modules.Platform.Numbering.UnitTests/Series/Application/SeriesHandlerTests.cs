using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Commands.ChangeNumberSeries;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Commands.SetUpNumberSeries;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Queries.GetNumberSeries;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Queries.ListNumberedDocuments;

namespace Dewiride.Erp.Modules.Platform.Numbering.UnitTests.Series.Application;

public sealed class SeriesHandlerTests
{
    private static readonly NumberFormat Format = new("INV/", NumberYearStyle.Full, "/", 4);

    [Fact]
    public async Task List_Always_AnswersTheAdministrationsDocuments()
    {
        var administration = new RecordingNumberingAdministration();

        var result = await new ListNumberedDocumentsHandler(administration).HandleAsync(new ListNumberedDocumentsQuery(), TestContext.Current.CancellationToken);

        Assert.Equal([RecordingNumberingAdministration.Answer], result.Value);
        Assert.Equal(["ListAsync"], administration.Calls);
    }

    [Fact]
    public async Task Get_ANumbering_AsksForItsId()
    {
        var administration = new RecordingNumberingAdministration();
        var id = NumberSeriesId.Create();

        var result = await new GetNumberSeriesHandler(administration).HandleAsync(new GetNumberSeriesQuery(id), TestContext.Current.CancellationToken);

        Assert.Same(RecordingNumberingAdministration.Answer, result.Value);
        Assert.Equal(id, administration.Id);
    }

    [Fact]
    public async Task SetUp_ACommand_PassesItsDocumentFormatAndNextNumber()
    {
        var administration = new RecordingNumberingAdministration();

        var result = await new SetUpNumberSeriesHandler(administration).HandleAsync(new SetUpNumberSeriesCommand("test.numbering.tax-invoices", Format, 38), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(("test.numbering.tax-invoices", Format, 38L), (administration.DocumentType, administration.Format, administration.NextNumber));
    }

    [Fact]
    public async Task Change_AVersionTheApiGave_PassesItsBytesWithTheChange()
    {
        var administration = new RecordingNumberingAdministration();
        var id = NumberSeriesId.Create();
        byte[] version = [0, 0, 0, 0, 0, 0, 7, 209];

        var result = await new ChangeNumberSeriesHandler(administration).HandleAsync(new ChangeNumberSeriesCommand(id, Format, null, Convert.ToBase64String(version)), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal((id, Format, (long?)null), (administration.Id, administration.Format, administration.NextNumber));
        Assert.Equal(version, administration.Version);
    }

    [Theory]
    [InlineData("not base64")]
    [InlineData("AAAA")]
    public async Task Change_AVersionTheApiNeverGave_IsRefusedWithoutAChange(string version)
    {
        var administration = new RecordingNumberingAdministration();

        var result = await new ChangeNumberSeriesHandler(administration).HandleAsync(new ChangeNumberSeriesCommand(NumberSeriesId.Create(), Format, 5, version), TestContext.Current.CancellationToken);

        Assert.Equal("numbering.version-invalid", result.Error!.Code);
        Assert.Contains(NumberingErrors.VersionField, result.Error.Fields.Keys);
        Assert.Empty(administration.Calls);
    }
}
