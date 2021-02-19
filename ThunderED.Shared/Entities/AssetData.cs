namespace ThunderED
{
    public class AssetData
    {
        public bool is_blueprint_copy;

        public bool is_singleton;

        public long item_id;

        public AssetLocationFlag location_flag;

        public long location_id;

        public AssetLocationType location_type;

        public int quantity;

        public long? type_id;
    }

    public enum AssetLocationType
    {
        station, solar_system, item, other
    }

    public enum AssetLocationFlag
    {
        AssetSafety, AutoFit, BoosterBay, Cargo, CorpseBay, Deliveries, DroneBay, FighterBay, FighterTube0, FighterTube1, FighterTube2, FighterTube3, FighterTube4, FleetHangar, FrigateEscapeBay, Hangar, HangarAll, HiSlot0, HiSlot1, HiSlot2, HiSlot3, HiSlot4, HiSlot5, HiSlot6, HiSlot7, HiddenModifiers, Implant, LoSlot0, LoSlot1, LoSlot2, LoSlot3, LoSlot4, LoSlot5, LoSlot6, LoSlot7, Locked, MedSlot0, MedSlot1, MedSlot2, MedSlot3, MedSlot4, MedSlot5, MedSlot6, MedSlot7, QuafeBay, RigSlot0, RigSlot1, RigSlot2, RigSlot3, RigSlot4, RigSlot5, RigSlot6, RigSlot7, ShipHangar, Skill, SpecializedAmmoHold, SpecializedCommandCenterHold, SpecializedFuelBay, SpecializedGasHold, SpecializedIndustrialShipHold, SpecializedLargeShipHold, SpecializedMaterialBay, SpecializedMediumShipHold, SpecializedMineralHold, SpecializedOreHold, SpecializedPlanetaryCommoditiesHold, SpecializedSalvageHold, SpecializedShipHold, SpecializedSmallShipHold, SubSystemBay, SubSystemSlot0, SubSystemSlot1, SubSystemSlot2, SubSystemSlot3, SubSystemSlot4, SubSystemSlot5, SubSystemSlot6, SubSystemSlot7, Unlocked, Wardrobe
    }

    /*public class AssetVisualDataBase
    {
        public string ItemTypeName { get; set; }
        public bool IsBlueprintCopy { get; set; }
        public bool IsSingleton { get; set; }
        public AssetLocationFlag LocationFlag { get; set; }
        public long LocationId { get; set; }
        public AssetLocationType LocationType { get; set; }
        public int Quantity { get; set; }
    }*/

    public class WebAssetData
    {
        public long ItemTypeId { get; set; }
        public string ItemTypeName { get; set; }
        public bool IsBlueprintCopy { get; set; }
        public int Quantity { get; set; }
        public string LocationName { get; set; }
        public long LocationId { get; set; }
    }

    public class WebAssetCategory
    {
        public string Name { get; set; }
    }
}
