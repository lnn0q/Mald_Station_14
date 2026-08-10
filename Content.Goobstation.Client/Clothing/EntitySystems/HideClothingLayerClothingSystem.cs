// SPDX-FileCopyrightText: 2025 Aviu00 <aviu00@protonmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Client.Clothing.Components;
using Content.Goobstation.Common.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Client.Clothing.EntitySystems;

public sealed class HideClothingLayerClothingSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, CheckClothingSlotHiddenEvent>(OnCheck);

        SubscribeLocalEvent<HideClothingLayerClothingComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<HideClothingLayerClothingComponent, GotUnequippedEvent>(OnUnequip);
        SubscribeLocalEvent<HideClothingLayerClothingComponent, AfterAutoHandleStateEvent>(OnChameleonUpdated);
    }

    private void OnUnequip(Entity<HideClothingLayerClothingComponent> ent, ref GotUnequippedEvent args)
    {
        ResetInventory(args.Equipee, ent.Comp);
    }

    private void OnEquip(Entity<HideClothingLayerClothingComponent> ent, ref GotEquippedEvent args)
    {
        ResetInventory(args.Equipee, ent.Comp);
    }

    private void OnChameleonUpdated(Entity<HideClothingLayerClothingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<ChameleonClothingComponent>(ent.Owner, out var chameleon) &&
            chameleon.User is { } user)
        {
            ResetInventory(user);
        }
    }

    private void ResetInventory(EntityUid equipee, HideClothingLayerClothingComponent component)
    {
        foreach (var slot in component.HiddenSlots)
        {
            if (_inventory.TryGetSlotEntity(equipee, slot, out var uid))
                _item.VisualsChanged(uid.Value);
        }
    }

    private void ResetInventory(EntityUid equipee)
    {
        var enumerator = _inventory.GetSlotEnumerator(equipee, SlotFlags.WITHOUT_POCKET);
        while (enumerator.NextItem(out var item))
        {
            _item.VisualsChanged(item);
        }
    }

    private void OnCheck(Entity<InventoryComponent> ent, ref CheckClothingSlotHiddenEvent args)
    {
        var enumerator = _inventory.GetSlotEnumerator((ent.Owner, ent.Comp), SlotFlags.WITHOUT_POCKET);
        while (enumerator.NextItem(out var item))
        {
            if (!TryComp(item, out HideClothingLayerClothingComponent? hide))
                continue;

            if (!ShouldHideSlot(item, hide, args.Slot))
                continue;

            args.Visible = false;
            return;
        }
    }

    private bool ShouldHideSlot(EntityUid item, HideClothingLayerClothingComponent hide, string slot)
    {
        if (!TryComp(item, out ChameleonClothingComponent? chameleon) ||
            chameleon.Default == null)
            return hide.HiddenSlots.Contains(slot);

        return _prototype.TryIndex(chameleon.Default, out EntityPrototype? selected) &&
               selected.TryGetComponent("HideClothingLayerClothing", out HideClothingLayerClothingComponent? selectedHide) &&
               selectedHide.HiddenSlots.Contains(slot);
    }
}
