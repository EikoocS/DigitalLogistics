namespace DigitalLogistics.Item
{
    internal static class ItemMovementProtection
    {
        internal static bool ContainsEnabledDevice(GameItem item) =>
            IsEnabledDevice(item) || GraphUtils.FindChildType<GameItem>(item, IsEnabledDevice, _ => true) != null;

        private static bool IsEnabledDevice(GameItem item) => item is Hopper { Enabled: true } || Bus.IsEnabledBus(item);
    }
}
