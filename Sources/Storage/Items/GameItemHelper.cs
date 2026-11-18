namespace DigitalLogistics.Storage.Items
{
    public static class GameItemHelper
    {
        public static string GetModId(this GameItem item)
        {
            string modId = item.IsTag(ModHelper.MOD_ID_TAG)
                ? item.GetTagReadonly(ModHelper.MOD_ID_TAG).GetString()
                : null;

            if (string.IsNullOrEmpty(modId))
            {
                var factory = DirectoryMaster.GetFactory<GameItem>(item.identifier);
                modId = ModSourceHelper.FindModByDelegate(factory)?.Manifest.ID;
            }

            return modId;
        }
    }
}
