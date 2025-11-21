using System;

[Serializable]
public class InventorySlot
{
    public ItemDataSO item;
    public int count;

    public InventorySlot(ItemDataSO item, int count)
    {
        this.item = item;
        this.count = count;
    }
}