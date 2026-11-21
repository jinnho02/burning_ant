using System;
using System.Collections.Generic;
using System.Linq;

namespace BurningAnt.Items
{
    /// <summary>플레이어가 상점에서 사서 가지고 있는 아이템과 개수.</summary>
    public class Inventory
    {
        readonly Dictionary<ItemDefinition, int> counts = new();

        public event Action Changed;

        public int Count(ItemDefinition item) => counts.TryGetValue(item, out int n) ? n : 0;

        public IEnumerable<(ItemDefinition item, int count)> Owned =>
            counts.Where(kv => kv.Value > 0).Select(kv => (kv.Key, kv.Value));

        public void Add(ItemDefinition item, int amount = 1)
        {
            counts[item] = Count(item) + amount;
            Changed?.Invoke();
        }

        public bool Consume(ItemDefinition item)
        {
            int n = Count(item);
            if (n < 1) return false;
            counts[item] = n - 1;
            Changed?.Invoke();
            return true;
        }
    }
}
