using Verse;

namespace UniversalFermenterSK
{
    [StaticConstructorOnStartup]
    public static class Static_Weather
    {
        public static readonly FloatRange SunGlowRange = new(0f, 1.0f);
        public static readonly FloatRange SnowRateRange = new(0f, 1.2f);
        public static readonly FloatRange RainRateRange = new(0f, 1.0f);
        public static readonly FloatRange WindSpeedRange = new(0f, 3f);
    }
}
