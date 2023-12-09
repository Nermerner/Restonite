using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Restonite
{
    internal class MeshRendererMap
    {
        public Slot NormalSlot { get; set; }
        public Slot StatueSlot { get; set; }
        public MeshRenderer NormalMeshRenderer {  get; set; }
        public MeshRenderer StatueMeshRenderer { get; set; }
        public MaterialSet NormalMaterialSet { get; set; }
        public MaterialSet StatueMaterialSet { get; set; }
        public List<List<MaterialMap>> MaterialSets { get; set; } = new List<List<MaterialMap>>();
    }
}
