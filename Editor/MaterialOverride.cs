using UnityEngine;

namespace net.koodaa.TreeItImporter.Editor
{
    [System.Serializable]
    internal class MaterialOverride
    {
        [SerializeField] internal string name;
        [SerializeField] internal Material material;
    }
}
