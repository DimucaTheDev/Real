using FMOD;

namespace Real;

public class SoundTest
{
    public static string Test()
    {
        return FMOD.Error.String(FMOD.Studio.System.create(out _));
    }
}