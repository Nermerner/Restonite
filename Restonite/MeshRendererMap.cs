using FrooxEngine;
using System.Collections.Generic;

namespace Restonite
{
    internal class MeshRendererMap
    {
        #region Public Properties

        public List<List<MaterialMap>> MaterialSets { get; set; } = new List<List<MaterialMap>>();
        public MaterialSet? NormalMaterialSet { get; set; }
        public MeshRenderer? NormalMeshRenderer { get; set; }
        public Slot? NormalSlot { get; set; }
        public MaterialSet? StatueMaterialSet { get; set; }
        public MeshRenderer? StatueMeshRenderer { get; set; }
        public Slot? StatueSlot { get; set; }

        #endregion
    }
}
