using Robust.Shared.GameStates;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.DeviceLinking;

namespace Content.Shared._BRatbite.Machines;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BoltableMachineComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Bolted;

    [DataField]
    public ProtoId<SinkPortPrototype> OnPort = "On";

    [DataField]
    public ProtoId<SinkPortPrototype> OffPort = "Off";

    [DataField]
    public ProtoId<SinkPortPrototype> TogglePort = "Toggle";

    [DataField]
    public SoundSpecifier BoltSound = new SoundPathSpecifier("/Audio/Machines/boltsdown.ogg");

    [DataField]
    public SoundSpecifier UnboltSound = new SoundPathSpecifier("/Audio/Machines/boltsup.ogg");

    [DataField]
    public string? AnchorFailedMessage = "machine-popup-cant-anchor";
}
