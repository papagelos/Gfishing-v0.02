// Assets/Scripts/Progress/RecordHolderNames.cs
//
// Provides fake "world record holder" names for each species.
// Deterministic per species id, so the same fish always shows the same
// holders, but the data itself is just flavour.
//
// Mapping rule (simple and dynamic):
//   index = speciesId % Names.Length
// So fish id 0 -> Names[0], id 1 -> Names[1], ...,
// id 200 -> Names[200 % Names.Length], etc.
//
// If you want to use your own list, just replace the Names[] contents.

namespace GalacticFishing.Progress
{
    public static class RecordHolderNames
    {
        // *** EDITABLE DATA ***
        // Replace this array with whatever names you want.
        private static readonly string[] Names = new[]
        {
            "Aldren",
            "NightMara",
            "Zyrion",
            "MiaLinn",
            "RustyKnuckles",
            "TehBaker",
            "Nyxie",
            "GrimTuesday",
            "PixelPunk",
            "Hearthling",
            "DrunkMonk",
            "YumiChan",
            "OldManRiver",
            "TiredPal",
            "LoneSentry",
            "VikingTony",
            "KappaTown",
            "DustyLaptop",
            "SeraphineX",
            "LaggyJoe",
            "Kiroshi",
            "MarbleEyes",
            "QuickSven",
            "RiotKit",
            "FortyWinks",
            "AriaNorth",
            "BackseatTank",
            "ChillFury",
            "DeskSamurai",
            "EllaNova",
            "Gorvak",
            "HarborChild",
            "IdleWizard",
            "JazzHands",
            "KuroNeko",
            "LittleLoki",
            "MapleSoda",
            "NoraByte",
            "OdinJunior",
            "Patchless",
            "QuietSora",
            "RagnaRookie",
            "SilkRoad",
            "TaxiHeals",
            "UmbraFox",
            "VeraCloud",
            "WeekendWar",
            "XenoKid",
            "YellowPing",
            "ZeroMousse",
            "BudgetBarb",
            "CouchRogue",
            "DadOnWifi",
            "EchoLane",
            "FridaSun",
            "GigaGlen",
            "HanaLeaf",
            "InkDragon",
            "JarOfButtons",
            "KebabKnight",
            "LanaOrbit",
            "MellowJay",
            "NoisyNils",
            "Otterstein",
            "PanicNurse",
            "QuietQuokka",
            "RamenDad",
            "SoftCrusader",
            "Tenuki",
            "UmiRider",
            "VoidTicket",
            "WornJoystick",
            "XrayMage",
            "YoloArchitect",
            "ZoomerTank",
            "ArcLight",
            "BoringHero",
            "ColdToast",
            "DogecoinDan",
            "EmptyMug",
            "FikaMaster",
            "GreyLynx",
            "HotKeyHilda",
            "IceCabbage",
            "JoJoSleeper",
            "KiteBreaker",
            "LateMage",
            "MetroMole",
            "NineLivesLeo",
            "OffMetaOskar",
            "PentaPedro",
            "QuietHarpy",
            "RustBucket",
            "SaneDruid",
            "TiltProof",
            "UrbanMoose",
            "VeggieRex",
            "WifiWizard",
            "XenoKarin",
            "YardGnome",
            "ZenMechanic",
            "AddictedAnna",
            "BlueSax",
            "ChocoFang",
            "DerpyBard",
            "EmptyLobby",
            "FlashStep",
            "Gloomberry",
            "HazySimon",
            "IdleIbrahim",
            "JadedJules",
            "KafeKarla",
            "LofiLars",
            "MidnightMads",
            "Narvi",
            "OpenTab",
            "PillowMike",
            "QwertyQueen",
            "RustyNora",
            "SodaKnight",
            "TinyGiant",
            "UnpaidIntern",
            "VividRune",
            "WaffleMage",
            "XenoVictor",
            "YuzuTea",
            "ZeroPolish",
            "AdamantEve",
            "BronzeBjorn",
            "CluelessCarl",
            "D20Dealer",
            "EuroTrashPunk",
            "FjordRider",
            "GamerGrandma",
            "HighPingHarry",
            "IdleIris",
            "JustJanne",
            "KoreanBBQ",
            "LuckyLoaf",
            "MangoYoshi",
            "NoScopeNikolai",
            "OutOfMana",
            "PatchlessPelle",
            "QueueKaren",
            "RetroRune",
            "SleepySasha",
            "TaxiDad",
            "UnluckyUlrik",
            "ValueVille",
            "WashedUpPro",
            "XmasLeftover",
            "YoungAtHeart",
            "ZugzugTony",
            "AnalogAndy",
            "BitrateBella",
            "CraftyCam",
            "DigiDag",
            "EsportElliot",
            "FeralFreja",
            "GlutenFreeGreg",
            "HardstuckHugo",
            "ImperfectIda",
            "JankJohan",
            "KindaKratos",
            "LowEffortLeo",
            "MetaMina",
            "NoisyNora",
            "OkayOlle",
            "PingPanda",
            "QueueQuinn",
            "RanklessRik",
            "ScuffedSara",
            "TryAgainTom",
            "UnmutedUlf",
            "VHSVera",
            "WaveformWally",
            "XPGrinder",
            "YawnYlva",
            "ZoneZara",
            // A *few* fishy ones for flavour:
            "SaltyTrout",
            "CarpDiEm",
            "MinnowMike",
            "TroutScout",
            "CatfishCaro"
        };

        // --- Public API used by HookCardWorldRecordBinder ---

        /// <summary>
        /// Name shown as the holder of the *length* world record for this species.
        /// </summary>
        public static string GetLengthHolder(int speciesId)
        {
            return GetBySpeciesIndex(speciesId);
        }

        /// <summary>
        /// Name shown as the holder of the *weight* world record for this species.
        /// </summary>
        public static string GetWeightHolder(int speciesId)
        {
            return GetBySpeciesIndex(speciesId);
        }

        // --- Internals ---

        private static string GetBySpeciesIndex(int speciesId)
        {
            if (Names == null || Names.Length == 0)
                return "Unknown";

            if (speciesId < 0)
                speciesId = 0;

            int index = speciesId % Names.Length;
            return Names[index];
        }
    }
}
