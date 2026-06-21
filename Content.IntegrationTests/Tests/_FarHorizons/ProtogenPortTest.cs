// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Clothing.Components;
using Content.Server.Station.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Guidebook;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Tag;
using Content.Shared.Wagging;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Tests._FarHorizons;

[TestFixture]
public sealed class ProtogenPortTest
{
    private static readonly string[] RetainedSpecies =
    [
        "Protogen",
        "ProtoArachnid",
        "ProtoDiona",
        "ProtoDwarf",
        "ProtoFelionoid",
        "ProtoHuman",
        "ProtoKin",
        "ProtoMoth",
        "ProtoReptilian",
        "ProtoResomi",
        "ProtoVox",
        "ProtoVulp",
    ];

    private static readonly string[] RemovedSpecies =
    [
        "ProtoAvali",
        "ProtoCyclorite",
        "ProtoElf",
        "ProtoLagomorph",
        "ProtoSlimePerson",
        "ProtoThaven",
    ];

    private static readonly string[] ProtogenFrames =
    [
        "ClothingOuterArmorProtogenLightBasic",
        "ClothingOuterArmorProtogenLightSpeed",
        "ClothingOuterArmorProtogenMediumBasic",
        "ClothingOuterArmorProtogenMediumMagic",
        "ClothingOuterArmorProtogenHeavyBasic",
        "ClothingOuterArmorProtogenHeavySpace",
    ];

    private static readonly HashSet<string> BreathlessSpecies = ["ProtoKin"];

    [Test]
    public async Task RetainedSpeciesAreComplete()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var prototypes = client.ResolveDependency<IPrototypeManager>();
        var components = client.ResolveDependency<IComponentFactory>();

        await client.WaitAssertion(() =>
        {
            var speciesGuide = prototypes.Index<GuideEntryPrototype>("Species");
            Assert.That(speciesGuide.Children, Does.Contain(new ProtoId<GuideEntryPrototype>("Protogen")));

            var protogenGuide = prototypes.Index<GuideEntryPrototype>("Protogen");
            foreach (var id in RetainedSpecies)
            {
                Assert.That(prototypes.TryIndex<SpeciesPrototype>(id, out var species), Is.True, $"Missing retained species {id}");
                var playerPrototype = prototypes.Index(species.Prototype);
                Assert.That(playerPrototype.Abstract, Is.False, $"{id} uses abstract player prototype {species.Prototype}");
                Assert.DoesNotThrow(() => prototypes.Index(species.DollPrototype), $"{id} has no doll prototype");
                Assert.That(prototypes.HasIndex<HumanoidSpeciesBaseSpritesPrototype>(species.SpriteSet), Is.True, $"{id} has no sprite set");
                Assert.That(prototypes.HasIndex<RoleLoadoutPrototype>(species.Loadout), Is.True, $"{id} has no species loadout");

                if (id == "Protogen")
                    Assert.That(species.HasSubspecies, Is.True);
                else
                {
                    Assert.That(species.SubspeciesOf?.Id, Is.EqualTo("Protogen"));
                    Assert.That(protogenGuide.Children, Does.Contain(new ProtoId<GuideEntryPrototype>(id)),
                        $"Protogen guide is missing retained subspecies {id}");
                }

                Assert.That(playerPrototype.TryGetComponent(out SpeechComponent speech, components), Is.True,
                    $"{id} has no speech component");
                Assert.That(playerPrototype.TryGetComponent(out VocalComponent vocal, components), Is.True,
                    $"{id} has no vocal component");
                Assert.That(vocal!.Sounds, Is.Not.Null.And.Not.Empty, $"{id} has no vocal sound profiles");

                foreach (var emoteId in speech!.AllowedEmotes)
                {
                    var emote = prototypes.Index<EmotePrototype>(emoteId);
                    if (!emote.Category.HasFlag(EmoteCategory.Vocal))
                        continue;

                    foreach (var soundId in vocal.Sounds!.Values.Distinct())
                    {
                        var sounds = prototypes.Index<EmoteSoundsPrototype>(soundId);
                        Assert.That(sounds.FallbackSound != null || sounds.Sounds.ContainsKey(emoteId),
                            $"{id} sound profile {soundId} has no sound for vocal emote {emoteId}");
                    }
                }

                var points = prototypes.Index<MarkingPointsPrototype>(species.MarkingPoints);
                foreach (var markingPoint in points.Points.Values)
                {
                    foreach (var markingId in markingPoint.DefaultMarkings)
                    {
                        var marking = prototypes.Index(markingId);
                        Assert.That(marking.SpeciesRestrictions, Does.Contain(id),
                            $"{id} required marking {markingId} does not permit the species");
                    }
                }
            }

            var resomi = prototypes.Index<SpeciesPrototype>("ProtoResomi");
            var resomiSprites = prototypes.Index<HumanoidSpeciesBaseSpritesPrototype>(resomi.SpriteSet);
            var resomiPoints = prototypes.Index<MarkingPointsPrototype>(resomi.MarkingPoints);
            Assert.That(resomiSprites.Sprites, Does.ContainKey(HumanoidVisualLayers.TailExtras));
            Assert.That(resomiPoints.Points, Does.ContainKey(MarkingCategories.TailExtras));

            var protoVulp = prototypes.Index<EntityPrototype>("MobProtoVulp");
            var protoHuman = prototypes.Index<EntityPrototype>("MobProtoHuman");
            var protoReptilian = prototypes.Index<EntityPrototype>("MobProtoReptilian");
            Assert.That(protoVulp.TryGetComponent<WaggingComponent>(out _, components), Is.False);
            Assert.That(protoHuman.TryGetComponent<WaggingComponent>(out _, components), Is.False);
            Assert.That(protoReptilian.TryGetComponent<WaggingComponent>(out _, components), Is.True);

            foreach (var id in RemovedSpecies)
                Assert.That(prototypes.HasIndex<SpeciesPrototype>(id), Is.False, $"Removed species {id} is still loaded");

            foreach (var id in ProtogenFrames)
            {
                var frame = prototypes.Index<EntityPrototype>(id);
                Assert.That(frame.TryGetComponent<ClothingComponent>(out var clothing, components), Is.True,
                    $"{id} has no clothing component");
                Assert.That(clothing!.ClothingVisuals, Does.ContainKey("outerClothing2"),
                    $"{id} must explicitly suppress generated outerClothing2 visuals");
                Assert.That(clothing.ClothingVisuals["outerClothing2"], Is.Empty,
                    $"{id} must remain stats-only and not overlap the Protogen chassis");
                Assert.That(frame.TryGetComponent<TagComponent>(out var tags, components), Is.True,
                    $"{id} has no tags");
                Assert.That(tags!.Tags, Does.Contain(new ProtoId<TagPrototype>("SurgeryCompatibleArmor")),
                    $"{id} must not block surgery");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RetainedSpeciesCanSpawnAsPlayers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var stationSpawning = server.System<StationSpawningSystem>();
        var bodySystem = server.System<BodySystem>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            foreach (var id in RetainedSpecies)
            {
                var profile = new HumanoidCharacterProfile().WithSpecies(id);
                EntityUid entity = default;
                Assert.DoesNotThrow(() =>
                {
                    entity = stationSpawning.SpawnPlayerMob(
                        map.GridCoords,
                        job: "Passenger",
                        profile: profile,
                        station: null,
                        entity: null);
                }, $"Failed to spawn retained species {id}");

                Assert.That(bodySystem.GetBodyChildrenOfType(entity, BodyPartType.Chest).Count(), Is.EqualTo(1),
                    $"{id} must have exactly one chest");
                Assert.That(bodySystem.GetBodyChildrenOfType(entity, BodyPartType.Groin).Count(), Is.EqualTo(1),
                    $"{id} must have exactly one groin for Ratbite surgery");

                var organs = bodySystem.GetBodyOrgans(entity).ToArray();
                Assert.That(organs, Is.Not.Empty, $"{id} has no attached organs");
                foreach (var organ in organs)
                {
                    Assert.That(organ.Component.Body, Is.EqualTo(entity),
                        $"{id} organ {entityManager.ToPrettyString(organ.Id)} is not attached to its body");
                    Assert.That(organ.Component.SlotId, Is.Not.Empty,
                        $"{id} organ {entityManager.ToPrettyString(organ.Id)} has no surgery slot ID");
                }

                var lungs = bodySystem.GetBodyOrganEntityComps<LungComponent>((entity, null));
                if (BreathlessSpecies.Contains(id))
                {
                    Assert.That(lungs, Is.Empty, $"{id} is intentionally breathless but has lungs");
                    Assert.That(entityManager.HasComponent<RespiratorComponent>(entity), Is.False,
                        $"{id} is intentionally breathless but has a respirator");
                }
                else
                {
                    Assert.That(lungs, Is.Not.Empty, $"{id} has no attached functional lungs");
                    Assert.That(entityManager.HasComponent<RespiratorComponent>(entity), Is.True,
                        $"{id} has lungs but no respirator");
                }

                entityManager.DeleteEntity(entity);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RetainedEntityLayoutsHaveUniqueMapKeys()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var resources = server.ResolveDependency<IResourceManager>();
        var path = new ResPath("/Prototypes/_Starlight/Entities/Mobs/Species/Protogen/");

        await server.WaitAssertion(() =>
        {
            foreach (var file in resources.ContentFindFiles(path).Where(p => p.Extension == "yml"))
            {
                using var stream = resources.ContentFileRead(file);
                using var reader = new StreamReader(stream);
                var yaml = new YamlStream();
                yaml.Load(reader);

                foreach (var entity in yaml.Documents.SelectMany(d => ((YamlSequenceNode) d.RootNode).Cast<YamlMappingNode>()))
                {
                    if (!entity.TryGetNode<YamlSequenceNode>("components", out var components))
                        continue;

                    foreach (var sprite in components.Cast<YamlMappingNode>()
                                 .Where(c => c.GetNode("type").AsString() == "Sprite"))
                    {
                        if (!sprite.TryGetNode<YamlSequenceNode>("layers", out var layers))
                            continue;

                        var keys = layers.Cast<YamlMappingNode>()
                            .Where(l => l.TryGetNode<YamlSequenceNode>("map", out _))
                            .SelectMany(l => l.GetNode<YamlSequenceNode>("map").Cast<YamlScalarNode>())
                            .Select(k => k.Value!)
                            .ToList();

                        Assert.That(keys, Is.Unique,
                            $"{file} entity {entity.GetNode("id").AsString()} has duplicate sprite map keys");
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
