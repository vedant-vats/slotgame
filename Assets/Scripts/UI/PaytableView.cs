using System.Collections.Generic;
using System.Linq;
using SlotGame.Data;
using UnityEngine;

namespace SlotGame.UI
{
    /// <summary>
    /// Builds the on-screen paytable from the SlotConfig at runtime, so the table can never
    /// get out of sync with the real payout values.
    /// </summary>
    public sealed class PaytableView : MonoBehaviour
    {
        [SerializeField] private SlotConfig config;
        [SerializeField] private PaytableRowView rowPrefab;
        [SerializeField] private Transform rowContainer;

        private void Start()
        {
            // Highest-paying symbols first.
            IEnumerable<SymbolData> ordered = config.Symbols.OrderByDescending(symbol => symbol.PayoutMultiplier);

            foreach (SymbolData symbol in ordered)
            {
                PaytableRowView row = Instantiate(rowPrefab, rowContainer);
                row.Bind(symbol);
            }
        }
    }
}
