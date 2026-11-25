using UnityEngine;

namespace SlotGame.Data
{
    /// <summary>
    /// Designer-facing definition of one reel symbol.
    /// Kept as a ScriptableObject so weights and payouts can be tuned in the Inspector
    /// without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "Symbol", menuName = "Slot Game/Symbol")]
    public sealed class SymbolData : ScriptableObject
    {
        [Tooltip("Stable identifier used in logs and tests.")]
        [SerializeField] private string symbolId = "symbol";

        [Tooltip("Name shown to the player, e.g. in win messages.")]
        [SerializeField] private string displayName = "Symbol";

        [SerializeField] private Sprite sprite;

        [Tooltip("Relative chance of this symbol landing on the payline. Higher = more common.")]
        [SerializeField, Min(0)] private int weight = 1;

        [Tooltip("Payout for three of this symbol on the payline, as a multiple of the bet.")]
        [SerializeField, Min(0)] private int payoutMultiplier = 5;

        [Tooltip("Three of this symbol also awards the progressive jackpot.")]
        [SerializeField] private bool awardsJackpot;

        public string SymbolId => symbolId;
        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
        public int Weight => weight;
        public int PayoutMultiplier => payoutMultiplier;
        public bool AwardsJackpot => awardsJackpot;

        /// <summary>
        /// Creates a symbol in code. Used by unit tests and the editor scene builder;
        /// in the game, symbols are loaded from assets.
        /// </summary>
        public static SymbolData Create(string id, string name, int weight, int payoutMultiplier,
            bool awardsJackpot = false, Sprite sprite = null)
        {
            var symbol = CreateInstance<SymbolData>();
            symbol.Configure(id, name, weight, payoutMultiplier, awardsJackpot, sprite);
            return symbol;
        }

        /// <summary>Overwrites all fields. Used by <see cref="Create"/> and by the editor builder.</summary>
        public void Configure(string id, string name, int newWeight, int newPayoutMultiplier,
            bool newAwardsJackpot, Sprite newSprite)
        {
            symbolId = id;
            displayName = name;
            weight = Mathf.Max(0, newWeight);
            payoutMultiplier = Mathf.Max(0, newPayoutMultiplier);
            awardsJackpot = newAwardsJackpot;
            sprite = newSprite;
            this.name = id;
        }
    }
}
