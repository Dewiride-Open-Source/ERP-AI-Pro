using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Application.Commands.ChooseOwnTheme;

internal sealed record ChooseOwnThemeCommand(ThemePreference Theme) : ICommand<ChosenTheme>;
