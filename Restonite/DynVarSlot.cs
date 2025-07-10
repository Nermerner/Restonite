using FrooxEngine;

namespace Restonite;

internal class DynVarSlot
{
    #region Public Constructors

    public DynVarSlot(Slot slot, IDynamicVariable dynamicVariable)
    {
        Slot = slot;
        DynamicVariable = dynamicVariable;
    }

    #endregion

    #region Public Properties

    public IDynamicVariable DynamicVariable { get; }
    public Slot Slot { get; }

    #endregion
}
