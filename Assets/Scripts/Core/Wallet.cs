using System;

namespace SlotGame.Core
{
    /// <summary>Holds the player's credits. The only place the balance can change.</summary>
    public sealed class Wallet
    {
        public int Balance { get; private set; }

        public event Action<int> BalanceChanged;

        public Wallet(int startingBalance)
        {
            Balance = Math.Max(0, startingBalance);
        }

        public bool CanAfford(int amount)
        {
            return amount >= 0 && Balance >= amount;
        }

        /// <summary>Removes credits if the player can afford it. Returns false and changes nothing otherwise.</summary>
        public bool TryDebit(int amount)
        {
            if (!CanAfford(amount))
            {
                return false;
            }

            Balance -= amount;
            BalanceChanged?.Invoke(Balance);
            return true;
        }

        public void Credit(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Balance += amount;
            BalanceChanged?.Invoke(Balance);
        }
    }
}
