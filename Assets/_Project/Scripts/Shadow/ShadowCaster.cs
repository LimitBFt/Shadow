using UnityEngine;

namespace ShadowPath.Shadow
{
    [RequireComponent(typeof(MeshFilter))]
    public class ShadowCaster : MonoBehaviour
    {
        private MeshFilter meshFilter;

        public MeshFilter MeshFilter
        {
            get
            {
                CacheComponents();
                return meshFilter;
            }
        }

        public bool IsValid
        {
            get
            {
                CacheComponents();
                return meshFilter != null
                    && meshFilter.sharedMesh != null
                    && meshFilter.sharedMesh.isReadable;
            }
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void CacheComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }
        }
    }
}
