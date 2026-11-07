namespace Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

// A GST State code can stand for different territories over time, so every answer is for a date; the list without one
// holds every row with the dates it applies to.
public interface IIndianStates
{
    Task<IReadOnlyList<IndianStateEntry>> ListAsync(DateOnly? on, CancellationToken cancellationToken);

    Task<IndianStateEntry?> FindAsync(string gstCode, DateOnly on, CancellationToken cancellationToken);
}
