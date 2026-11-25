using SlotGame.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>One paytable entry: three symbol icons and the payout multiplier.</summary>
    public sealed class PaytableRowView : MonoBehaviour
    {
        [SerializeField] private Image[] icons;
        [SerializeField] private Text payoutText;

        public void Bind(SymbolData symbol)
        {
            foreach (Image icon in icons)
            {
                icon.sprite = symbol.Sprite;
                icon.preserveAspect = true;
            }

            payoutText.text = symbol.AwardsJackpot
                ? $"x{symbol.PayoutMultiplier} + JACKPOT"
                : $"x{symbol.PayoutMultiplier}";
        }
    }
}
