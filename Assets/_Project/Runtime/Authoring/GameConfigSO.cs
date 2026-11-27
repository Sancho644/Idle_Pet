using UnityEngine;

namespace IdlePet.Authoring
{
    [CreateAssetMenu(menuName = "Idle Pet/Configuration/Game")]
    public sealed class GameConfigSO : ScriptableObject
    {
        [SerializeField] private BalanceProfileSO _profile;

        [Header("Configuration revisions")]
        [Tooltip("Rules version in major.minor or major.minor.patch format.")]
        [SerializeField] private string _rulesVersion;

        [Tooltip("Content version in major.minor or major.minor.patch format.")]
        [SerializeField] private string _contentVersion;

        public BalanceProfileSO Profile => _profile;

        public string RulesVersion => _rulesVersion;

        public string ContentVersion => _contentVersion;
    }
}
