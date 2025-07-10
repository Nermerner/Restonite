using FrooxEngine;

namespace Restonite;

internal class MaterialMap
{
    public bool Clothes { get; set; }
    public IAssetProvider<Material>? Normal { get; set; }
    public IAssetProvider<Material>? Statue { get; set; }
    public StatueType TransitionType { get; set; }
    public bool UseAsIs { get; set; }
}
