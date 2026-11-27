using UnityEngine;

namespace IdlePet.Tests.EditMode.Authoring
{
    public sealed class ProfileReferenceAsset : ScriptableObject
    {
        [SerializeField] private Object _reference;

        public Object Reference => _reference;
    }
}
