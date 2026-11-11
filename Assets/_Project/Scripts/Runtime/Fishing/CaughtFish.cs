namespace Offshore
{
    /// <summary>One fish: what it is, how heavy, and where it was caught.</summary>
    public readonly struct CaughtFish
    {
        public readonly FishSpecies Species;
        public readonly float Kg;
        public readonly string Ground;

        public CaughtFish(FishSpecies species, float kg, string ground)
        {
            Species = species;
            Kg = kg;
            Ground = ground;
        }

        public string Name => Species != null ? Species.DisplayName : "Fish";

        public override string ToString() => $"{Kg:0.0} kg {Name}";
    }
}
