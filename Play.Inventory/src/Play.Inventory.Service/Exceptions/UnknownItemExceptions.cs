using System;

namespace Play.Inventory.Service.Exceptions
{

    [Serializable]
    internal class UnknownItemException : Exception
    {


        public UnknownItemException(Guid itemId) : base($"Unknown Item: '{itemId}'")
        {
            this.ItemId = itemId;
        }

        public Guid ItemId { get; }
    }
}