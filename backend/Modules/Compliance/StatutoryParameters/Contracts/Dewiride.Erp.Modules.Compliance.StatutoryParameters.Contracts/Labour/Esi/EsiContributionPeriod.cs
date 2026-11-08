namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

// Each day and month falls at its next occurrence: a contribution period runs from StartsOn to the next EndsOn, and its benefit
// period from the next BenefitStartsOn after the contribution period ends to the next BenefitEndsOn. For a person who becomes an
// employee for the first time the first contribution period starts on the day of employment, and the first benefit period
// BenefitAfterMonths months after it.
public sealed record EsiContributionPeriod(
    string Period,
    string Name,
    DayAndMonth? StartsOn,
    DayAndMonth? EndsOn,
    DayAndMonth? BenefitStartsOn,
    DayAndMonth? BenefitEndsOn,
    int? BenefitAfterMonths,
    string Wording);
