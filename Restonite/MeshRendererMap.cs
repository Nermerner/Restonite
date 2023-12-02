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
        public MeshRenderer NormalMeshRenderer {  get; set; }
        public MeshRenderer StatueMeshRenderer { get; set; }
        public List<MaterialMap> Materials { get; set; } = new List<MaterialMap>();
    }
}
