using Content.Client.Lobby.UI.Loadouts;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences.Loadouts;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void UpdateSpeciesLoadout()
    {
        CSpeciesLoadout.Visible = false;
        SpeciesLoadout.OnPressed -= SpeciesLoadoutPressed;

        if (Profile == null ||
            !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species) ||
            species.Loadout == null ||
            !_prototypeManager.HasIndex<RoleLoadoutPrototype>(species.Loadout.Value))
        {
            return;
        }

        CSpeciesLoadout.Visible = true;
        SpeciesLoadout.OnPressed += SpeciesLoadoutPressed;
    }

    private void SpeciesLoadoutPressed(BaseButton.ButtonEventArgs args)
    {
        if (Profile == null ||
            !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species) ||
            species.Loadout == null ||
            !_prototypeManager.TryIndex<RoleLoadoutPrototype>(species.Loadout.Value, out var loadoutPrototype))
        {
            return;
        }

        var loadout = Profile.GetSpeciesLoadoutOrDefault(_playerManager.LocalSession, _prototypeManager)?.Clone();
        if (loadout == null)
            return;

        OpenSpeciesLoadout(species, loadout, loadoutPrototype);
    }

    private void OpenSpeciesLoadout(
        SpeciesPrototype species,
        RoleLoadout speciesLoadout,
        RoleLoadoutPrototype speciesLoadoutPrototype)
    {
        _loadoutWindow?.Dispose();
        _loadoutWindow = null;

        if (_playerManager.LocalSession == null || Profile == null)
            return;

        var session = _playerManager.LocalSession;
        var dependencies = IoCManager.Instance;
        if (dependencies == null)
            return;

        _loadoutWindow = new LoadoutWindow(Profile, speciesLoadout, speciesLoadoutPrototype, session, dependencies)
        {
            Title = Loc.GetString("loadout-window-title-loadout", ("job", Loc.GetString(species.Name))),
        };

        _loadoutWindow.RefreshLoadouts(speciesLoadout, session, dependencies);
        _loadoutWindow.OpenCenteredLeft();

        _loadoutWindow.OnLoadoutPressed += (group, loadout) =>
        {
            speciesLoadout.AddLoadout(group, loadout, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(speciesLoadout, session, dependencies);
            Profile = Profile.WithSpeciesLoadout(speciesLoadout);
            ReloadPreview();
        };

        _loadoutWindow.OnLoadoutUnpressed += (group, loadout) =>
        {
            speciesLoadout.RemoveLoadout(group, loadout, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(speciesLoadout, session, dependencies);
            Profile = Profile.WithSpeciesLoadout(speciesLoadout);
            ReloadPreview();
        };

        ReloadPreview();

        _loadoutWindow.OnClose += () =>
        {
            JobOverride = null;
            ReloadPreview();
        };
    }
}
