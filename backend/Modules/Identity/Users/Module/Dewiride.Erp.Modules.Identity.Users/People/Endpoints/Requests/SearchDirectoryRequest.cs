using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Requests;

public sealed record SearchDirectoryRequest(
    [property: FromQuery(Name = "search")]
    [property: Description("Text to look for at the start of a word of a person's display name, or at the start of their email address or user principal name: at most 100 characters, at least 2 of them besides spaces at either end, without & or control characters.")]
    [property: Required]
    [property: StringLength(DirectorySearch.MaxTextLength)]
    string? Search) : IValidatableObject
{
    public const string TextMessage = "The search text has at least 2 characters besides spaces at either end, and no & or control characters.";

    // Microsoft Graph refuses an ampersand in $search even when it is encoded.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Search is { } search && (search.Trim().Length < DirectorySearch.MinTextLength || search.Contains('&', StringComparison.Ordinal) || search.Any(char.IsControl)))
        {
            yield return new ValidationResult(TextMessage, [nameof(Search)]);
        }
    }
}
