using FrooxEngine;
using System.Collections.Generic;

namespace Restonite
{
    public class DynVarSlot
    {
        public Slot Slot { get; }
        public IDynamicVariable DynamicVariable { get; }

        public DynVarSlot(Slot slot, IDynamicVariable dynamicVariable)
        {
            Slot = slot;
            DynamicVariable = dynamicVariable;
        }
    }
}
