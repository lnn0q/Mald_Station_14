using Robust.Shared.Configuration;

namespace Content.Shared._BRatbite.CCVar;

[CVarDefs]
public sealed partial class RatbiteCVars
{
    public static readonly CVarDef<float> PointerScale =
        CVarDef.Create("accessibility.pointer_scale", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> PointerOutline =
        CVarDef.Create("accessibility.pointer_outline", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
