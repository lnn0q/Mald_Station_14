using Content.Server.Polymorph.Systems;
using Content.Server.Radiation.Components;
using Content.Shared._BRatbite.Radiation;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server._BRatbite.Radiation;

public sealed class BananiumLizardMutationSystem : EntitySystem
{
    private static readonly ProtoId<PolymorphPrototype> BananiumLizardMutation = "BananiumLizardMutation";
    private const string ReptilianSpecies = "Reptilian";
    private const float MutationThreshold = 0.5f;

    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadiationReceiverComponent, BananiumIrradiatedEvent>(OnBananiumIrradiated);
    }

    private void OnBananiumIrradiated(EntityUid uid, RadiationReceiverComponent component, BananiumIrradiatedEvent args)
    {
        if (args.RadsPerSecond < MutationThreshold)
            return;

        var target = uid;
        if (!TryComp(target, out HumanoidAppearanceComponent? humanoid) &&
            TryComp(target, out BodyPartComponent? bodyPart) &&
            bodyPart.Body != null)
        {
            target = bodyPart.Body.Value;
            TryComp(target, out humanoid);
        }

        if (humanoid == null || humanoid.Species != ReptilianSpecies)
            return;

        var lizard = _polymorph.PolymorphEntity(target, BananiumLizardMutation);
        if (lizard == null)
            return;

        EnsureComp<RadiationProtectionComponent>(lizard.Value);
    }
}
